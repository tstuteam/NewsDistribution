using System.Windows;
using NewsDistribution;
using NewsDistribution.Client;

namespace ClientTCPWpfApp;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int Port = 8910;
    private readonly NewsClient _client = new();

    public MainWindow()
    {
        InitializeComponent();

        _client.OnNewsReceived += news =>
        {
            Dispatcher.Invoke(() =>
            {
                var (title, description, content) = news;
                NewsTextBlock.Text += $"\t{title}\n{description}\n{content}\n\n";
            });
        };
    }

    ~MainWindow()
    {
        _client.Unsubscribe();
    }

    private void ConnectButton_onClick(object sender, RoutedEventArgs e)
    {
        var name = UserName.Text;

        if (name.Length == 0)
            return;

        _client.Subscribe(Address.Text, Port, name);
    }

    private void DisconnectButton_onClick(object sender, RoutedEventArgs e)
    {
        _client.Unsubscribe();
    }
}