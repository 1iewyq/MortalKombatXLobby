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
    /* Handles callback operations from the lobby service for real-time updates
       Implements the ILobbyServiceCallback interface for duplex communication */
    public class LobbyCallBackHandler : ILobbyServiceCallback
    {
        //Reference to the main application window for UI updates
        private MainWindowD _mainWindow;
        //Dispatcher for thread-safe UI updates from callback threads
        private Dispatcher _dispatcher;

        //Initializes a new instance of the LobbyCallBackHandler
        public LobbyCallBackHandler(MainWindowD mainWindow)
        {
            _mainWindow = mainWindow;
            //Store the current dispatcher for UI thread operations
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        //Callback method triggered when the room list is updated by the server
        public void OnRoomListUpdated(List<LobbyRoom> rooms)
        {
            //Update UI on the main thread
            _dispatcher.Invoke(() =>
            {
                string selectedRoomName = null;
                //Preserve the currently selected room to maintain user selection after update
                if (_mainWindow.lstRooms.SelectedItem is LobbyRoom selectedRoom)
                {
                    selectedRoomName = selectedRoom.RoomName;
                }

                //Clear the current room list and repopulate with updated data
                _mainWindow.lstRooms.Items.Clear();
                foreach (var room in rooms)
                {
                    _mainWindow.lstRooms.Items.Add(room);
                }

                //Restore selection if the previously selected room still exists
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

        //Callback method triggered when room-specific data is updated
        //Only processes updates for the current room
        public void OnRoomDataUpdated(string roomName, List<ChatMessage> messages, List<string> players, List<SharedFile> files)
        {
            //Only process updates for the room the user is currently in
            if (_mainWindow.CurrentRoom == roomName)
            {
                _dispatcher.Invoke(() =>
                {
                    _mainWindow.txtChatMessages.Text = string.Join("\n", messages.Select(m => $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Content}"));
                    //Auto-scroll to the latest message
                    _mainWindow.chatScrollViewer.ScrollToBottom();

                    //Update player list
                    _mainWindow.lstPlayers.Items.Clear();
                    foreach (var player in players)
                    {
                        _mainWindow.lstPlayers.Items.Add(player);
                    }

                    //Update shared files list
                    _mainWindow.lstSharedFiles.Items.Clear();
                    foreach (var file in files)
                    {
                        _mainWindow.lstSharedFiles.Items.Add(file);
                    }
                });
            }
        }

        //Callback method triggered when a private message is received
        public void OnPrivateMessageReceived(ChatMessage message)
        {
            _dispatcher.Invoke(() =>
            {
                // Create a new private chat window if one doesn't exist for the current user
                if (!_mainWindow.PrivateWindows.ContainsKey(message.From) && message.From != _mainWindow.CurrentUsername)
                {
                    var privateWindow = new PrivateChatWindowD(_mainWindow.CurrentUsername, message.From, _mainWindow.LobbyService);
                    //Clean up the window reference when the window is closed
                    privateWindow.Closed += (s, args) => _mainWindow.PrivateWindows.Remove(message.From);
                    _mainWindow.PrivateWindows[message.From] = privateWindow;
                    privateWindow.Show();
                }

                //Display the message in the appropriate private chat window
                if (_mainWindow.PrivateWindows.TryGetValue(message.From, out var window))
                {
                    window.DisplayMessage(message);
                }
            });
        }

        //Callback method triggered when a player joins a room
        public void OnPlayerJoinedRoom(string roomName, string username)
        {
            // Only update if the player joined the current room
            if (_mainWindow.CurrentRoom == roomName)
            {
                _dispatcher.Invoke(() =>
                {
                    // Add the new player to the player list
                    _mainWindow.lstPlayers.Items.Add(username);
                });
            }
        }

        //Callback method triggered when a player leaves a room
        public void OnPlayerLeftRoom(string roomName, string username)
        {
            //Only update if the player left the current room
            if (_mainWindow.CurrentRoom == roomName)
            {
                _dispatcher.Invoke(() =>
                {
                    //Remove the player from the player list
                    _mainWindow.lstPlayers.Items.Remove(username);
                });
            }
        }
    }
}
