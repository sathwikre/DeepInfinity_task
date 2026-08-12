using System.Windows;
using System.Windows.Controls;
using LoginClient.Services;

namespace LoginClient;

public partial class DashboardWindow : Window
{
    public ApiService ApiService { get; } = new();

    public DashboardWindow()
    {
        InitializeComponent();
        MainFrame.Navigate(new Pages.DashboardPage(this));
    }

    public void Navigate(Page page)
    {
        MainFrame.Navigate(page);
    }

    public void NavigateBack()
    {
        if (MainFrame.CanGoBack)
        {
            MainFrame.GoBack();
        }
    }
}
