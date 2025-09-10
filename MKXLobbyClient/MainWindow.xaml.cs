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


namespace MKXLobbyClient
{
    /* Main window of the client application - handles all user interactions
       This includes login, room management, chatting, and file sharing
       Uses polling to regularly check for updates from the server */
    public partial class MainWindow : Window
    {
        // WCF service proxy to communicate with the lobby server
        private ILobbyService lobbyService;

        //current user info
        private string currentUsername; //username of the logged-in player
        private string currentRoom;     //name of the room the player is currently in (null if in lobby)

        //background polling timer to check for updates from server
        private Timer pollTimer;

        //dictionary to open private message windows (one per user)
        private Dictionary<string, PrivateChatWindow> privateWindows;

        //media player for background music
        private MediaPlayer backgroundPlayer = new MediaPlayer();

        //Constructor - initializes the main window and sets up WCF connection to server
        public MainWindow()
        {
            InitializeComponent();

            //configure NetTcpBinding with the same settings as the server
            var tcp = new NetTcpBinding();
            tcp.MaxBufferSize = 104857600; //100MB
            tcp.MaxReceivedMessageSize = 104857600; //100MB

            //create connection to server
            var URL = "net.tcp://localhost:8100/LobbyService";
            var chanFactory = new ChannelFactory<ILobbyService>(tcp, URL);
            lobbyService = chanFactory.CreateChannel();

            //start background music and initialize private window tracking
            PlayBackgroundMusic();
            privateWindows = new Dictionary<string, PrivateChatWindow>();
        }


        //Handles login button click - attempts to log player into the lobby
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();

            //validate input
            if (string.IsNullOrEmpty(username))
            {
                lblLoginStatus.Text = "Please enter a username.";
                return;
            }

            try
            {
                //attempt login on a background thread to avoid freezing UI
                bool loginSuccess = await Task.Run(() => lobbyService.LoginPlayer(username));

                if (loginSuccess)
                {
                    //login successful - store usernname and update UI
                    currentUsername = username;
                    lblWelcome.Text = $"Welcome, {currentUsername}!";

                    //switch from login screen to main lobby
                    LoginPanel.Visibility = Visibility.Collapsed;
                    MainLobbyPanel.Visibility = Visibility.Visible;

                    //start background polling for real-time updates
                    StartPolling();

                    //load available rooms
                    await RefreshRooms();
                }
                else
                {
                    //login failed - username already taken
                    lblLoginStatus.Text = "Username already exists! Please choose a unique username :)";
                }
            }
            catch (Exception ex)
            {
                //handle connection or server errors
                lblLoginStatus.Text = $"Login failed: {ex.Message}";
            }
        }

        //Handles logout button click - logs player out and returns to login screen
        private async void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //stop background polling
                StopPolling();

                //notify server of logout
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

                //reset application state
                currentUsername = null;
                currentRoom = null;

                //return to login screen
                MainLobbyPanel.Visibility = Visibility.Collapsed;
                RoomChatPanel.Visibility = Visibility.Collapsed;
                LoginPanel.Visibility = Visibility.Visible;

