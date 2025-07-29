using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wpf.Ui.Controls;
using MusicPlayer.ViewModels;
using MusicPlayer.Models;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MusicPlayer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private MainViewModel? _viewModel;

    // Windows API constants for window operations
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;
    
    private DateTime _lastClickTime = DateTime.MinValue;
    
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        // Subscribe to property changes to handle lyric scrolling
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        // Setup window state change handling
        this.StateChanged += MainWindow_StateChanged;
        
        // Setup drag functionality for the title bar
        DragArea.MouseLeftButtonDown += DragArea_MouseLeftButtonDown;
        
        // Update maximize button icon based on current window state
        UpdateMaximizeButtonIcon();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeButtonIcon();
    }

    private void UpdateMaximizeButtonIcon()
    {
        if (MaximizeButtonText != null)
        {
            MaximizeButtonText.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        
        // Check for double click (within 500ms)
        if ((now - _lastClickTime).TotalMilliseconds < 500)
        {
            ToggleMaximize();
            _lastClickTime = DateTime.MinValue; // Reset to prevent triple-click
            return;
        }
        
        _lastClickTime = now;
        
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            ReleaseCapture();
            SendMessage(new WindowInteropHelper(this).Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentLyricLine) && _viewModel?.CurrentLyricLine != null)
        {
            // Scroll to the current lyric line and center it
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // First scroll to the item
                    LyricsListBox.ScrollIntoView(_viewModel.CurrentLyricLine);
                    
                    // Then try to center it by getting the scroll viewer
                    var scrollViewer = GetScrollViewer(LyricsListBox);
                    if (scrollViewer != null)
                    {
                        var container = LyricsListBox.ItemContainerGenerator.ContainerFromItem(_viewModel.CurrentLyricLine) as ListBoxItem;
                        if (container != null)
                        {
                            var transform = container.TransformToAncestor(scrollViewer);
                            var itemPosition = transform.Transform(new Point(0, 0));
                            
                            // Calculate position to center the item
                            var centerOffset = (scrollViewer.ViewportHeight / 2) - (container.ActualHeight / 2);
                            var targetOffset = scrollViewer.VerticalOffset + itemPosition.Y - centerOffset;
                            
                            scrollViewer.ScrollToVerticalOffset(Math.Max(0, targetOffset));
                        }
                    }
                }
                catch
                {
                    // Ignore any scrolling errors
                }
            }));
        }
    }

    private void PlaylistSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is Models.Song selectedSong && _viewModel != null)
        {
            _viewModel.CurrentSong = selectedSong;
        }
    }

    private ScrollViewer? GetScrollViewer(DependencyObject o)
    {
        if (o is ScrollViewer)
            return (ScrollViewer)o;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
        {
            var child = VisualTreeHelper.GetChild(o, i);
            var result = GetScrollViewer(child);
            if (result != null)
                return result;
        }
        return null;
    }
}