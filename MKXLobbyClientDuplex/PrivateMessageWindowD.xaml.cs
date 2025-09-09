using MKXLobbyContracts;
using MKXLobbyModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MKXLobbyClientDuplex
{
    /* Private chat window for one-on-one conversations between two players
       This window handles sending and receiving private messages between specific users
       The server will directly push updates of the messages to the client by using netTCPBinding*/
    public partial class PrivateChatWindowD : Window
    {
        //User info for this private chat
        private readonly string fromUser;//the current user (sender)
        private readonly string toUser;//the other user (receiver)

        //Service proxy to communicate with server using duplex communication
        private readonly ILobbyServiceDuplex lobbyService;

        //Constructor - creates a new private chat window between two users
        public PrivateChatWindowD(string fromUser, string toUser, ILobbyServiceDuplex lobbyService)
        {
            InitializeComponent();

            //store user info and service reference
            this.fromUser = fromUser;
            this.toUser = toUser;
            this.lobbyService = lobbyService;

            //update window title and header with the other user's name
            lblPrivateChatWith.Text = $"Private Chat with {toUser}";
            Title = $"Private Chat - {toUser}";

            //load existing and history message if any
            _ = LoadInitialMessages();
        }

        //Display chat message that are relevant with both the user in the current window
        //This method is thread-safe and can be called from any thread.
        public void DisplayMessage(ChatMessage message)
        {
            //Check if we are on the UI thread
            //If not, invoke back to the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DisplayMessage(message));
                return;
            }

            //Only display messages that are relavent between fromUser and toUser
            if ((message.From == fromUser && message.To == toUser) || (message.From == toUser && message.To == fromUser))
            {
                txtPrivateChatMessages.Text += $"[{message.Timestamp:HH:mm:ss}] {message.From}: {message.Content}\n";
                //Auto-scroll to the bottem to show the latest message
                scrollPrivateChat.ScrollToEnd();
            }
        }

        //Loads the initial message history between the two users from the server
        private async Task LoadInitialMessages()
        {
            try
            {
                //Retrieve all private messages from the current user
                var messages = await Task.Run(() => lobbyService.GetPrivateMessages(fromUser));

                //Filter messages - Only include messages between the two users in the current private chat 
                var relevantMessages = messages
                    .Where(m => (m.From == fromUser && m.To == toUser) || (m.From == toUser && m.To == fromUser))
                    .OrderBy(m => m.Timestamp).ToList();//Sort by timestamp to maintain chronological order


                //Update the UI on the main thread with the filtered messages
                Dispatcher.Invoke(() =>
                {
                    txtPrivateChatMessages.Text = string.Join("\n", relevantMessages.Select(m => $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Content}"));
                    scrollPrivateChat.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading initial message: {ex.Message}");
            }
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

                //Display chat message
                DisplayMessage(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error");
            }
        }

        //Handles close button click - closes the private chat window
        private void BtnClosePrivateChat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //Cleanup when the window is closed
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
