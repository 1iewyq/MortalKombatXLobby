using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using MKXLobbyContracts;
using MKXLobbyModels;

namespace MKXLobbyServer
{
    /* Main lobby service that handles all client requests for the gaming lobby
       This class implements the ILobbyService interface and manages:
       - Player login/logout
       - Room creation and management
       - Message handling (public and private)
       - File sharing between players */

    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single)]
    public class LobbyService : ILobbyService
    {
        //static collections to store data in memory (shared across all clients)
        private static Dictionary<string, Player> onlinePlayers = new Dictionary<string, Player>(); //all logged-in players
        private static Dictionary<string, LobbyRoom> lobbyRooms = new Dictionary<string, LobbyRoom>(); //all created rooms
        private static List<ChatMessage> allMessages = new List<ChatMessage>(); //all chat messages (public and private)
        private static List<SharedFile> allSharedFiles = new List<SharedFile>(); //all shared files

        //thread safety lock to prevent multiple clients from modifying data simultaneously
        private static object lockObject = new object();

        
        //Logs in a new player to the gaming lobby
        //Checks if the username is unique and adds the player to online list
        public bool LoginPlayer(string username)
        {
            lock (lockObject)
            {
                //check if username already exists
                if (onlinePlayers.ContainsKey(username))
                    return false; //username already exists

                //create new player and add to online list
                onlinePlayers[username] = new Player
                {
                    Username = username,
                    LoginTime = DateTime.Now, //record login time
                    IsOnline = true,
                    CurrentRoom = null //player not in any room initially
                };

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
                    Console.WriteLine($"Player {username} logged out at {DateTime.Now}");
                }
            }
        }

        //gets a list of all currently online players
        //Used by clients to display online users
        public List<string> GetOnlinePlayers()
        {
            lock (lockObject)
            {
                //Return a list of usernames of all online players
                return onlinePlayers.Keys.ToList();
            }
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
                    CreatedBy = createdBy, //track who created the room
                    CreatedTime = DateTime.Now
                };

                //add creator to the room immediately
                lobbyRooms[roomName].Players.Add(username);
                onlinePlayers[username].CurrentRoom = roomName;

                //log room creation and player joining
                Console.WriteLine($"Room {roomName} created by {createdBy}");
                Console.WriteLine($"Player {username} joined room {roomName}");
                return true; //room creation successful
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
                return true; //join successful
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

                    //remove room if empty
                    if (lobbyRooms[currentRoom].Players.Count == 0)
                    {
                        lobbyRooms.Remove(currentRoom);
                        Console.WriteLine($"Room {currentRoom} removed (empty)");
                    }

                    Console.WriteLine($"Player {username} left room {currentRoom}");
                }
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
                return true; //file stored successful
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
