using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using MKXLobbyContracts;
using MKXLobbyModels;
using System.Collections.Concurrent;

namespace MKXLobbyServerDuplex
{
    /* Main lobby service that handles all client requests for the gaming lobby
       This class implements the ILobbyServiceDuplex interface and manages:
       - Player login/logout
       - Room creation and management
       - Message handling (public and private)
       - File sharing between players */
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single)]
    public class LobbyServiceD : ILobbyServiceDuplex
    {
        //static collections to store data in memory (shared across all clients)
        private static Dictionary<string, Player> onlinePlayers = new Dictionary<string, Player>();
        private static Dictionary<string, LobbyRoom> lobbyRooms = new Dictionary<string, LobbyRoom>();
        private static List<ChatMessage> allMessages = new List<ChatMessage>();
        private static List<SharedFile> allSharedFiles = new List<SharedFile>();
        //thread safety lock to prevent multiple clients from modifying data simultaneously
        private static object lockObject = new object();

        //Thread-safe dictionary to store client callback channels for duplex communication
        private static ConcurrentDictionary<string, ILobbyServiceCallback> clientCallbacks = new ConcurrentDictionary<string, ILobbyServiceCallback>();

        //Logs in a new player to the gaming lobby
        //Checks if the username is unique and adds the player to online list
        public bool LoginPlayer(string username)
        {
            lock (lockObject)
            {
                //check if username already exists
                if (onlinePlayers.ContainsKey(username))
                    return false; //seurname already exists

                //create new player and add to online list
                onlinePlayers[username] = new Player
                {
                    Username = username,
                    LoginTime = DateTime.Now,   //record login time
                    IsOnline = true,
                    CurrentRoom = null  //player not in any room initially
                };

                // Get callback channel for duplex communication with this client
                var callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();
                clientCallbacks[username] = callback;

                //log the login event
                Console.WriteLine($"Player {username} logged in at {DateTime.Now}");
                return true; //login successful
            }
        }

        //Logs out a player from the gaming lobby
        //Removes the player from any room they are in and from the online list
        public void LogoutPlayer(string username)
        {
            lock (lockObject)
            {
                //check if player is online
                if (onlinePlayers.ContainsKey(username))
                {
                    //remove from current room if any
                    LeaveRoom(username);

                    //remove from online players list
                    onlinePlayers.Remove(username);

                    //Remove callback reference
                    clientCallbacks.TryRemove(username, out _);
                    Console.WriteLine($"Player {username} logged out at {DateTime.Now}");

                    //Notify all clients that room list has been updated
                    NotifyRoomListUpdated();
                }
            }
        }

        //Subscribes a player to receive real-time updates
        public void SubscribeToUpdate(string username)
        {
            Console.WriteLine($"Player {username} subscribed to updates");
        }

        //Unsubscribes a player from receiving real-time updates
        public void UnsubscribeFromUpdate(string username)
        {
            clientCallbacks.TryRemove(username, out _);
            Console.WriteLine($"Player {username} unsubscribe from updates");
        }

        //Creates a new room in the lobby
        public bool CreateRoom(string roomName, string createdBy, string username)
        {
            lock (lockObject)
            {
                //check if room name already exists
                if (lobbyRooms.ContainsKey(roomName))
                    return false; //room already exists

                //create new lobby room
                lobbyRooms[roomName] = new LobbyRoom
                {
                    RoomName = roomName,
                    CreatedBy = createdBy,
                    CreatedTime = DateTime.Now
                };

                //add creator to the room immediately
                lobbyRooms[roomName].Players.Add(username);
                onlinePlayers[username].CurrentRoom = roomName;

                //log room creation and player joining
                Console.WriteLine($"Room {roomName} created by {createdBy}");
                Console.WriteLine($"Player {username} joined room {roomName}");

                // Notify all clients that room list has been updated
                NotifyRoomListUpdated();
                return true; //room creation successful
            }
        }

        //Adds a player to an existing room
        //Removes them from their current room if they are in one
        public bool JoinRoom(string roomName, string username)
        {
            lock (lockObject)
            {
                //check if both room and player exist
                if (!lobbyRooms.ContainsKey(roomName) || !onlinePlayers.ContainsKey(username))
                    return false;

                //remove player from current room if any
                LeaveRoom(username);

                //add player to the new room
                lobbyRooms[roomName].Players.Add(username);
                onlinePlayers[username].CurrentRoom = roomName;

                Console.WriteLine($"Player {username} joined room {roomName}");

                //Notify all players in the current room about the new player and update room data
                NotifyPlayerJoinedRoom(roomName, username);
                NotifyRoomDataUpdated(roomName);
                return true;//join successful
            }
        }

        //Removes a player from their current room
        //If the room becomes empty, it is deleted automatically
        public void LeaveRoom(string username)
        {
            lock (lockObject)
            {
                //check if player exists
                if (!onlinePlayers.ContainsKey(username))
                    return;

                //get the player's current room
                string currentRoom = onlinePlayers[username].CurrentRoom;
                //if player is in a room, remove them
                if (!string.IsNullOrEmpty(currentRoom) && lobbyRooms.ContainsKey(currentRoom))
                {
                    //remove player from room's player list
                    lobbyRooms[currentRoom].Players.Remove(username);
                    onlinePlayers[username].CurrentRoom = null; //clear current room

                    //Notify players that leave the current room
                    NotifyPlayerLeftRoom(currentRoom, username);

                    //remove room if empty
                    if (lobbyRooms[currentRoom].Players.Count == 0)
                    {
                        lobbyRooms.Remove(currentRoom);
                        Console.WriteLine($"Room {currentRoom} removed (empty)");

                        // Notify all clients that room list has been updated
                        NotifyRoomListUpdated();
                    }
                    else
                    {
                        //Notify all players in the current room about the update room data
                        NotifyRoomDataUpdated(currentRoom);
                    }

                        Console.WriteLine($"Player {username} left room {currentRoom}");
                }
            }
        }

        //Processes and stores a chat message (public or private)
        //All messages are stored in the same list but filtered by type when retrieved
        public void SendMessage(ChatMessage message)
        {
            lock (lockObject)
            {
                //set timestamp and add to message list
                message.Timestamp = DateTime.Now;
                allMessages.Add(message);

                //log the message to console for debugging
                string messageType = message.IsPrivate ? "private" : "public";
                Console.WriteLine($"{messageType} message from {message.From}: {message.Content}");

                if (message.IsPrivate)
                {
                    // Delivers a private message to the intended recipient
                    NotifyPrivateMessage(message);
                }
                else
                {
                    //Notify all players in the current room about the update room data
                    NotifyRoomDataUpdated(message.RoomName);
                }
            }
        }

        //Stores a file shared by a player in a room
        //Files are stored in memory as byte arrays
        public bool ShareFile(SharedFile file)
        {
            lock (lockObject)
            {
                //set the timestamp when file is shared
                file.SharedTime = DateTime.Now;
                //add file to the shared files list
                allSharedFiles.Add(file);

                //log the file sharing event
                Console.WriteLine($"File {file.FileName} shared by {file.SharedBy} in room {file.RoomName}");

                //Notify all players in the current room about the update room data
                NotifyRoomDataUpdated(file.RoomName);
                return true; //file stored successful
            }
        }

        //Notifies all subscribed clients that the room list has been updated
        private void NotifyRoomListUpdated()
        {
            var rooms = GetAvailableRooms();
            foreach (var callback in clientCallbacks.Values)
            {
                try
                {
                    callback.OnRoomListUpdated(rooms);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error notifying client: {ex.Message}");
                }
            }
        }

        //Notifies all subscribed players in a room that room data (messages, players, files) has been updated
        private void NotifyRoomDataUpdated(string roomName)
        {
            var messages = GetRoomMessages(roomName);
            var players = GetPlayersInRoom(roomName);
            var files = GetSharedFiles(roomName);

            var roomPlayers = GetPlayersInRoom(roomName);
            foreach (var player in roomPlayers)
            {
                if (clientCallbacks.TryGetValue(player, out var callback))
                {
                    try
                    {
                        callback.OnRoomDataUpdated(roomName, messages, players, files);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying player {player}: {ex.Message}");
                    }
                }
            }
        }

        // Delivers a private message to the intended recipient
        private void NotifyPrivateMessage(ChatMessage message)
        {
            var recipients = new[] { message.From, message.To };
            foreach (var recipient in recipients)
            {
                if (clientCallbacks.TryGetValue((string)recipient, out var callback))
                {
                    try
                    {
                        callback.OnPrivateMessageReceived(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying {recipient}: {ex.Message}");
                    }
                }
            }
        }

        // Notifies all subscribed players in a room that a new player has joined
        private void NotifyPlayerJoinedRoom(string roomName, string username)
        {
            var roomPlayers = GetPlayersInRoom(roomName);
            foreach (var player in roomPlayers)
            {
                if (clientCallbacks.TryGetValue(player, out var callback))
                {
                    try
                    {
                        callback.OnPlayerJoinedRoom(roomName, username);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying player {player}: {ex.Message}");
                    }
                }
            }
        }

        // Notifies all subscribed players in a room that a player has left
        private void NotifyPlayerLeftRoom(string roomName, string username)
        {
            var roomPlayers = GetPlayersInRoom(roomName);
            foreach (var player in roomPlayers)
            {
                if (clientCallbacks.TryGetValue(player, out var callback))
                {
                    try
                    {
                        callback.OnPlayerLeftRoom(roomName, username);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying player {player}: {ex.Message}");
                    }
                }
            }
        }

        //gets a list of all currently online players
        //Used by clients to display online users
        public List<string> GetOnlinePlayers()
        {
            lock (lockObject)
            {
                // Return a list of usernames of all online players
                return onlinePlayers.Keys.ToList();
            }
        }

        //gets all available rooms for clients to display
        public List<LobbyRoom> GetAvailableRooms()
        {
            lock (lockObject)
            {
                //return all rooms as a list
                return lobbyRooms.Values.ToList();
            }
        }

        //Gets all players currently in a specific room
        //Used to display the player list in the room's UI
        public List<string> GetPlayersInRoom(string roomName)
        {
            lock (lockObject)
            {
                //If room exists, return its player list, otherwise return empty list
                if (lobbyRooms.ContainsKey(roomName))
                    return lobbyRooms[roomName].Players.ToList();
                return new List<string>();
            }
        }

        //Gets all public messages for a specific room
        //Private messages are filtered out
        public List<ChatMessage> GetRoomMessages(string roomName)
        {
            lock (lockObject)
            {
                //filter messages: only public messages for the specified room
                return allMessages
                    .Where(m => m.RoomName == roomName && !m.IsPrivate) //only public messages
                    .OrderBy(m => m.Timestamp) //sort by time (oldest first)
                    .ToList();
            }
        }

        //Gets all private messages for a specific user
        //Returns messages sent to or from the user
        public List<ChatMessage> GetPrivateMessages(string username)
        {
            lock (lockObject)
            {
                //filter messages: only private messages involving the specified user
                return allMessages
                    .Where(m => m.IsPrivate && (m.From == username || m.To == username))
                    .OrderBy(m => m.Timestamp) //sort by time (oldest first)
                    .ToList();
            }
        }

        //Gets all files shared in a specific room
        //Used to display the file list in the room's UI
        public List<SharedFile> GetSharedFiles(string roomName)
        {
            lock (lockObject)
            {
                //filter files: only those shared in the specified room
                return allSharedFiles
                    .Where(f => f.RoomName == roomName) //only files for this room
                    .OrderBy(f => f.SharedTime) //sort by time (oldest first)
                    .ToList();
            }
        }

        //Downloads a specific file from a room
        //Returns the complete file data for the client to save/open
        public SharedFile DownloadFile(string fileName, string roomName)
        {
            lock (lockObject)
            {
                //find and return the specific file
                return allSharedFiles
                    .FirstOrDefault(f => f.FileName == fileName && f.RoomName == roomName);
            }
        }
    }
}
