using Avalonia.Controls;
using Avalonia.Interactivity;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem
{
    public partial class LoginWindow : Window
    {
        private readonly UserRepository _userRepo = new();

        public User? AuthenticatedUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Login_Click(object? sender, RoutedEventArgs e)
        {
            var login = LoginTextBox.Text?.Trim() ?? "";
            var password = PasswordTextBox.Text ?? "";

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                ErrorText.Text = "Vyplňte login i heslo.";
                return;
            }

            var user = _userRepo.Authenticate(login, password);
            if (user is null)
            {
                ErrorText.Text = "Přihlášení se nezdařilo: neplatný login nebo heslo.";
                return;
            }

            AuthenticatedUser = user;
            Close(true);
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}