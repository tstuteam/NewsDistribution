using System.Windows;
using NewsDistribution;

namespace ClientTCPWpfApp;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string Address = "127.0.0.1";
    private const int Port = 8910;
    private readonly NewsClient _client = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    ~MainWindow()
    {
        _client.Disconnect();
    }

    private void ConnectButton_onClick(object sender, RoutedEventArgs e)
    {
        var name = UserName.Text;

        if (name.Length == 0)
            return;

        _client.Connect(name, Address, Port);

        _client.OnNewsReceived += news =>
        {
            Dispatcher.Invoke(() =>
                {
                    var (title, description, content) = news;
                    NewsTextBlock.Text += $"\t{title}\n{description}\n{content}\n\n";
                }
            );
        };
    }

    private void DisconnectButton_onClick(object sender, RoutedEventArgs e)
    {
        _client.Disconnect();
    }
}