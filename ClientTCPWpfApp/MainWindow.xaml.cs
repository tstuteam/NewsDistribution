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
    private const string Ip = "178.159.53.223";
    private const int Port = 8910;
    private const int BufferSize = 256;
    private IPEndPoint? _iPEndPoint;
    private Socket? _listenSocket;
    private bool _connected;

    public MainWindow()
    {
        InitializeComponent();
    }

    ~MainWindow()
    {
        _listenSocket?.Close();
    }


    private void ConnectButton_onClick(object sender, RoutedEventArgs e)
    {
        void AcceptNews()
        {
            while (_connected)
            {
                var data = new byte[BufferSize];
                var builder = new StringBuilder();
                do
                {
                    var bytes = _listenSocket!.Receive(data, data.Length, 0);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                } while (_listenSocket.Available > 0);

                NewsTextBlock.Text += builder.Append('\n');
            }
        }

        var acceptNewsThread = new Thread(AcceptNews);

        try
        {
            _iPEndPoint = new IPEndPoint(IPAddress.Parse(Ip), Port);

            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Connect(_iPEndPoint);
            _connected = true;

            Subscriber self = new(UserName.Text);

            _listenSocket.Send(Encoding.Unicode.GetBytes($"Subscribe {self}"));

            acceptNewsThread.Start();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.ToString());
        }
    }


    private void DisconnectButton_onClick(object sender, RoutedEventArgs e)
    {
        _listenSocket?.Close();
        _connected = false;
        try
        {
            _iPEndPoint = new IPEndPoint(IPAddress.Parse(Ip), Port);
            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Connect(_iPEndPoint);
            _listenSocket.Send(Encoding.Unicode.GetBytes($"Unsubscribe {UserName.Text}"));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.ToString());
        }

        _listenSocket?.Close();
    }
}