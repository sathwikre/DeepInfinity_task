using System.IO;
using System.Windows;
using System.Windows.Controls;
using LoginClient.Services;
using Microsoft.Win32;

namespace LoginClient.Pages;

public partial class AudioTranscriptionPage : Page
{
    private readonly DashboardWindow _owner;
    private readonly ApiService _apiService;
    private string? _selectedAudioPath;

    public AudioTranscriptionPage(DashboardWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        _apiService = owner.ApiService;
    }

    private void SelectAudioButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select an audio file",
            Filter = "Supported audio|*.wav;*.mp3",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedAudioPath = dialog.FileName;
            SelectedAudioTextBlock.Text = Path.GetFileName(_selectedAudioPath);
            TranscriptTextBox.Clear();
            StatusTextBlock.Text = string.Empty;
        }
    }

    private async void TranscribeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedAudioPath))
        {
            SetStatus("Select a supported audio file before uploading.");
            return;
        }

        string extension = Path.GetExtension(_selectedAudioPath).ToLowerInvariant();
        if (extension is not ".wav" and not ".mp3")
        {
            SetStatus("Unsupported audio file type. Use .wav or .mp3.");
            return;
        }

        var fileInfo = new FileInfo(_selectedAudioPath);
        if (fileInfo.Length == 0)
        {
            SetStatus("The selected audio file is empty.");
            return;
        }

        if (fileInfo.Length > 50_000_000)
        {
            SetStatus("The selected audio file is too large. Choose a smaller file.");
            return;
        }

        SetBusy(true);
        TranscriptTextBox.Clear();
        StatusTextBlock.Text = string.Empty;

        try
        {
            string fileName = Path.GetFileName(_selectedAudioPath);
            string contentType = GetContentType(extension);

            await using var fileStream = File.OpenRead(_selectedAudioPath);
            var result = await _apiService.TranscribeAudioAsync(fileStream, fileName, contentType);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Transcript))
            {
                TranscriptTextBox.Text = result.Transcript;
                SetStatus("Audio transcription completed successfully.", isError: false);
            }
            else
            {
                SetStatus(string.IsNullOrWhiteSpace(result.Message)
                    ? "The audio could not be transcribed."
                    : result.Message);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"An unexpected transcription error occurred: {ex.Message}");
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
        SelectAudioButton.IsEnabled = !isBusy;
        TranscribeButton.IsEnabled = !isBusy;
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
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream"
        };
}
