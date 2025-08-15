using System;
using System.Windows;
using MKXLobbyContracts;
using MKXLobbyModels;

namespace MKXLobbyDuplexClient
{
    public class LobbyCallbackHandler : ILobbyCallback
    {
        public event Action<ChatMessage> MessageReceived;
        public event Action<string, string> PlayerJoined;
        public event Action<string, string> PlayerLeft;
        public event Action<SharedFile> FileShared;
        public event Action<LobbyRoom> RoomCreated;

        public void OnMessageReceived(ChatMessage message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageReceived?.Invoke(message);
            });
        }

        public void OnPlayerJoined(string username, string roomName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PlayerJoined?.Invoke(username, roomName);
            });
        }

        public void OnPlayerLeft(string username, string roomName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PlayerLeft?.Invoke(username, roomName);
            });
        }

        public void OnFileShared(SharedFile file)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                FileShared?.Invoke(file);
            });
        }

        public void OnRoomCreated(LobbyRoom room)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RoomCreated?.Invoke(room);
            });
        }
    }
}