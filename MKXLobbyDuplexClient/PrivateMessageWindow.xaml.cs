using MKXLobbyContracts;
using MKXLobbyModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MKXLobbyDuplexClient
{
    public partial class PrivateMessageWindow : Window
    {
        private string fromUser;
        private string toUser;
        private ILobbyService lobbyService;
        private Timer refreshTimer;

        public PrivateMessageWindow(string fromUser, string toUser, ILobbyService lobbyService)
        {
            InitializeComponent();

            this.fromUser = fromUser;
            this.toUser = toUser;
            this.lobbyService = lobbyService;

            lblChatWith.Text = $"Private chat with: {toUser}";
            Title = $"Private Chat - {toUser}";

            // Start refreshing messages
            StartMessageRefresh();
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
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
                txtMessageInput.Text = "";

                // Refresh messages immediately
                await RefreshMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}", "Error");
            }
        }

        private void StartMessageRefresh()
        {
            refreshTimer = new Timer(async _ => await RefreshMessages(),
                                   null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private async Task RefreshMessages()
        {
            try
            {
                var messages = await Task.Run(() => lobbyService.GetPrivateMessages(fromUser));

                // Filter messages between these two users
                var relevantMessages = messages
                    .Where(m => (m.From == fromUser && m.To == toUser) ||
                               (m.From == toUser && m.To == fromUser))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                Dispatcher.Invoke(() =>
                {
                    txtMessages.Text = string.Join("\n", relevantMessages.Select(m =>
                        $"[{m.Timestamp:HH:mm:ss}] {m.From}: {m.Content}"));

                    // Auto-scroll to bottom
                    messagesScrollViewer.ScrollToBottom();
                });
            }
            catch (Exception ex)
            {
                // Handle silently for polling errors
                Console.WriteLine($"Private message refresh error: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            refreshTimer?.Dispose();
            base.OnClosed(e);
        }
    }
}