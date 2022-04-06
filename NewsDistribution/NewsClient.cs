using System.Net.Sockets;
using System.Text;
using NewsDistribution.Shared;

namespace NewsDistribution.Client;


public enum ConnectStatus
{
    Success,
    AlreadyConnected,
    UnableToConnect,
    Rejected
}


/// <summary>
///     TCP news client implementation.
/// </summary>
public class NewsClient
{
    /// <summary>
    ///     Size of the receive buffer.
    /// </summary>
    private const int BufferSize = 1024;

    /// <summary>
    ///     Size of the packet header.
    /// </summary>
    private const int PacketHeaderSize = sizeof(byte) + sizeof(int);


    /// <summary>
    ///     TCP connection client.
    /// </summary>
    private TcpClient? _tcpClient;


    /// <summary>
    ///     Network stream for <see cref="_tcpClient"/>.
    /// </summary>
    private NetworkStream? _stream;

    /// <summary>
    ///     Packet reader.
    /// </summary>
    private PacketReader? _reader;

    /// <summary>
    ///     Packet writer.
    /// </summary>
    private PacketWriter? _writer;

    /// <summary>
    ///     Received data.
    /// </summary>
    private MemoryStream? _data;


    /// <summary>
    ///     Receive buffer.
    /// </summary>
    private readonly byte[] _packetBuffer = new byte[BufferSize];

    /// <summary>
    ///     Was the packet header fully read?
    /// </summary>
    private bool _packetHeaderRead;

    /// <summary>
    ///     Packet type of the last packet.
    /// </summary>
    private PacketType _packetType;

    /// <summary>
    ///     Packet data size of the last packet.
    /// </summary>
    private int _packetSize;


    /// <summary>
    ///     Is the client subscribed?
    /// </summary>
    private bool _subscribed = false;


    /// <summary>
    ///     Delegate for <see cref="OnSubscribeAttempt"/>.
    /// </summary>
    /// <param name="status">Connection status.</param>
    public delegate void SubscribeAttempt(ConnectStatus status);

    /// <summary>
    ///     Delegate for <see cref="OnDisconnect"/>.
    /// </summary>
    public delegate void DisconnectEvent();

    /// <summary>
    ///     Delegate for <see cref="OnDisconnect"/>.
    /// </summary>
    /// <param name="news">News.</param>
    public delegate void NewsReceived(News news);


    /// <summary>
    ///     Invoked on a subscribe attempt.
    /// </summary>
    public event SubscribeAttempt? OnSubscribeAttempt;

    /// <summary>
    ///     Invoked on disconnect.
    /// </summary>
    public event DisconnectEvent? OnDisconnect;

    /// <summary>
    ///     Invoked when receiving news.
    /// </summary>
    public event NewsReceived? OnNewsReceived;


    /// <summary>
    ///     Subscribes to the news server.
    /// </summary>
    /// <param name="address">Server address.</param>
    /// <param name="port">Server port.</param>
    /// <param name="name">Client's name.</param>
    public void Subscribe(string address, ushort port, string name)
    {
        if (_tcpClient != null)
        {
            OnSubscribeAttempt?.Invoke(ConnectStatus.AlreadyConnected);
            return;
        }

        _tcpClient = new TcpClient();

        try
        {
            _tcpClient.BeginConnect(address, port, new AsyncCallback(DoBeginConnect), name);
        }
        catch (SocketException)
        {
            _tcpClient.Close();
            _tcpClient = null;

            OnSubscribeAttempt?.Invoke(ConnectStatus.UnableToConnect);
            return;
        }
    }


    /// <summary>
    ///     Unsubscribes from the news server.
    /// </summary>
    /// <param name="sendPacket">Should the server be informed?</param>
    /// <exception cref="InvalidOperationException">The client is not connected to a server.</exception>
    public void Unsubscribe(bool sendPacket = true)
    {
        if (_tcpClient is null)
            return;

        lock (_tcpClient)
        {
            if (!_subscribed)
                return;

            if (sendPacket)
                SendUnsubscribePacket();

            _tcpClient?.Close();
            _reader?.Close();
            _writer?.Close();
            _data?.Close();

            _tcpClient = null;
            _stream = null;
            _reader = null;
            _writer = null;
            _data = null;

            _subscribed = false;

            OnDisconnect?.Invoke();
        }
    }


    /// <summary>
    ///     Attempts to connect to the server.
    /// </summary>
    /// <param name="asyncResult">Async result.</param>
    private void DoBeginConnect(IAsyncResult asyncResult)
    {
        try
        {
            _tcpClient!.EndConnect(asyncResult);
        }
        catch (SocketException)
        {
            _tcpClient?.Close();
            _tcpClient = null;
            OnSubscribeAttempt?.Invoke(ConnectStatus.UnableToConnect);
            return;
        }

        _stream = _tcpClient.GetStream();
        _data = new MemoryStream();
        _reader = new PacketReader(_data);
        _writer = new PacketWriter();

        _packetHeaderRead = false;

        _stream!.BeginRead(_packetBuffer, 0, BufferSize, new AsyncCallback(DoBeginRead), null);

        SendSubscribePacket((string)asyncResult.AsyncState!);

        Console.WriteLine("Connected");
    }


