using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MKXLobbyContracts;
using MKXLobbyModels;
using System.Runtime.Serialization;


namespace MKXLobbyClient
{
    public partial class MainWindow : Window
    {
        private ILobbyService lobbyService;
        private string currentUsername;
        private string currentRoom;
        private Timer pollTimer;
        private Dictionary<string, PrivateMessageWindow> privateWindows;

        public MainWindow()
        {
            InitializeComponent();
            InitializeService();
            privateWindows = new Dictionary<string, PrivateMessageWindow>();
        }

        private void InitializeService()
        {
            try
            {
                BasicHttpBinding binding = new BasicHttpBinding();
                binding.MaxBufferSize = int.MaxValue;
                binding.MaxReceivedMessageSize = int.MaxValue;
                binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
                binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;

                EndpointAddress endpoint = new EndpointAddress("http://localhost:8080/LobbyService");
                ChannelFactory<ILobbyService> factory = new ChannelFactory<ILobbyService>(binding, endpoint);
                lobbyService = factory.CreateChannel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to server: {ex.Message}", "Connection Error");
            }
        }

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
                    lblWelcome.Text = $"Welcome, {currentUsername}!";

                    LoginPanel.Visibility = Visibility.Collapsed;
                    MainLobbyPanel.Visibility = Visibility.Visible;

                    // Start polling for updates
                    StartPolling();

                    // Load available rooms
                    await RefreshRooms();
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
                StopPolling();

                if (!string.IsNullOrEmpty(currentUsername))
                {
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
                    await RefreshRooms();
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
                        lblCurrentRoom.Text = $"Room: {currentRoom}";

                        MainLobbyPanel.Visibility = Visibility.Collapsed;
                        RoomChatPanel.Visibility = Visibility.Visible;

                        await RefreshRoomData();
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

                await RefreshRooms();
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

        #region Refresh Methods

        private async void BtnRefreshRooms_Click(object sender, RoutedEventArgs e)
        {
            await RefreshRooms();
        }

        private async void BtnRefreshPlayers_Click(object sender, RoutedEventArgs e)
        {
            await RefreshRoomData();
        }

        private async Task RefreshRooms()
        {
            try
            {
                var rooms = await Task.Run(() => lobbyService.GetAvailableRooms());

                Dispatcher.Invoke(() =>
                {
                    lstRooms.Items.Clear();
                    foreach (var room in rooms)
                    {
                        lstRooms.Items.Add(room);
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Error refreshing rooms: {ex.Message}", "Error"));
            }
        }

        private async Task RefreshRoomData()
        {
            if (string.IsNullOrEmpty(currentRoom))
                return;

            try
            {
                var messages = await Task.Run(() => lobbyService.GetRoomMessages(currentRoom));
                var players = await Task.Run(() => lobbyService.GetPlayersInRoom(currentRoom));
                var files = await Task.Run(() => lobbyService.GetSharedFiles(currentRoom));

                Dispatcher.Invoke(() =>
                {
                    // Update messages
                    txtChatMessages.Text = string.Join("\n", messages.Select(m =>
                        $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Content}"));

                    // Auto-scroll to bottom
                    chatScrollViewer.ScrollToBottom();

                    // Update players
                    lstPlayers.Items.Clear();
                    foreach (var player in players)
                    {
                        lstPlayers.Items.Add(player);
                    }

                    // Update files
                    lstSharedFiles.Items.Clear();
                    foreach (var file in files)
                    {
                        lstSharedFiles.Items.Add(file);
                    }
                });
            }
            catch (Exception ex)
            {
                // Handle silently for polling errors
                Console.WriteLine($"Polling error: {ex.Message}");
            }
        }

        #endregion

        #region Polling

        private void StartPolling()
        {
            pollTimer = new Timer(async _ =>
            {
                if (!string.IsNullOrEmpty(currentRoom))
                {
                    await RefreshRoomData();
                }
                else
                {
                    await RefreshRooms();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        private void StopPolling()
        {
            pollTimer?.Dispose();
            pollTimer = null;
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            StopPolling();

            try
            {
                if (!string.IsNullOrEmpty(currentUsername))
                {
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