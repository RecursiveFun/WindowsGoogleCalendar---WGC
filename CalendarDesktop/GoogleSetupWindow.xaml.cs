using System.Diagnostics;
using System.Windows;
using CalendarDesktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CalendarDesktop;

public partial class GoogleSetupWindow : Window
{
    public GoogleSetupWindow()
    {
        InitializeComponent();
        var current = GoogleConfigStore.Load();
        ClientIdBox.Text = current.ClientId;
        ClientSecretBox.Text = current.ClientSecret;
    }

    private void OpenConsole_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://console.cloud.google.com/apis/credentials?project=446930592064",
            UseShellExecute = true
        });
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ClientIdBox.Text) || string.IsNullOrWhiteSpace(ClientSecretBox.Text))
        {
            MessageBox.Show(this, "Client ID and Client Secret are required.", "Google setup",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        GoogleConfigStore.Save(ClientIdBox.Text, ClientSecretBox.Text);

        // Update live options used by DI
        var options = App.Services.GetRequiredService<IOptions<GoogleCalendarOptions>>();
        options.Value.ClientId = ClientIdBox.Text.Trim();
        options.Value.ClientSecret = ClientSecretBox.Text.Trim();

        MessageBox.Show(this,
            "Saved.\n\nImportant: the Client ID must be from an OAuth client of type \"Desktop app\".\nWeb application clients will keep showing \"request is invalid\".",
            "Google setup",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
