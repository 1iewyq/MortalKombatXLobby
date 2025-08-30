using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using MKXLobbyContracts;
using MKXLobbyModels;
using System.Collections.Concurrent;

using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyServerDuplex
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single)]
    public class LobbyServiceD : ILobbyServiceDuplex
    {
        private static Dictionary<string, Player> onlinePlayers = new Dictionary<string, Player>();
        private static Dictionary<string, LobbyRoom> lobbyRooms = new Dictionary<string, LobbyRoom>();
        private static List<ChatMessage> allMessages = new List<ChatMessage>();
        private static List<SharedFile> allSharedFiles = new List<SharedFile>();
        private static object lockObject = new object();

        private static ConcurrentDictionary<string, ILobbyServiceCallback> clientCallbacks = new ConcurrentDictionary<string, ILobbyServiceCallback>();
        public bool LoginPlayer(string username)
        {
            lock (lockObject)
            {
                if (onlinePlayers.ContainsKey(username))
                    return false; //seurname already exists

                onlinePlayers[username] = new Player
                {
                    Username = username,
                    LoginTime = DateTime.Now,
                    IsOnline = true,
                    CurrentRoom = null
                };

                var callback = OperationContext.Current.GetCallbackChannel<ILobbyServiceCallback>();
                clientCallbacks[username] = callback;

                Console.WriteLine($"Player {username} logged in at {DateTime.Now}");
                return true;
            }
        }

        public void LogoutPlayer(string username)
        {
            lock (lockObject)
            {
                if (onlinePlayers.ContainsKey(username))
                {
                    //remove from current room if any
                    LeaveRoom(username);

                    onlinePlayers.Remove(username);

                    clientCallbacks.TryRemove(username, out _);
                    Console.WriteLine($"Player {username} logged out at {DateTime.Now}");
                    NotifyRoomListUpdated();
                }
            }
        }

        public void SubscribeToUpdate(string username)
        {
            Console.WriteLine($"Player {username} subscribed to updates");
        }

        public void UnsubscribeFromUpdate(string username)
        {
            clientCallbacks.TryRemove(username, out _);
            Console.WriteLine($"Player {username} unsubscribe from updates");
        }

        public bool CreateRoom(string roomName, string createdBy, string username)
        {
            lock (lockObject)
            {
                if (lobbyRooms.ContainsKey(roomName))
                    return false; //room already exists

                lobbyRooms[roomName] = new LobbyRoom
                {
                    RoomName = roomName,
                    CreatedBy = createdBy,
                    CreatedTime = DateTime.Now
                };

                lobbyRooms[roomName].Players.Add(username);
                onlinePlayers[username].CurrentRoom = roomName;

                Console.WriteLine($"Room {roomName} created by {createdBy}");
                Console.WriteLine($"Player {username} joined room {roomName}");

                NotifyRoomListUpdated();
                return true;
            }
        }

        public bool JoinRoom(string roomName, string username)
        {
            lock (lockObject)
            {
                if (!lobbyRooms.ContainsKey(roomName) || !onlinePlayers.ContainsKey(username))
                    return false;

                //leave current room if any
                LeaveRoom(username);

                lobbyRooms[roomName].Players.Add(username);
                onlinePlayers[username].CurrentRoom = roomName;

                Console.WriteLine($"Player {username} joined room {roomName}");

                NotifyPlayerJoinedRoom(roomName, username);
                NotifyRoomDataUpdated(roomName);
                return true;
            }
        }

        public void LeaveRoom(string username)
        {
            lock (lockObject)
            {
                if (!onlinePlayers.ContainsKey(username))
                    return;

                string currentRoom = onlinePlayers[username].CurrentRoom;
                if (!string.IsNullOrEmpty(currentRoom) && lobbyRooms.ContainsKey(currentRoom))
                {
                    lobbyRooms[currentRoom].Players.Remove(username);
                    onlinePlayers[username].CurrentRoom = null;

                    NotifyPlayerLeftRoom(currentRoom, username);

                    //remove room if empty
                    if (lobbyRooms[currentRoom].Players.Count == 0)
                    {
                        lobbyRooms.Remove(currentRoom);
                        Console.WriteLine($"Room {currentRoom} removed (empty)");

                        NotifyRoomListUpdated();
                    }
                    else
                    {
                        NotifyRoomDataUpdated(currentRoom);
                    }

                        Console.WriteLine($"Player {username} left room {currentRoom}");
                }
            }
        }      

        public void SendMessage(ChatMessage message)
        {
            lock (lockObject)
            {
                message.Timestamp = DateTime.Now;
                allMessages.Add(message);

                string messageType = message.IsPrivate ? "private" : "public";
                Console.WriteLine($"{messageType} message from {message.From}: {message.Content}");

                if (message.IsPrivate)
                {
                    NotifyPrivateMessage(message);
                }
                else
                {
                    NotifyRoomDataUpdated(message.RoomName);
                }
            }
        }

        public bool ShareFile(SharedFile file)
        {
            lock (lockObject)
            {
                file.SharedTime = DateTime.Now;
                allSharedFiles.Add(file);

                Console.WriteLine($"File {file.FileName} shared by {file.SharedBy} in room {file.RoomName}");
                
                NotifyRoomDataUpdated(file.RoomName);
                return true;
            }
        }

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

        public List<string> GetOnlinePlayers()
        {
            lock (lockObject)
            {
                return onlinePlayers.Keys.ToList();
            }
        }

        public List<LobbyRoom> GetAvailableRooms()
        {
            lock (lockObject)
            {
                return lobbyRooms.Values.ToList();
            }
        }

        public List<string> GetPlayersInRoom(string roomName)
        {
            lock (lockObject)
            {
                if (lobbyRooms.ContainsKey(roomName))
                    return lobbyRooms[roomName].Players.ToList();
                return new List<string>();
            }
        }

        public List<ChatMessage> GetRoomMessages(string roomName)
        {
            lock (lockObject)
            {
                return allMessages
                    .Where(m => m.RoomName == roomName && !m.IsPrivate)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
            }
        }

        public List<ChatMessage> GetPrivateMessages(string username)
        {
            lock (lockObject)
            {
                return allMessages
                    .Where(m => m.IsPrivate && (m.From == username || m.To == username))
                    .OrderBy(m => m.Timestamp)
                    .ToList();
            }
        }

        public List<SharedFile> GetSharedFiles(string roomName)
        {
            lock (lockObject)
            {
                return allSharedFiles
                    .Where(f => f.RoomName == roomName)
                    .OrderBy(f => f.SharedTime)
                    .ToList();
            }
        }

        public SharedFile DownloadFile(string fileName, string roomName)
        {
            lock (lockObject)
            {
                return allSharedFiles
                    .FirstOrDefault(f => f.FileName == fileName && f.RoomName == roomName);
            }
        }
    }
}
