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
    public partial class PrivateChatWindow : Window
    {
        private readonly string fromUser;
        private readonly string toUser;
        private readonly ILobbyService lobbyService;
        private Timer refreshTimer;

        public PrivateChatWindow(string fromUser, string toUser, ILobbyService lobbyService)
        {
            InitializeComponent();

            this.fromUser = fromUser;
            this.toUser = toUser;
            this.lobbyService = lobbyService;

            lblPrivateChatWith.Text = $"Private Chat with {toUser}";
            Title = $"Private Chat - {toUser}";

            //start refreshing messages
            StartMessageRefresh();

            //load existing messages
            _ = RefreshMessages();
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
                await RefreshMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error");
            }
        }

        private void StartMessageRefresh()
        {
            refreshTimer = new Timer(async _ => await RefreshMessages(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private async Task RefreshMessages()
        {
            try
            {
                var messages = await Task.Run(() => lobbyService.GetPrivateMessages(fromUser));

                var relevantMessages = messages
                    .Where(m => (m.From == fromUser && m.To == toUser) || (m.From == toUser && m.To == fromUser))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                Dispatcher.Invoke(() =>
                {
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
                Console.WriteLine($"Private message refresh error: {ex.Message}");
            }
        }

        private void BtnClosePrivateChat_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            refreshTimer?.Dispose();
            base.OnClosed(e);
        }
    }
}
