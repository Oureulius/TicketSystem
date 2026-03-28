using Avalonia;
using System;
using TicketSystem.Data;

namespace TicketSystem
{
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            DatabaseHelper.Initialize();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}