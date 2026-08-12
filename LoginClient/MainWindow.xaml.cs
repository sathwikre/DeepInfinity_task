using System.Net.Http;
using System.Windows;
using LoginClient.Models;
using LoginClient.Services;

namespace LoginClient;

public partial class MainWindow : Window
{
    private readonly ApiService _apiService = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        string username = UsernameTextBox.Text.Trim();
        string password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Enter both a username and password.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoginButton.IsEnabled = false;

        try
        {
            LoginResponse result = await _apiService.LoginAsync(new LoginRequest
            {
                Username = username,
                Password = password
            });

            if (result.Success)
            {
                var dashboard = new DashboardWindow();
                dashboard.Show();
                Close();
            }
            else
            {
                MessageBox.Show(result.Message, "Login",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (HttpRequestException)
        {
            MessageBox.Show("Cannot reach the login service. Start LoginFunction first.",
                "Connection error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (TaskCanceledException)
        {
            MessageBox.Show("The login request timed out.", "Connection error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception)
        {
            MessageBox.Show("An unexpected error occurred while logging in.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
