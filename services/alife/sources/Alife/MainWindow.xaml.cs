using System.Windows;

namespace Alife;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    void MaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaxBtn.Content = "▢";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaxBtn.Content = "❐";
        }
    }

    void CloseClick(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }
}
