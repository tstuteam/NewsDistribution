using System;
using System.Windows;
using System.Diagnostics;
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

        _client.OnSubscribeAttempt += status =>
            Trace.WriteLine($"Subscribe attempt: {status}.");

        _client.OnUnsubscribe += () =>
            Trace.WriteLine("Disconnected.");

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