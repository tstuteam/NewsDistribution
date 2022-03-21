using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace ServerTCPWpfApp;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string Ip = "127.0.0.1";
    private const int Port = 8910;
    private const int MaxConnections = 16;
    private const int BufferSize = 256;
    private IPEndPoint? _iPEndPoint;
    private Socket? _listenSocket;

    public MainWindow()
    {
        InitializeComponent();
    }

    ~MainWindow()
    {
        _listenSocket?.Close();
    }

    private void sendButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void enableButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _iPEndPoint = new IPEndPoint(IPAddress.Parse(Ip), Port);
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            _listenSocket.Bind(_iPEndPoint);
            _listenSocket.Listen(MaxConnections);

            statusLabel.Content = "Server started. Waiting for connections.";

            while (true)
            {
                var handler = _listenSocket.Accept();
                var builder = new StringBuilder();
                var data = new byte[BufferSize];

                do
                {
                    var bytes = handler.Receive(data);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                } while (handler.Available > 0);

                switch (builder.ToString())
                {
                    case "Subscribe":
                        break;
                    case "Unsubscribe":
                        break;
                }
                
                const string message = "Message has been delivered.";
                data = Encoding.Unicode.GetBytes(message);
                handler.Send(data);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
        }
        catch (Exception exception)
        {
            statusLabel.Content = exception.ToString();
            MessageBox.Show(exception.ToString());
        }
    }

    private void disableButton_Click(object sender, RoutedEventArgs e)
    {
        _listenSocket?.Close();
        statusLabel.Content = "The server is stopped.";
    }
}