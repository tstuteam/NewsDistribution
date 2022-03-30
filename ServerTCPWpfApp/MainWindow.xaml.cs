using System.Windows;
using NewsDistribution;

namespace ServerTCPWpfApp;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int Port = 8910;
    private readonly NewsServer _server = new();

    public MainWindow()
    {
        InitializeComponent();

        _server.OnClientAuthenticated += _ => UpdateClientList();
        _server.OnClientUnsubscribed += _ => UpdateClientList();
    }

    ~MainWindow()
    {
        _server.Shutdown();
    }

    private void UpdateClientList()
    {
        Dispatcher.Invoke(() =>
            ClientsListBox.ItemsSource = _server.Clients
        );
    }

    private void sendButton_Click(object sender, RoutedEventArgs e)
    {
        _server.SendNews(new News(
            TitleTextBox.Text,
            DescriptionTextBox.Text,
            ContentTextBox.Text
        ));
    }

    private void enableButton_Click(object sender, RoutedEventArgs e)
    {
        StatusLabel.Content = _server.Start(Port) ? "Server started, waiting for connections." : "Failed to start the server.";
    }

    private void disableButton_Click(object sender, RoutedEventArgs e)
    {
        _server.Shutdown();
        StatusLabel.Content = "Server stopped.";
    }
}