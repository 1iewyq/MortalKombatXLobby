using Microsoft.Win32;
using MKXLobbyContracts;
using MKXLobbyModels;
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
using System.Windows.Media;
using MKXLobbyClientDuplex.CallBackHandlers;


namespace MKXLobbyClientDuplex
{
    public partial class MainWindowD : Window
    {
        private InstanceContext instanceContext;
        private DuplexChannelFactory<ILobbyServiceDuplex> chanFactory;
        private ILobbyServiceDuplex lobbyService;
        private string currentUsername;
        private string currentRoom;
        private Dictionary<string, PrivateChatWindowD> privateWindows;
        private MediaPlayer backgroundPlayer = new MediaPlayer();

        public string CurrentRoom => currentRoom;
        public string CurrentUsername => currentUsername;
        public Dictionary<string, PrivateChatWindowD> PrivateWindows => privateWindows;
        public ILobbyServiceDuplex LobbyService => lobbyService;
        public MainWindowD()
        {
            InitializeComponent();

            instanceContext = new InstanceContext(new LobbyCallBackHandler(this));

            //configure NetTcpBinding for large file sharing
            var tcp = new NetTcpBinding();
            tcp.MaxBufferSize = 104857600; //100MB
            tcp.MaxReceivedMessageSize = 104857600; //100MB
            tcp.TransferMode = TransferMode.Buffered;
            tcp.SendTimeout = TimeSpan.FromMinutes(10);
            tcp.ReceiveTimeout = TimeSpan.FromMinutes(10);
            tcp.OpenTimeout = TimeSpan.FromMinutes(1);
            tcp.CloseTimeout = TimeSpan.FromMinutes(1);
            tcp.ReaderQuotas.MaxArrayLength = 104857600;
            tcp.ReaderQuotas.MaxStringContentLength = 104857600;
            tcp.ReaderQuotas.MaxDepth = 32;
            tcp.ReaderQuotas.MaxBytesPerRead = 4096;
            tcp.ReaderQuotas.MaxNameTableCharCount = 16384;

            var URL = "net.tcp://localhost:8100/LobbyService";
            //var chanFactory = new ChannelFactory<ILobbyService>(tcp, URL);
            //lobbyService = chanFactory.CreateChannel();

            chanFactory = new DuplexChannelFactory<ILobbyServiceDuplex>(instanceContext, tcp, URL);
            lobbyService = chanFactory.CreateChannel();

            try
            {
                ((ICommunicationObject)lobbyService).Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to server: {ex.Message}");
            }

            PlayBackgroundMusic();
            privateWindows = new Dictionary<string, PrivateChatWindowD>();
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

                    //start polling for updates
                    //StartPolling();

                    try
                    {
                        await Task.Run(() => lobbyService.SubscribeToUpdate(currentUsername));
                    }
                    catch
                    {
                        //
                    }
                    //load available rooms
                    await RefreshRooms();
                }
                else
                {
                    lblLoginStatus.Text = "Username already exists! Please choose a unique username :)";
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
                //StopPolling();

                try
                {
                    await Task.Run(() => lobbyService.UnsubscribeFromUpdate(currentUsername));
                }
                catch
                {
                    //
                }

                if (!string.IsNullOrEmpty(currentUsername))
                {
                    await Task.Run(() => lobbyService.LogoutPlayer(currentUsername));
                }

                //close all private message windows
                foreach (var window in privateWindows.Values)
                {
                    window.Close();
                }
                privateWindows.Clear();

                //reset UI
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
                bool success = await Task.Run(() => lobbyService.CreateRoom(roomName, currentUsername, currentUsername));
                if (success)
                {
                    lblRoomStatus.Text = $"Room '{roomName}' created successfully!";
                    txtNewRoomName.Text = "";
                    await RefreshRooms();

                    currentRoom = roomName;
                    lblCurrentRoom.Text = $"Room: {currentRoom}";

                    MainLobbyPanel.Visibility = Visibility.Collapsed;
                    RoomChatPanel.Visibility = Visibility.Visible;

                    await RefreshRoomData();
                }
                else
                {
                    lblRoomStatus.Text = "Oh no! Room name already exists.";
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

                //clear room data
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
        private void EmojiButton_Click(object sender, RoutedEventArgs e) => EmojiPopup.IsOpen = true;
        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                txtMessageInput.Text += btn.Content.ToString();
                EmojiPopup.IsOpen = false;
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
                        //get a temp file path with the correct extension
                        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));
                        File.WriteAllBytes(tempPath, file.FileContent);

                        //open the file with the default application
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = tempPath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening file: {ex.Message}", "Error");
                }
            }
        }

        private void LstPlayers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstPlayers.SelectedItem is string selectedPlayer && selectedPlayer != currentUsername)
            {
                if (!privateWindows.ContainsKey(selectedPlayer))
                {
                    var privateWindow = new PrivateChatWindowD(currentUsername, selectedPlayer, lobbyService);
                    privateWindow.Closed += (s, args) => privateWindows.Remove(selectedPlayer);
                    privateWindows[selectedPlayer] = privateWindow;
                }

                privateWindows[selectedPlayer].Show();
                privateWindows[selectedPlayer].Activate();
            }
        }

        private void PlayBackgroundMusic()
        {
            backgroundPlayer.Open(new Uri("Resources/bgm.mp3", UriKind.Relative));
            backgroundPlayer.MediaEnded += (s, e) =>
            {
                backgroundPlayer.Position = TimeSpan.Zero;
                backgroundPlayer.Play();
            };
            backgroundPlayer.Play();
        }


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
                    //save the currently selected room's RoomName
                    string selectedRoomName = null;
                    if (lstRooms.SelectedItem is LobbyRoom selectedRoom)
                        selectedRoomName = selectedRoom.RoomName;

                    lstRooms.Items.Clear();
                    foreach (var room in rooms)
                    {
                        lstRooms.Items.Add(room);
                    }

                    //restore selection if possible
                    if (selectedRoomName != null)
                    {
                        foreach (var room in lstRooms.Items)
                        {
                            if (room is LobbyRoom lobbyRoom && lobbyRoom.RoomName == selectedRoomName)
                            {
                                lstRooms.SelectedItem = room;
                                break;
                            }
                        }
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
                    //update messages
                    txtChatMessages.Text = string.Join("\n", messages.Select(m => $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Content}"));

                    //automatically scroll to bottom
                    chatScrollViewer.ScrollToBottom();

                    //update players
                    lstPlayers.Items.Clear();
                    foreach (var player in players)
                    {
                        lstPlayers.Items.Add(player);
                    }

                    //update files
                    lstSharedFiles.Items.Clear();
                    foreach (var file in files)
                    {
                        lstSharedFiles.Items.Add(file);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Polling error: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
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