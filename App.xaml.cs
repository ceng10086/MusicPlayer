using System.Configuration;
using System.Data;
using System.Windows;

namespace MusicPlayer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Handle unhandled exceptions
        this.DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show($"Error: {args.Exception.Message}\n\nStack Trace:\n{args.Exception.StackTrace}", 
                          "Application Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

