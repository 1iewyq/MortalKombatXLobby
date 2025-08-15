using Microsoft.Win32;
using MKXLobbyContracts;
using MKXLobbyModels;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MKXLobbyDuplexClient
{
    public partial class MainWindow : Window
    {
        private ILobbyDuplexService lobbyService;
        private LobbyCallbackHandler callbackHandler;
        private string currentUsername;
        private string currentRoom;
        private Dictionary<string, PrivateMessageWindow> privateWindows;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDuplexService();
            privateWindows = new Dictionary<string, PrivateMessageWindow>();
        }

        private void InitializeDuplexService()
        {
            try
            {
                callbackHandler = new LobbyCallbackHandler();
                SetupCallbackHandlers();

                NetTcpBinding binding = new NetTcpBinding();
                binding.MaxBufferSize = int.MaxValue;
                binding.MaxReceivedMessageSize = int.MaxValue;
                binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
                binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;

                EndpointAddress endpoint = new EndpointAddress("net.tcp://localhost:8081/LobbyDuplexService");

                DuplexChannelFactory<ILobbyDuplexService> factory =
                    new DuplexChannelFactory<ILobbyDuplexService>(callbackHandler, binding, endpoint);

                lobbyService = factory.CreateChannel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to duplex server: {ex.Message}", "Connection Error");
            }
        }

        private void SetupCallbackHandlers()
        {
            callbackHandler.MessageReceived += OnMessageReceived;
            callbackHandler.PlayerJoined += OnPlayerJoined;
            callbackHandler.PlayerLeft += OnPlayerLeft;
            callbackHandler.FileShared += OnFileShared;
            callbackHandler.RoomCreated += OnRoomCreated;
        }

        #region Callback Event Handlers

        private void OnMessageReceived(ChatMessage message)
        {
            if (message.IsPrivate)
            {
                // Handle private message
                string otherUser = message.From == currentUsername ? message.To : message.From;

                if (!privateWindows.ContainsKey(otherUser))
                {
                    var privateWindow = new PrivateMessageWindow(currentUsername, otherUser, lobbyService);
                    privateWindow.Closed += (s, args) => privateWindows.Remove(otherUser);
                    privateWindows[otherUser] = privateWindow;
                    privateWindow.Show();
                }

                // The private window will handle updating its own messages
            }
            else if (message.RoomName == currentRoom)
            {
                // Handle public room message
                RefreshRoomMessages();
            }
        }

        private void OnPlayerJoined(string username, string roomName)
        {
            if (roomName == currentRoom)
            {
                RefreshPlayersList();
            }
        }

        private void OnPlayerLeft(string username, string roomName)
        {
            if (roomName == currentRoom)
            {
                RefreshPlayersList();
            }
        }

        private void OnFileShared(SharedFile file)
        {
            if (file.RoomName == currentRoom)
            {
                RefreshSharedFiles();
            }
        }

        private void OnRoomCreated(LobbyRoom room)
        {
            if (string.IsNullOrEmpty(currentRoom))
            {
                // Only refresh if we're in the lobby view
                RefreshRoomsList();
            }
        }

        #endregion

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                lblLoginStatus.Text = "Please enter a username.";
                return;
            }

            try
            {
                bool loginSuccess = await Task.Run(() => lobbyService.LoginPlayer(username));

                if (loginSuccess)
                {
                    currentUsername = username;
                    lblWelcome.Text = $"Welcome, {currentUsername}! (Real-time Mode)";

                    // Register for callbacks
                    await Task.Run(() => lobbyService.RegisterForCallbacks(currentUsername));

                    LoginPanel.Visibility = Visibility.Collapsed;
                    MainLobbyPanel.Visibility = Visibility.Visible;

                    // Load available rooms
                    await RefreshRoomsList();
                }
                else
                {
                    lblLoginStatus.Text = "Username already exists. Please choose another.";
                }
            }
            catch (Exception ex)
            {
                lblLoginStatus.Text = $"Login failed: {ex.Message}";
            }
        }

        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    await Task.Run(() => lobbyService.UnregisterFromCallbacks(currentUsername));
                    await Task.Run(() => lobbyService.LogoutPlayer(currentUsername));
                }

                // Close all private message windows
                foreach (var window in privateWindows.Values)
                {
                    window.Close();
                }
                privateWindows.Clear();

                // Reset UI
                currentUsername = null;
                currentRoom = null;

                MainLobbyPanel.Visibility = Visibility.Collapsed;
                RoomChatPanel.Visibility = Visibility.Collapsed;
                LoginPanel.Visibility = Visibility.Visible;

                txtUsername.Text = "";
                lblLoginStatus.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Logout error: {ex.Message}", "Error");
            }
        }

        private async void BtnCreateRoom_Click(object sender, RoutedEventArgs e)
        {
            string roomName = txtNewRoomName.Text.Trim();

            if (string.IsNullOrEmpty(roomName))
            {
                lblRoomStatus.Text = "Please enter a room name.";
                return;
            }

            try
            {
                bool success = await Task.Run(() => lobbyService.CreateRoom(roomName, currentUsername));

                if (success)
                {
                    lblRoomStatus.Text = $"Room '{roomName}' created successfully!";
                    txtNewRoomName.Text = "";
                    // Room list will be updated via callback
                }
                else
                {
                    lblRoomStatus.Text = "Room name already exists.";
                }
            }
            catch (Exception ex)
            {
                lblRoomStatus.Text = $"Error creating room: {ex.Message}";
            }
        }

        private async void BtnJoinRoom_Click(object sender, RoutedEventArgs e)
        {
            if (lstRooms.SelectedItem is LobbyRoom selectedRoom)
            {
                try
                {
                    bool success = await Task.Run(() => lobbyService.JoinRoom(selectedRoom.RoomName, currentUsername));

                    if (success)
                    {
                        currentRoom = selectedRoom.RoomName;
                        lblCurrentRoom.Text = $"Room: {currentRoom} (Real-time)";

                        MainLobbyPanel.Visibility = Visibility.Collapsed;
                        RoomChatPanel.Visibility = Visibility.Visible;

                        await InitializeRoomData();
                    }
                    else
                    {
                        MessageBox.Show("Failed to join room.", "Error");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error joining room: {ex.Message}", "Error");
                }
            }
            else
            {
                MessageBox.Show("Please select a room to join.", "No Room Selected");
            }
        }

        private async void BtnLeaveRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() => lobbyService.LeaveRoom(currentUsername));

                currentRoom = null;

                RoomChatPanel.Visibility = Visibility.Collapsed;
                MainLobbyPanel.Visibility = Visibility.Visible;

                // Clear room data
                txtChatMessages.Text = "";
                lstPlayers.Items.Clear();
                lstSharedFiles.Items.Clear();

                await RefreshRoomsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error leaving room: {ex.Message}", "Error");
            }
        }

        private async void BtnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void TxtMessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            string messageContent = txtMessageInput.Text.Trim();

            if (string.IsNullOrEmpty(messageContent) || string.IsNullOrEmpty(currentRoom))
                return;

            try
            {
                var message = new ChatMessage
                {
                    From = currentUsername,
                    Content = messageContent,
                    RoomName = currentRoom,
                    IsPrivate = false,
                    Timestamp = DateTime.Now
                };

                await Task.Run(() => lobbyService.SendMessage(message));
                txtMessageInput.Text = "";
                // Message will appear via callback
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error");
            }
        }

        private async void BtnShareFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Text files (*.txt)|*.txt|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string fileName = Path.GetFileName(openFileDialog.FileName);
                    byte[] fileContent = File.ReadAllBytes(openFileDialog.FileName);
                    string fileExtension = Path.GetExtension(openFileDialog.FileName).ToLower();

                    string fileType = "other";
                    if (fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || fileExtension == ".bmp")
                        fileType = "image";
                    else if (fileExtension == ".txt")
                        fileType = "text";

                    var sharedFile = new SharedFile
                    {
                        FileName = fileName,
                        FileContent = fileContent,
                        SharedBy = currentUsername,
                        RoomName = currentRoom,
                        FileType = fileType,
                        SharedTime = DateTime.Now
                    };

                    bool success = await Task.Run(() => lobbyService.ShareFile(sharedFile));

                    if (success)
                    {
                        MessageBox.Show("File shared successfully!", "Success");
                        // File list will update via callback
                    }
                    else
                    {
                        MessageBox.Show("Failed to share file.", "Error");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error sharing file: {ex.Message}", "Error");
                }
            }
        }

        private async void LstSharedFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstSharedFiles.SelectedItem is SharedFile selectedFile)
            {
                try
                {
                    var file = await Task.Run(() => lobbyService.DownloadFile(selectedFile.FileName, currentRoom));

                    if (file != null)
                    {
                        SaveFileDialog saveFileDialog = new SaveFileDialog();
                        saveFileDialog.FileName = file.FileName;

                        if (saveFileDialog.ShowDialog() == true)
                        {
                            File.WriteAllBytes(saveFileDialog.FileName, file.FileContent);
                            MessageBox.Show("File downloaded successfully!", "Success");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error downloading file: {ex.Message}", "Error");
                }
            }
        }

        private void LstPlayers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstPlayers.SelectedItem is string selectedPlayer && selectedPlayer != currentUsername)
            {
                if (!privateWindows.ContainsKey(selectedPlayer))
                {
                    var privateWindow = new PrivateMessageWindow(currentUsername, selectedPlayer, lobbyService);
                    privateWindow.Closed += (s, args) => privateWindows.Remove(selectedPlayer);
                    privateWindows[selectedPlayer] = privateWindow;
                }

                privateWindows[selectedPlayer].Show();
                privateWindows[selectedPlayer].Activate();
            }
        }

        #region Manual Refresh Methods (Fallback)

        private async void BtnRefreshRooms_Click(object sender, RoutedEventArgs e)
        {
            await RefreshRoomsList();
        }

        private async void BtnRefreshPlayers_Click(object sender, RoutedEventArgs e)
        {
            await RefreshPlayersList();
        }

        #endregion

        #region Data Refresh Methods

        private async Task RefreshRoomsList()
        {
            try
            {
                var rooms = await Task.Run(() => lobbyService.GetAvailableRooms());

                lstRooms.Items.Clear();
                foreach (var room in rooms)
                {
                    lstRooms.Items.Add(room);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing rooms: {ex.Message}", "Error");
            }
        }

        private async Task InitializeRoomData()
        {
            await RefreshRoomMessages();
            await RefreshPlayersList();
            await RefreshSharedFiles();
        }

        private async Task RefreshRoomMessages()
        {
            if (string.IsNullOrEmpty(currentRoom))
                return;

            try
            {
                var messages = await Task.Run(() => lobbyService.GetRoomMessages(currentRoom));

                txtChatMessages.Text = string.Join("\n", messages.Select(m =>
                    $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Content}"));

                // Auto-scroll to bottom
                chatScrollViewer.ScrollToBottom();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing messages: {ex.Message}");
            }
        }

        private async Task RefreshPlayersList()
        {
            if (string.IsNullOrEmpty(currentRoom))
                return;

            try
            {
                var players = await Task.Run(() => lobbyService.GetPlayersInRoom(currentRoom));

                lstPlayers.Items.Clear();
                foreach (var player in players)
                {
                    lstPlayers.Items.Add(player);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing players: {ex.Message}");
            }
        }

        private async Task RefreshSharedFiles()
        {
            if (string.IsNullOrEmpty(currentRoom))
                return;

            try
            {
                var files = await Task.Run(() => lobbyService.GetSharedFiles(currentRoom));

                lstSharedFiles.Items.Clear();
                foreach (var file in files)
                {
                    lstSharedFiles.Items.Add(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing files: {ex.Message}");
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    lobbyService.UnregisterFromCallbacks(currentUsername);
                    lobbyService.LogoutPlayer(currentUsername);
                }
            }
            catch { }

            foreach (var window in privateWindows.Values)
            {
                window.Close();
            }

            base.OnClosed(e);
        }
    }
}