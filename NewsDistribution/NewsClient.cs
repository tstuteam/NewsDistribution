using System.Net.Sockets;
using System.Text;

namespace NewsDistribution;

/// <summary>
///     TCP news client implementation.
/// </summary>
public class NewsClient
{
    /// <summary>
    ///     Delegate for OnNewsReceived.
    /// </summary>
    /// <param name="news">Received news.</param>
    public delegate void NewsReceived(News news);

    /// <summary>
    ///     Read-write buffer.
    /// </summary>
    private readonly byte[] _buffer = new byte[2048];


    /// <summary>
    ///     Thread cancellation token source.
    /// </summary>
    private CancellationTokenSource? _disconnectToken;

    /// <summary>
    ///     Connection socket stream reader.
    /// </summary>
    private BinaryReader? _reader;

    /// <summary>
    ///     Thread for processing incoming packets.
    /// </summary>
    private Thread? _receiveThread;

    /// <summary>
    ///     Connection socket.
    /// </summary>
    private Socket? _socket;

    /// <summary>
    ///     Connection socket stream.
    /// </summary>
    private NetworkStream? _stream;

    /// <summary>
    ///     Invoked when the server sends news.
    /// </summary>
    public event NewsReceived? OnNewsReceived;

    /// <summary>
    ///     Attempts to connect to a server.
    /// </summary>
    /// <param name="name">Client name.</param>
    /// <param name="address">Server address.</param>
    /// <param name="port">Server port.</param>
    /// <returns><c>true</c> if connected.</returns>
    /// <exception cref="ArgumentException">Name is either empty or too long (>256).</exception>
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

        SendAuthenticationPacket(name);
        var authenticated = ReceiveAuthenticationPacket();

        if (authenticated)
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

        return authenticated;
    }

    /// <summary>
    ///     Disconnects the client from the server.
    /// </summary>
    /// <param name="sendPacket">Should the client send the <c>Unsubscribe</c> packet to the server?</param>
    public void Disconnect(bool sendPacket = true)
    {
        if (_socket == null)
            return;

        _disconnectToken?.Cancel();

        if (sendPacket)
        {
            _buffer[0] = (byte) PacketType.Unsubscribe;
            _socket.Send(_buffer, 1, SocketFlags.None);
        }

        _socket.Close();
        _stream!.Close();
        _socket = null;
    }

    /// <summary>
    ///     Processes received packets.
    /// </summary>
    private async void ReceiveThreadProc()
    {
        if (_socket == null || _disconnectToken == null)
            return;

        var run = true;

        try
        {
            while (run)
            {
                await _socket.ReceiveAsync(_buffer.AsMemory(0, 1), SocketFlags.None, _disconnectToken.Token);

                var packetType = (PacketType) _buffer[0];
                
                if (packetType == PacketType.Unsubscribe)
                {
                    Disconnect(false);
                    run = false;
                }
                else if (packetType == PacketType.News)
                {
                    ReceiveNewsPacket();
                }
            }
        }
        catch (OperationCanceledException)
        {
            Disconnect(false);
        }
    }

    /// <summary>
    ///     Sends the authentication packet to the server.
    /// </summary>
    /// <param name="name">Client name.</param>
    /// <exception cref="InvalidOperationException">Client is not connected.</exception>
    private void SendAuthenticationPacket(string name)
    {
        if (_socket == null)
            throw new InvalidOperationException();

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var length = nameBytes.Length;

        if (length > 256)
            return;

        _stream!.WriteByte((byte) length);
        _stream.Write(nameBytes);
        _stream.Flush();
    }

    /// <summary>
    ///     Receives the authentication packet from the server.
    /// </summary>
    /// <returns><c>true</c> if authenticated.</returns>
    /// <exception cref="InvalidOperationException">Client is not connected.</exception>
    private bool ReceiveAuthenticationPacket()
    {
        if (_socket == null)
            throw new InvalidOperationException();

        _socket.Receive(_buffer, 1, SocketFlags.None);

        return _buffer[0] != 0;
    }

    /// <summary>
    ///     Received the news packet from the server.
    /// </summary>
    /// <exception cref="InvalidOperationException">Client is not connected.</exception>
    private void ReceiveNewsPacket()
    {
        if (_socket == null)
            throw new InvalidOperationException();

        var title = _reader!.ReadString();
        var description = _reader.ReadString();
        var content = _reader.ReadString();

        OnNewsReceived?.Invoke(new News(title, description, content));
    }
}