    /// <summary>
    ///     Reads incoming data.
    /// </summary>
    /// <param name="asyncResult">Async result.</param>
    private void DoBeginRead(IAsyncResult asyncResult)
    {
        int bytesRead;

        try
        {
            if (_stream is null)
            {
                Unsubscribe(false);
                return;
            }

            bytesRead = _stream!.EndRead(asyncResult);
        }
        catch (Exception ex)
        when (ex is IOException || ex is ObjectDisposedException)
        {
            Unsubscribe(false);
            return;
        }

        var dataOffset = 0;

        if (bytesRead <= 0)
        {
            // The remote host shut down the socket connection.
            Unsubscribe(false);
            return;
        }

        while (bytesRead > 0)
        {
            if (!_packetHeaderRead)
            {
                var needToRead = Math.Min(bytesRead, PacketHeaderSize - (int)_data!.Length);

                _data!.Write(_packetBuffer, 0, needToRead);

                dataOffset += needToRead;
                bytesRead -= needToRead;

                if (_data.Length == PacketHeaderSize)
                {
                    _packetHeaderRead = true;
                    _data.Position = 0;

                    _packetType = (PacketType)_reader!.ReadByte();
                    _packetSize = _reader.ReadInt32();

                    _data.Position = 0;
                    _data.SetLength(0);
                }
                else
                {
                    _stream.BeginRead(_packetBuffer, 0, BufferSize, new AsyncCallback(DoBeginRead), null);
                    return;
                }
            }

            if (_data!.Length + bytesRead >= _packetSize)
            {
                // Happens for empty packets like Unsubscribe.
                if (bytesRead != 0)
                {
                    var needToRead = _packetSize - (int)_data.Length;
                    _data!.Write(_packetBuffer, dataOffset, needToRead);

                    dataOffset += needToRead;
                    bytesRead -= needToRead;
                }

                _data.Position = 0;

                ProcessIncomingPacket();

                if (!_subscribed)
                    return;

                _data.Position = 0;
                _data.SetLength(0);

                _packetHeaderRead = false;

                if (bytesRead == 0)
                    _stream.BeginRead(_packetBuffer, 0, BufferSize, new AsyncCallback(DoBeginRead), null);
            }
            else
            {
                _data.Write(_packetBuffer, dataOffset, bytesRead);
                _stream.BeginRead(_packetBuffer, 0, BufferSize, new AsyncCallback(DoBeginRead), null);
                return;
            }
        }
    }


    /// <summary>
    ///     Writes outcoming data.
    /// </summary>
    /// <param name="asyncResult">Async result.</param>
    private void DoBeginWrite(IAsyncResult asyncResult)
    {
        try
        {
            _stream!.EndWrite(asyncResult);
        }
        catch (IOException)
        {
            Unsubscribe(false);
        }
    }


    /// <summary>
    ///     Processess the last incoming packet.
    /// </summary>
    private void ProcessIncomingPacket()
    {
        Console.WriteLine("Received packet {0}: {1} bytes", _packetType, _packetSize);

        try
        {
            if (!_subscribed)
            {
                if (_packetType != PacketType.Subscribe)
                    return;

                _subscribed = _reader!.ReadBoolean();
                OnSubscribeAttempt?.Invoke(_subscribed ? ConnectStatus.Success : ConnectStatus.Rejected);

                if (!_subscribed)
                    Unsubscribe(false);

                return;
            }

            switch (_packetType)
            {
                case PacketType.Unsubscribe:
                    Unsubscribe(false);
                    break;

                case PacketType.News:
                    ReceiveNews();
                    break;
            }
        }
        catch (Exception ex)
        when (ex is EndOfStreamException || ex is DecoderFallbackException)
        {
            Console.WriteLine("Failed to read packet.\n{0}\n", ex.Message);
        }
    }


    /// <summary>
    ///     Sends the <see cref="PacketType.Subscribe"/> packet.
    /// </summary>
    /// <param name="name">Client's name.</param>
    /// <exception cref="InvalidOperationException">Client is not connected.</exception>
    private void SendSubscribePacket(string name)
    {
        if (_tcpClient == null)
            throw new InvalidOperationException();

        _writer!.Start(PacketType.Subscribe);
        _writer.Write(name);

        var data = _writer.End();

        _stream!.BeginWrite(data, 0, data.Length, new AsyncCallback(DoBeginWrite), null);
    }


    /// <summary>
    ///     Sends the <see cref="PacketType.Unsubscribe"/> packet.
    /// </summary>
    /// <exception cref="InvalidOperationException">Client is not connected.</exception>
    private void SendUnsubscribePacket()
    {
        if (_tcpClient == null)
            throw new InvalidOperationException();

        _writer!.Start(PacketType.Unsubscribe);

        var data = _writer.End();

        _stream!.BeginWrite(data, 0, data.Length, new AsyncCallback(DoBeginWrite), null);
    }


    /// <summary>
    ///     Reads the incoming news packet.
    /// </summary>
    private void ReceiveNews()
    {
        var title = _reader!.ReadString();
        var description = _reader!.ReadString();
        var content = _reader!.ReadString();

        OnNewsReceived?.Invoke(new News(title, description, content));
    }
}
