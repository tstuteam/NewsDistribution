using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using NewsDistribution;

namespace ClientTCPWpfApp;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string _address = "127.0.0.1";
    private const int _port = 8910;
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
        _client.Connect(UserName.Text, _address, _port);

        _client.OnNewsReceived += (title, description, content) =>
        {
            NewsTextBlock.Text += $"\t{title}\n{description}\n{content}\n\n";
        };
    }

    private void DisconnectButton_onClick(object sender, RoutedEventArgs e)
    {
        _client.Disconnect();
    }
}