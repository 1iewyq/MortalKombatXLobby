using MKXLobbyContracts;
using MKXLobbyModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MKXLobbyClient
{
    /* Private chat window for one-on-one conversations between two players
       This window handles sending and receiving private messages between specific users
       It uses polling to regularly check for new messages in the conversation */
    public partial class PrivateChatWindow : Window
    {
        //User info for this private chat
        private readonly string fromUser; //the current user (sender)
        private readonly string toUser;   //the other user (receiver)

        //Service proxy to communicate with server
        private readonly ILobbyService lobbyService;
        
        //Timer for refreshing messages regularly
        private Timer refreshTimer;

        //Constructor - creates a new private chat window between two users
        public PrivateChatWindow(string fromUser, string toUser, ILobbyService lobbyService)
        {
            InitializeComponent();

            //store user info and service reference
            this.fromUser = fromUser;
            this.toUser = toUser;
            this.lobbyService = lobbyService;

            //update window title and header with the other user's name
            lblPrivateChatWith.Text = $"Private Chat with {toUser}";
            Title = $"Private Chat - {toUser}";

            //start refreshing messages
            StartMessageRefresh();

            //load existing messages
            _ = RefreshMessages();
        }

        //Handles send button click - sends the private message
        private async void BtnSendPrivateMessage_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        //Handles Enter key press in message input - sends the private message
        private async void TxtPrivateMessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SendMessage();
            }
        }

        //Send a private message to the other user
        private async Task SendMessage()
        {
            string messageContent = txtPrivateMessageInput.Text.Trim();

            //don't send empty messages
            if (string.IsNullOrEmpty(messageContent))
                return;

            try
            {
                //create private message object
                var message = new ChatMessage
                {
                    From = fromUser,            //Current user sending the message
                    To = toUser,                //Other user receiving the message
                    Content = messageContent,   //The actual message text
                    IsPrivate = true,           //Mark as private message
                    Timestamp = DateTime.Now
                };

                //send the message to the server
                await Task.Run(() => lobbyService.SendMessage(message));

                //clear input box
                txtPrivateMessageInput.Text = "";

                //refresh messages immediately
                await RefreshMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error");
            }
        }

        //Starts the background timer to regularly check for new messages
        //This provides near real-time updates for the private conversation
        private void StartMessageRefresh()
        {
            //create a timer that triggers RefreshMessages every second
            refreshTimer = new Timer(async _ => await RefreshMessages(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }


        //Refreshes the conversation by getting all private messages between the two users
        //Filters and displays only messages relevant to this conversation
        private async Task RefreshMessages()
        {
            try
            {
                //Get all private emssages for the current user from the server
                var messages = await Task.Run(() => lobbyService.GetPrivateMessages(fromUser));

                //filter to show only messages between these two specific users
                var relevantMessages = messages
                    .Where(m => (m.From == fromUser && m.To == toUser) ||   //Messages sent by current user to other user
                                (m.From == toUser && m.To == fromUser))     //Messages received from other user
                    .OrderBy(m => m.Timestamp)                              //Sort by time (oldest first)
                    .ToList(); 

                //Update the UI on main thread
                Dispatcher.Invoke(() =>
                {
                    //display messages in chat format with timestamp and sender name
                    txtPrivateChatMessages.Text = string.Join(
                        "\n",
                        relevantMessages.Select(m => $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Content}")
                    );

                    //auto-scroll to bottom
                    scrollPrivateChat.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                //log errors but don't crash the app
                Console.WriteLine($"Private message refresh error: {ex.Message}");
            }
        }

        //Handles close button click - closes the private chat window
        private void BtnClosePrivateChat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //Cleanup when window is closed - stop the refresh timer
        protected override void OnClosed(EventArgs e)
        {
            //stop the background refresh timer to prevent memory leaks
            refreshTimer?.Dispose();
            base.OnClosed(e);
        }
    }
}
