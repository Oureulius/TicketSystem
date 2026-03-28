using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TicketSystem.Models;

namespace TicketSystem
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var loginWindow = new LoginWindow();
                desktop.MainWindow = loginWindow;

                loginWindow.Closed += (_, _) =>
                {
                    var user = loginWindow.AuthenticatedUser;
                    if (user is null)
                    {
                        desktop.Shutdown();
                        return;
                    }

                    var mainWindow = new MainWindow();
                    mainWindow.InitializeForUser(user);

                    desktop.MainWindow = mainWindow;
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    mainWindow.Show();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}