using MKXLobbyContracts;
using MKXLobbyModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace MKXLobbyServer
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single)]
    public class LobbyDuplexService : LobbyService, ILobbyDuplexService
    {
        private static Dictionary<string, ILobbyCallback> connectedClients = new Dictionary<string, ILobbyCallback>();
        private static object callbackLockObject = new object();

        public void RegisterForCallbacks(string username)
        {
            lock (callbackLockObject)
            {
                var callback = OperationContext.Current.GetCallbackChannel<ILobbyCallback>();
                connectedClients[username] = callback;

                Console.WriteLine($"Client {username} registered for callbacks");
            }
        }

        public void UnregisterFromCallbacks(string username)
        {
            lock (callbackLockObject)
            {
                if (connectedClients.ContainsKey(username))
                {
                    connectedClients.Remove(username);
                    Console.WriteLine($"Client {username} unregistered from callbacks");
                }
            }
        }

        // Override base methods to include callbacks
        public new bool JoinRoom(string roomName, string username)
        {
            bool result = base.JoinRoom(roomName, username);

            if (result)
            {
                // Notify all clients in the room
                NotifyPlayersInRoom(roomName, callback => callback.OnPlayerJoined(username, roomName));
            }

            return result;
        }

        public new void LeaveRoom(string username)
        {
            string roomName = GetPlayerCurrentRoom(username);

            base.LeaveRoom(username);

            if (!string.IsNullOrEmpty(roomName))
            {
                // Notify remaining players in the room
                NotifyPlayersInRoom(roomName, callback => callback.OnPlayerLeft(username, roomName));
            }
        }

        public new void SendMessage(ChatMessage message)
        {
            base.SendMessage(message);

            // Notify relevant clients
            if (message.IsPrivate)
            {
                // Notify sender and recipient
                NotifyClient(message.From, callback => callback.OnMessageReceived(message));
                NotifyClient(message.To, callback => callback.OnMessageReceived(message));
            }
            else
            {
                // Notify all players in the room
                NotifyPlayersInRoom(message.RoomName, callback => callback.OnMessageReceived(message));
            }
        }

        public new bool ShareFile(SharedFile file)
        {
            bool result = base.ShareFile(file);

            if (result)
            {
                // Notify all players in the room
                NotifyPlayersInRoom(file.RoomName, callback => callback.OnFileShared(file));
            }

            return result;
        }

        public new bool CreateRoom(string roomName, string createdBy)
        {
            bool result = base.CreateRoom(roomName, createdBy);

            if (result)
            {
                var room = GetRoom(roomName);
                if (room != null)
                {
                    // Notify all connected clients
                    NotifyAllClients(callback => callback.OnRoomCreated(room));
                }
            }

            return result;
        }

        #region Helper Methods

        private void NotifyClient(string username, Action<ILobbyCallback> action)
        {
            lock (callbackLockObject)
            {
                if (connectedClients.ContainsKey(username))
                {
                    try
                    {
                        action(connectedClients[username]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying client {username}: {ex.Message}");
                        // Remove disconnected client
                        connectedClients.Remove(username);
                    }
                }
            }
        }

        private void NotifyPlayersInRoom(string roomName, Action<ILobbyCallback> action)
        {
            var playersInRoom = GetPlayersInRoom(roomName);

            foreach (string player in playersInRoom)
            {
                NotifyClient(player, action);
            }
        }

        private void NotifyAllClients(Action<ILobbyCallback> action)
        {
            lock (callbackLockObject)
            {
                var clientsToRemove = new List<string>();

                foreach (var kvp in connectedClients.ToList())
                {
                    try
                    {
                        action(kvp.Value);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error notifying client {kvp.Key}: {ex.Message}");
                        clientsToRemove.Add(kvp.Key);
                    }
                }

                // Remove disconnected clients
                foreach (string client in clientsToRemove)
                {
                    connectedClients.Remove(client);
                }
            }
        }

        private string GetPlayerCurrentRoom(string username)
        {
            // This method should be implemented in the base LobbyService
            // For now, we'll implement a simple version
            var player = GetOnlinePlayers().FirstOrDefault(p => p == username);
            if (player != null)
            {
                // Look through all rooms to find where this player is
                foreach (var room in GetAvailableRooms())
                {
                    if (room.Players.Contains(username))
                    {
                        return room.RoomName;
                    }
                }
            }
            return null;
        }

        private LobbyRoom GetRoom(string roomName)
        {
            return GetAvailableRooms().FirstOrDefault(r => r.RoomName == roomName);
        }

        #endregion
    }
}
