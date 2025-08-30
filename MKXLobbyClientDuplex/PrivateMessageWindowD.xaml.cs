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
    public partial class PrivateChatWindowD : Window
    {
        private readonly string fromUser;
        private readonly string toUser;
        private readonly ILobbyServiceDuplex lobbyService;
        // private Timer refreshTimer;

        public PrivateChatWindowD(string fromUser, string toUser, ILobbyServiceDuplex lobbyService)
        {
            InitializeComponent();

            this.fromUser = fromUser;
            this.toUser = toUser;
            this.lobbyService = lobbyService;

            lblPrivateChatWith.Text = $"Private Chat with {toUser}";
            Title = $"Private Chat - {toUser}";

            //start refreshing messages
            //StartMessageRefresh();

            //load existing messages
            //_ = RefreshMessages();

            _ = LoadInitialMessages();
        }

        public void DisplayMessage(ChatMessage message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => DisplayMessage(message));
                return;
            }

            if ((message.From == fromUser && message.To == toUser) || (message.From == toUser && message.To == fromUser))
            {
                txtPrivateChatMessages.Text += $"[{message.Timestamp:HH:mm:ss}] {message.From}: {message.Content}\n";
                scrollPrivateChat.ScrollToEnd();
            }
        }

        private async Task LoadInitialMessages()
        {
            try
            {
                var messages = await Task.Run(() => lobbyService.GetPrivateMessages(fromUser));

                var relevantMessages = messages
                    .Where(m => (m.From == fromUser && m.To == toUser) || (m.From == toUser && m.To == fromUser))
                    .OrderBy(m => m.Timestamp).ToList();

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

        private async void BtnSendPrivateMessage_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void TxtPrivateMessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            string messageContent = txtPrivateMessageInput.Text.Trim();

            if (string.IsNullOrEmpty(messageContent))
                return;

            try
            {
                var message = new ChatMessage
                {
                    From = fromUser,
                    To = toUser,
                    Content = messageContent,
                    IsPrivate = true,
                    Timestamp = DateTime.Now
                };

                await Task.Run(() => lobbyService.SendMessage(message));
                txtPrivateMessageInput.Text = "";

                //refresh messages immediately
                //await RefreshMessages();

                DisplayMessage(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error");
            }
        }

        private void BtnClosePrivateChat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            //refreshTimer?.Dispose();
            base.OnClosed(e);
        }
    }
}
