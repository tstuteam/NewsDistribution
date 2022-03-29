using System.Text;
using System.Net.Sockets;

namespace NewsDistribution;

public class NewsClient
{
    private Socket? _socket;
    private NetworkStream? _stream;
    private BinaryReader? _reader;
    private readonly byte[] _buffer = new byte[2048];
    private Thread? _receiveThread;

    private CancellationTokenSource? _disconnectToken;

    public delegate void NewsReceived(News news);
    public event NewsReceived? OnNewsReceived;

    public bool Connect(string name, string address, ushort port)
    {
        if (name == string.Empty || name.Length > 256)
            throw new ArgumentException("Name is either empty or too long (>256).", nameof(name));

        if (_socket != null)
            return false;

        try
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(address, port);
        }
        catch (SocketException)
        {
            _socket?.Dispose();
            _socket = null;

            return false;
        }

        _disconnectToken = new CancellationTokenSource();

        _stream = new NetworkStream(_socket);
        _reader = new BinaryReader(_stream);

        SendAuthorizationPacket(name);
        bool authorized = ReceiveAuthorizationPacket();

        if (authorized)
        {
            _receiveThread = new Thread(ReceiveThreadProc);
            _receiveThread.Start();
        }
        else
        {
            _stream.Close();
            _socket.Close();
            _stream = null;
            _socket = null;
        }

        return authorized;
    }

    public void Disconnect(bool sendPacket = true)
    {
        if (_socket == null)
            return;

        _disconnectToken.Cancel();

        if (sendPacket)
        {
            _buffer[0] = (byte)PacketType.Unsubscribe;
            _socket.Send(_buffer, 1, SocketFlags.None);
        }

        _socket.Close();
        _stream!.Close();
        _socket = null;
    }

    private async void ReceiveThreadProc()
    {
        if (_socket == null)
            return;

        bool run = true;

        try
        {
            while (run)
            {
                await _socket.ReceiveAsync(_buffer.AsMemory(0, 1), SocketFlags.None, _disconnectToken.Token);

                PacketType packetType = (PacketType)_buffer[0];

                switch (packetType)
                {
                    case PacketType.Unsubscribe:
                        Disconnect(false);
                        run = false;
                        break;

                    case PacketType.News:
                        ReceiveNewsPacket();
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Disconnect(false);
            return;
        }
    }

    private void SendAuthorizationPacket(string name)
    {
        if (_socket == null)
            throw new InvalidOperationException();

        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        int length = nameBytes.Length;

        if (length > 256)
            return;

        _stream!.WriteByte((byte)length);
        _stream.Write(nameBytes);
        _stream.Flush();
    }

    private bool ReceiveAuthorizationPacket()
    {
        if (_socket == null)
            throw new InvalidOperationException();

        _socket.Receive(_buffer, 1, SocketFlags.None);

        return _buffer[0] != 0;
    }

    private void ReceiveNewsPacket()
    {
        if (_socket == null)
            throw new InvalidOperationException();

        string title = _reader!.ReadString();
        string Description = _reader.ReadString();
        string Content = _reader.ReadString();

        OnNewsReceived?.Invoke(new News(title, Description, Content));
    }
}
