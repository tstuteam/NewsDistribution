using System.Windows;
using NewsDistribution;
using NewsDistribution.Server;

namespace ServerTCPWpfApp;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int Port = 8910;
    private readonly NewsServer _server = new();

    private News? _lastNews;

    public ObservableHashSet<string> Clients { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _server.OnClientSubscribes += name =>
        {
            Dispatcher.Invoke(() => Clients.Add(name));

            if (_lastNews is not null)
                _server.SendNews(_lastNews, new string[] { name });
        };

        _server.OnClientUnsubscribes += name =>
            Dispatcher.Invoke(() => Clients.Remove(name));
    }

    ~MainWindow()
    {
        _server.Shutdown();
    }

    private void sendButton_Click(object sender, RoutedEventArgs e)
    {
        _lastNews = new News(
            TitleTextBox.Text,
            DescriptionTextBox.Text,
            ContentTextBox.Text
        );

        _server.SendNews(_lastNews, Clients);
    }

    private void enableButton_Click(object sender, RoutedEventArgs e)
    {
        StatusLabel.Content = _server.Start(Port)
            ? "Server started, waiting for connections."
            : "Failed to start the server.";
    }

    private void disableButton_Click(object sender, RoutedEventArgs e)
    {
        _server.Shutdown();
        StatusLabel.Content = "Server stopped.";
    }
}