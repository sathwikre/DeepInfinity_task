using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LoginClient.Services;
using Microsoft.Win32;

namespace LoginClient.Pages;

public partial class FileReaderPage : Page
{
    private readonly DashboardWindow _owner;
    private readonly ApiService _apiService;
    private string? _selectedFilePath;

    public FileReaderPage(DashboardWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        _apiService = owner.ApiService;
    }

    private void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a file or image",
            Filter = "Supported files|*.txt;*.pdf;*.jpg;*.jpeg;*.png",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedFilePath = dialog.FileName;
            SelectedFileTextBlock.Text = Path.GetFileName(_selectedFilePath);
            OutputTextBox.Clear();
            StatusTextBlock.Text = string.Empty;
        }
    }

    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            SetStatus("Select a supported file before uploading.");
            return;
        }

        string extension = Path.GetExtension(_selectedFilePath).ToLowerInvariant();
        if (extension is not ".txt" and not ".pdf" and not ".jpg" and not ".jpeg" and not ".png")
        {
            SetStatus("Unsupported file type. Use .txt, .pdf, .jpg, .jpeg or .png.");
            return;
        }

        var fileInfo = new FileInfo(_selectedFilePath);
        if (fileInfo.Length == 0)
        {
            SetStatus("The selected file is empty.");
            return;
        }

        if (fileInfo.Length > 25_000_000)
        {
            SetStatus("The selected file is too large. Choose a smaller file.");
            return;
        }

        SetBusy(true);
        OutputTextBox.Clear();
        StatusTextBlock.Text = string.Empty;

        try
        {
            await using var fileStream = File.OpenRead(_selectedFilePath);
            var response = await _apiService.ReadFileAsync(fileStream, Path.GetFileName(_selectedFilePath), GetContentType(extension));

            if (response.Success)
            {
                OutputTextBox.Text = response.Content;
                SetStatus("File processed successfully.", isError: false);
            }
            else
            {
                SetStatus(response.Message);
            }
        }
        catch (HttpRequestException)
        {
            SetStatus("Cannot reach the file reader service. Start LoginFunction first.");
        }
        catch (TaskCanceledException)
        {
            SetStatus("The read request timed out.");
        }
        catch (Exception ex)
        {
            SetStatus($"An unexpected error occurred: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        _owner.NavigateBack();
    }

    private void SetBusy(bool isBusy)
    {
        SelectFileButton.IsEnabled = !isBusy;
        UploadButton.IsEnabled = !isBusy;
        BackButton.IsEnabled = !isBusy;
    }

    private void SetStatus(string message, bool isError = true)
    {
        StatusTextBlock.Foreground = isError ? System.Windows.Media.Brushes.DarkRed : System.Windows.Media.Brushes.DarkGreen;
        StatusTextBlock.Text = message;
    }

    private static string GetContentType(string extension)
        => extension switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
}