                //clear input fields
                txtUsername.Text = "";
                lblLoginStatus.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Logout error: {ex.Message}", "Error");
            }
        }

        //Handles create room button click - creates a new lobby room
        private async void BtnCreateRoom_Click(object sender, RoutedEventArgs e)
        {
            string roomName = txtNewRoomName.Text.Trim();

            //validate input
            if (string.IsNullOrEmpty(roomName))
            {
                lblRoomStatus.Text = "Please enter a room name.";
                return;
            }

            try
            {
                //attempt to create room on server
                bool success = await Task.Run(() => lobbyService.CreateRoom(roomName, currentUsername, currentUsername));
                if (success)
                {
                    //room created successfully
                    lblRoomStatus.Text = $"Room '{roomName}' created successfully!";
                    txtNewRoomName.Text = ""; //clear input field
                    await RefreshRooms(); //update room list

                    //automatically enter the newly created room
                    currentRoom = roomName;
                    lblCurrentRoom.Text = $"Room: {currentRoom}";

                    //switch from lobby view to room chat view
                    MainLobbyPanel.Visibility = Visibility.Collapsed;
                    RoomChatPanel.Visibility = Visibility.Visible;

                    //load room data (messages, players, files)
                    await RefreshRoomData();
                }
                else
                {
                    //room creation failed - room name already exists
                    lblRoomStatus.Text = "Oh no! Room name already exists.";
                }
            }
            catch (Exception ex)
            {
                lblRoomStatus.Text = $"Error creating room: {ex.Message}";
            }
        }

        //handles join room button click - joins an existing room
        private async void BtnJoinRoom_Click(object sender, RoutedEventArgs e)
        {
            //get the selected room from the list
            if (lstRooms.SelectedItem is LobbyRoom selectedRoom)
            {
                try
                {
                    //attempt to join the selected room
                    bool success = await Task.Run(() => lobbyService.JoinRoom(selectedRoom.RoomName, currentUsername));

                    if (success)
                    {
                        //successfully joined room
                        currentRoom = selectedRoom.RoomName;
                        lblCurrentRoom.Text = $"Room: {currentRoom}";

                        //switch from lobby view to room chat view
                        MainLobbyPanel.Visibility = Visibility.Collapsed;
                        RoomChatPanel.Visibility = Visibility.Visible;

                        //load room data (messages, players, files)
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
                //no room selected
                MessageBox.Show("Please select a room to join.", "No Room Selected");
            }
        }

        //Handles leave room button click - leaves the current room and returns to lobby
        private async void BtnLeaveRoom_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //notify server of leaving the room
                await Task.Run(() => lobbyService.LeaveRoom(currentUsername));

                //reset current room state
                currentRoom = null;

                //switch from room chat view back to main lobby
                RoomChatPanel.Visibility = Visibility.Collapsed;
                MainLobbyPanel.Visibility = Visibility.Visible;

                //clear room data
                txtChatMessages.Text = "";
                lstPlayers.Items.Clear();
                lstSharedFiles.Items.Clear();

                //refresh lobby data
                await RefreshRooms();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error leaving room: {ex.Message}", "Error");
            }
        }

        //Shows emoji picker popup when emoji button is clicked
        private void EmojiButton_Click(object sender, RoutedEventArgs e) => EmojiPopup.IsOpen = true;

        //Handles emoji selection - inserts emoji into message input
        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                txtMessageInput.Text += btn.Content.ToString(); //add emoji to input
                EmojiPopup.IsOpen = false; //close popup
            }
        }

        //Handles send message button click
        private async void BtnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        //Handles Enter key press in message input - sends message
        private async void TxtMessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SendMessage();
            }
        }

        //Sends a public message to the current room
        private async Task SendMessage()
        {
            string messageContent = txtMessageInput.Text.Trim();

            //validate input
            if (string.IsNullOrEmpty(messageContent) || string.IsNullOrEmpty(currentRoom))
                return;

            try
            {
                //create message object
                var message = new ChatMessage
                {
                    From = currentUsername,
                    Content = messageContent,
                    RoomName = currentRoom,
                    IsPrivate = false, //this is a public message
                    Timestamp = DateTime.Now
                };

                //send message to server
                await Task.Run(() => lobbyService.SendMessage(message));
                txtMessageInput.Text = ""; //clear input field
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error");
            }
        }

        //Handles share file button click - opens file dialog to select and share a file
        private async void BtnShareFile_Click(object sender, RoutedEventArgs e)
        {
            //Open file selection dialog with filters for supported file types
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Text files (*.txt)|*.txt|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    //get file info
                    string fileName = Path.GetFileName(openFileDialog.FileName);
                    byte[] fileContent = File.ReadAllBytes(openFileDialog.FileName);
                    string fileExtension = Path.GetExtension(openFileDialog.FileName).ToLower();

                    //determine file type for proper handling
                    string fileType = "other";
                    if (fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || fileExtension == ".bmp")
                        fileType = "image";
                    else if (fileExtension == ".txt")
                        fileType = "text";

                    //create shared file object
                    var sharedFile = new SharedFile
                    {
                        FileName = fileName,
                        FileContent = fileContent, //binary data of the file
                        SharedBy = currentUsername,
                        RoomName = currentRoom,
                        FileType = fileType,
                        SharedTime = DateTime.Now
                    };

                    //upload file to server
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

        //handles double-click on a shared file to download and open it
        private async void LstSharedFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstSharedFiles.SelectedItem is SharedFile selectedFile)
            {
                try
                {
                    //download file from server
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

        //Handles double-click on player name - opens private message window
        private void LstPlayers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstPlayers.SelectedItem is string selectedPlayer && selectedPlayer != currentUsername)
            {
                //check if private chat window already exists
                if (!privateWindows.ContainsKey(selectedPlayer))
                {
                    //create new private chat window
                    var privateWindow = new PrivateChatWindow(currentUsername, selectedPlayer, lobbyService);
                    privateWindow.Closed += (s, args) => privateWindows.Remove(selectedPlayer);
                    privateWindows[selectedPlayer] = privateWindow;
                }

                //show and bring to front the private chat window
                privateWindows[selectedPlayer].Show();
                privateWindows[selectedPlayer].Activate();
            }
        }

        //Starts playing background music in a loop
        private void PlayBackgroundMusic()
        {
            backgroundPlayer.Open(new Uri("Resources/bgm.mp3", UriKind.Relative));
            backgroundPlayer.MediaEnded += (s, e) =>
            {
                //Loop the music
                backgroundPlayer.Position = TimeSpan.Zero;
                backgroundPlayer.Play();
            };
            backgroundPlayer.Play();
        }

        //Handles refresh rooms button click - manually updates room list
        private async void BtnRefreshRooms_Click(object sender, RoutedEventArgs e)
        {
            await RefreshRooms();
        }

        //Handles refresh players button click - manually updates room data
        private async void BtnRefreshPlayers_Click(object sender, RoutedEventArgs e)
        {
            await RefreshRoomData();
        }

        //Refreshes the list of available rooms from the server
        //Maintains the user's current selection if possible
        private async Task RefreshRooms()
        {
            try
            {
                //get updated room lsit from server
                var rooms = await Task.Run(() => lobbyService.GetAvailableRooms());

                //update UI on the main thread
                Dispatcher.Invoke(() =>
                {
                    //save the currently selected room's RoomName
                    string selectedRoomName = null;
                    if (lstRooms.SelectedItem is LobbyRoom selectedRoom)
                        selectedRoomName = selectedRoom.RoomName;

                    //clear and repopulate the room list
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

        //Refreshes all data for the current room (messages, players, files)
        //This method is called regularly by the polling timer for real-time updates

        private async Task RefreshRoomData()
        {
            //only refresh if player is actually in a room
            if (string.IsNullOrEmpty(currentRoom))
                return;

            try
            {
                //get all room data from server in parallel
                var messages = await Task.Run(() => lobbyService.GetRoomMessages(currentRoom));
                var players = await Task.Run(() => lobbyService.GetPlayersInRoom(currentRoom));
                var files = await Task.Run(() => lobbyService.GetSharedFiles(currentRoom));

                //update UI on main thread
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
                //log polling errors but do not disrupt the user experience
                Console.WriteLine($"Polling error: {ex.Message}");
            }
        }

        //Keep track of previously seen private messages to detect new ones
        private List<ChatMessage> lastPrivateMessages = new List<ChatMessage>();

        //Starts the background polling timer for real-time updates
        //This simulates near real-time communication by regularly checking for updates
        private void StartPolling()
        {
            pollTimer = new Timer(async _ =>
            {
                //refresh room data if in a room, otherwise refresh room list
                if (!string.IsNullOrEmpty(currentRoom))
                {
                    await RefreshRoomData();
                }
                else
                {
                    await RefreshRooms();
                }

                //poll for new private messages
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    var privateMessages = await Task.Run(() => lobbyService.GetPrivateMessages(currentUsername));
                    foreach (var msg in privateMessages)
                    {
                        //only show new messages
                        if (!lastPrivateMessages.Any(m => m.Timestamp == msg.Timestamp && m.From == msg.From && m.Content == msg.Content))
                        {
                            //open private chat window if not already open
                            if (!privateWindows.ContainsKey(msg.From) && msg.From != currentUsername)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    var privateWindow = new PrivateChatWindow(currentUsername, msg.From, lobbyService);
                                    privateWindow.Closed += (s, args) => privateWindows.Remove(msg.From);
                                    privateWindows[msg.From] = privateWindow;
                                    privateWindow.Show();
                                    privateWindow.Activate(); //bring to front
                                });
                            }
                        }
                    }
                    lastPrivateMessages = privateMessages; //update last seen messages
                }
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1)); //poll every second
        }

        //Stops the background polling timer
        private void StopPolling()
        {
            pollTimer?.Dispose();
            pollTimer = null;
        }

        //Cleanup when window is closed - logout player and close private windows
        protected override void OnClosed(EventArgs e)
        {
            StopPolling();

            try
            {
                //logout player from server
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    lobbyService.LogoutPlayer(currentUsername);
                }
            }
            catch { } //Ignore errors during cleanup

            //Close all private message windows
            foreach (var window in privateWindows.Values)
            {
                window.Close();
            }

            base.OnClosed(e);
        }
    }
}