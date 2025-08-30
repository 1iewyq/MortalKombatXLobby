using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using MKXLobbyContracts;
using MKXLobbyModels;

namespace MKXLobbyClientDuplex.CallBackHandlers
{
    public class LobbyCallBackHandler : ILobbyServiceCallback
    {
        private MainWindowD _mainWindow;
        private Dispatcher _dispatcher;

        public LobbyCallBackHandler(MainWindowD mainWindow)
        {
            _mainWindow = mainWindow;
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public void OnRoomListUpdated(List<LobbyRoom> rooms)
        {
            _dispatcher.Invoke(() =>
            {
                string selectedRoomName = null;
                if (_mainWindow.lstRooms.SelectedItem is LobbyRoom selectedRoom)
                {
                    selectedRoomName = selectedRoom.RoomName;
                }

                _mainWindow.lstRooms.Items.Clear();
                foreach (var room in rooms)
                {
                    _mainWindow.lstRooms.Items.Add(room);
                }

                if (!string.IsNullOrEmpty(selectedRoomName))
                {
                    foreach (var item in _mainWindow.lstRooms.Items)
                    {
                        if (item is LobbyRoom room && room.RoomName == selectedRoomName)
                        {
                            _mainWindow.lstRooms.SelectedItem = item;
                            break;
                        }
                    }
                }
            });
        }

        public void OnRoomDataUpdated(string roomName, List<ChatMessage> messages, List<string> players, List<SharedFile> files)
        {
            if (_mainWindow.CurrentRoom == roomName)
            {
                _dispatcher.Invoke(() =>
                {
                    _mainWindow.txtChatMessages.Text = string.Join("\n", messages.Select(m => $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Content}"));
                    _mainWindow.chatScrollViewer.ScrollToBottom();

                    _mainWindow.lstPlayers.Items.Clear();
                    foreach (var player in players)
                    {
                        _mainWindow.lstPlayers.Items.Add(player);
                    }

                    _mainWindow.lstSharedFiles.Items.Clear();
                    foreach (var file in files)
                    {
                        _mainWindow.lstSharedFiles.Items.Add(file);
                    }
                });
            }
        }

        public void OnPrivateMessageReceived(ChatMessage message)
        {
            _dispatcher.Invoke(() =>
            {
                if (!_mainWindow.PrivateWindows.ContainsKey(message.From) && message.From != _mainWindow.CurrentUsername)
                {
                    var privateWindow = new PrivateChatWindowD(_mainWindow.CurrentUsername, message.From, _mainWindow.LobbyService);
                    privateWindow.Closed += (s, args) => _mainWindow.PrivateWindows.Remove(message.From);
                    _mainWindow.PrivateWindows[message.From] = privateWindow;
                    privateWindow.Show();
                }

                if (_mainWindow.PrivateWindows.TryGetValue(message.From, out var window))
                {
                    window.DisplayMessage(message);
                }
            });
        }

        public void OnPlayerJoinedRoom(string roomName, string username)
        {
            if (_mainWindow.CurrentRoom == roomName)
            {
                _dispatcher.Invoke(() =>
                {
                    _mainWindow.lstPlayers.Items.Add(username);
                });
            }
        }

        public void OnPlayerLeftRoom(string roomName, string username)
        {
            if (_mainWindow.CurrentRoom == roomName)
            {
                _dispatcher.Invoke(() =>
                {
                    _mainWindow.lstPlayers.Items.Remove(username);
                });
            }
        }
    }
}
