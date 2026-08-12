using System.Windows;
using System.Windows.Controls;

namespace LoginClient.Pages;

public partial class DashboardPage : Page
{
    private readonly DashboardWindow _owner;

    public DashboardPage(DashboardWindow owner)
    {
        InitializeComponent();
        _owner = owner;
    }

    private void ReadFileButton_Click(object sender, RoutedEventArgs e)
    {
        _owner.Navigate(new FileReaderPage(_owner));
    }

    private void AudioTranscriptionButton_Click(object sender, RoutedEventArgs e)
    {
        _owner.Navigate(new AudioTranscriptionPage(_owner));
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var loginWindow = new MainWindow();
        loginWindow.Show();
        _owner.Close();
    }
}
