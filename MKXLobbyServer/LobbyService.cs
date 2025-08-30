using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using MKXLobbyContracts;
using MKXLobbyModels;

using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyServer
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single)]
    public class LobbyService : ILobbyService
    {
        private static Dictionary<string, Player> onlinePlayers = new Dictionary<string, Player>();
        private static Dictionary<string, LobbyRoom> lobbyRooms = new Dictionary<string, LobbyRoom>();
        private static List<ChatMessage> allMessages = new List<ChatMessage>();
        private static List<SharedFile> allSharedFiles = new List<SharedFile>();
        private static object lockObject = new object();

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
                    Console.WriteLine($"Player {username} logged out at {DateTime.Now}");
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
                return true;
            }
        }

        public List<LobbyRoom> GetAvailableRooms()
        {
            lock (lockObject)
            {
                return lobbyRooms.Values.ToList();
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

        public List<string> GetPlayersInRoom(string roomName)
        {
            lock (lockObject)
            {
                if (lobbyRooms.ContainsKey(roomName))
                    return lobbyRooms[roomName].Players.ToList();
                return new List<string>();
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

        public bool ShareFile(SharedFile file)
        {
            lock (lockObject)
            {
                file.SharedTime = DateTime.Now;
                allSharedFiles.Add(file);

                Console.WriteLine($"File {file.FileName} shared by {file.SharedBy} in room {file.RoomName}");
                return true;
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
