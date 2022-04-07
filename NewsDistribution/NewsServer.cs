using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NewsDistribution.Shared;

namespace NewsDistribution.Server;


/// <summary>
///     Network client implementation.
/// </summary>
public class NetworkClient : IDisposable
{
    /// <summary>
    ///     Size of the receive buffer.
    /// </summary>
    public const int BufferSize = 1024;


    /// <summary>
    ///     TCP connection client.
    /// </summary>
    public readonly TcpClient TcpClient;


    /// <summary>
    ///     Network stream for <see cref="TcpClient"/>.
    /// </summary>
    public readonly NetworkStream Stream;

    /// <summary>
    ///     Packet reader.
    /// </summary>
    public readonly PacketReader Reader;

    /// <summary>
    ///     Packet writer.
    /// </summary>
    public readonly PacketWriter Writer;

    /// <summary>
    ///     Received data.
    /// </summary>
    public readonly MemoryStream Data;


    /// <summary>
    ///     Receive buffer.
    /// </summary>
    public readonly byte[] PacketBuffer = new byte[BufferSize];

    /// <summary>
    ///     Was the packet header fully read?
    /// </summary>
    public bool PacketHeaderRead;

    /// <summary>
    ///     Packet type of the last packet.
    /// </summary>
    public PacketType PacketType;

    /// <summary>
    ///     Packet data size of the last packet.
    /// </summary>
    public int PacketSize;


    /// <summary>
    ///     Client's name.
    /// </summary>
    public string? Name;

    /// <summary>
    ///     Is the client subscribed?
    /// </summary>
    public bool Subscribed;


    /// <summary>
    ///     Initializes a NetworkClient instance.
    /// </summary>
    /// <param name="tcpClient">TcpClient of the client.</param>
    public NetworkClient(TcpClient tcpClient)
    {
        TcpClient = tcpClient;
        Stream = tcpClient.GetStream();
        Data = new MemoryStream();
        Reader = new PacketReader(Data);
        Writer = new PacketWriter();
    }

    public void Dispose()
    {
        TcpClient.Close();
        Data.Close();
        Reader.Close();
        Writer.Close();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
///     TCP news server implementation.
/// </summary>
public class NewsServer
{
    /// <summary>
    ///     Size of the packet header.
    /// </summary>
    private const int PacketHeaderSize = sizeof(byte) + sizeof(int);


    /// <summary>
    ///     TCP connection listener.
    /// </summary>
    private TcpListener? _listener;

    /// <summary>
    ///     Dictionary of all subscribed clients.
    /// </summary>
    private Dictionary<string, NetworkClient>? _clients;


    /// <summary>
    ///     Packet writer.
    /// </summary>
    public PacketWriter? _writer;


    /// <summary>
    ///     Delegate for <see cref="OnClientSubscribes"/>.
    /// </summary>
    /// <param name="name">Client's name.</param>
    public delegate void SubscribeEvent(string name);

    /// <summary>
    ///     Delegate for <see cref="OnClientUnsubscribes"/>.
    /// </summary>
    /// <param name="name">Client's name.</param>
    public delegate void UnsubscribeEvent(string name);


    /// <summary>
    ///     Invoked when a client subscribes.
    /// </summary>
    public event SubscribeEvent? OnClientSubscribes;

    /// <summary>
    ///     Invoked when a client unsubscribes.
    /// </summary>
    public event UnsubscribeEvent? OnClientUnsubscribes;


    /// <summary>
    ///     List of all client names.
    /// </summary>
    public string[]? Clients => _clients?.Keys.ToArray();


    /// <summary>
    ///     Starts the server.
    /// </summary>
    /// <param name="port">Server port.</param>
    /// <returns><c>true</c> if successfully started.</returns>
    public bool Start(ushort port)
    {
        if (_listener != null)
            return false;

        var endpoint = new IPEndPoint(IPAddress.Any, port);

        try
        {
            _listener = new TcpListener(endpoint);
            _listener.Start();
            _listener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClient), null);
        }
        catch (SocketException)
        {
            return false;
        }

        _clients = new Dictionary<string, NetworkClient>();
        _writer = new PacketWriter();

        return true;
    }


    /// <summary>
    ///     Disconnects all clients and shuts down the server.
    /// </summary>
    public void Shutdown()
    {
        if (_listener == null)
            return;

        lock (_clients!)
        {
            foreach (NetworkClient client in _clients.Values)
                UnsubscribeClient(client);
        }

        _listener.Stop();
        _listener = null;
    }


    /// <summary>
    ///     Sends news to the connected clients.
    /// </summary>
    /// <param name="news">News.</param>
    public void SendNews(News news)
    {
        _writer!.Start(PacketType.News);
        _writer.Write(news.Title);
        _writer.Write(news.Description);
        _writer.Write(news.Content);

        var data = _writer!.End();

        lock (_clients!)
        {
            foreach (NetworkClient client in _clients.Values)
                client.Stream.BeginWrite(data, 0, data.Length, new AsyncCallback(DoBeginWrite), client);
        }
    }


    /// <summary>
    ///     Accepts connections.
    /// </summary>
    /// <param name="asyncResult">Async result.</param>
    private void DoAcceptTcpClient(IAsyncResult asyncResult)
    {
        if (_listener is null)
            return;

        var client = new NetworkClient(_listener!.EndAcceptTcpClient(asyncResult));

        _listener.BeginAcceptTcpClient(new AsyncCallback(DoAcceptTcpClient), null);
        client.Stream.BeginRead(client.PacketBuffer, 0, NetworkClient.BufferSize, new AsyncCallback(DoBeginRead), client);
    }


    /// <summary>
    ///     Reads incoming data.
    /// </summary>
    /// <param name="asyncResult">Async result.</param>
    private void DoBeginRead(IAsyncResult asyncResult)
    {
        var client = (NetworkClient)asyncResult.AsyncState!;
        int bytesRead;

        try
        {
            bytesRead = client.Stream.EndRead(asyncResult);
        }
        catch (Exception ex)
        when (ex is IOException || ex is ObjectDisposedException)
        {
            RemoveClient(client);
            return;
        }

        var dataOffset = 0;

        if (bytesRead <= 0)
        {
            // The remote host shut down the socket connection.
            RemoveClient(client);
            return;
        }

        while (bytesRead > 0)
        {
            if (!client.PacketHeaderRead)
            {
                var needToRead = Math.Min(bytesRead, PacketHeaderSize - (int)client.Data.Length);

                client.Data.Write(client.PacketBuffer, 0, needToRead);

                dataOffset += needToRead;
                bytesRead -= needToRead;

                if (client.Data.Length == PacketHeaderSize)
                {
                    client.PacketHeaderRead = true;
                    client.Data.Position = 0;

                    client.PacketType = (PacketType)client.Reader.ReadByte();
                    client.PacketSize = client.Reader.ReadInt32();

                    client.Data.Position = 0;
                    client.Data.SetLength(0);

                    if (client.PacketSize > 0xFFFF)
                    {
                        UnsubscribeClient(client);
                        return;
                    }
                }
                else
                {
                    client.Stream.BeginRead(client.PacketBuffer, 0, NetworkClient.BufferSize, new AsyncCallback(DoBeginRead), client);
                    return;
                }
            }

            if (client.Data.Length + bytesRead >= client.PacketSize)
            {
                // Happens for empty packets like Unsubscribe.
                if (bytesRead != 0)
                {
                    var needToRead = client.PacketSize - (int)client.Data.Length;
                    client.Data.Write(client.PacketBuffer, dataOffset, needToRead);

                    dataOffset += needToRead;
                    bytesRead -= needToRead;
                }

                client.Data.Position = 0;

                ProcessIncomingPacket(client);

                if (!client.Subscribed)
                    return;

                client.Data.Position = 0;
                client.Data.SetLength(0);

                client.PacketHeaderRead = false;

                if (bytesRead == 0)
                    client.Stream.BeginRead(client.PacketBuffer, 0, NetworkClient.BufferSize, new AsyncCallback(DoBeginRead), client);
            }
            else
            {
                client.Data.Write(client.PacketBuffer, dataOffset, bytesRead);
                client.Stream.BeginRead(client.PacketBuffer, 0, NetworkClient.BufferSize, new AsyncCallback(DoBeginRead), client);
            }
        }
    }


    /// <summary>
    ///     Writes to the connected client.
    /// </summary>
    /// <param name="asyncResult">Async result.</param>
    private void DoBeginWrite(IAsyncResult asyncResult)
    {
        var client = (NetworkClient)asyncResult.AsyncState!;
        var shouldDispose = !client.Subscribed;

        try
        {
            client.Stream.EndWrite(asyncResult);
        }
        catch (IOException)
        {client.Dispose();
            shouldDispose = true;
        }

        if (shouldDispose)
            RemoveClient(client);
    }

    private void DoBeginWriteUnsubscribe(IAsyncResult asyncResult)
    {
        var client = (NetworkClient)asyncResult.AsyncState!;

        try
        {
            client.Stream.EndWrite(asyncResult);
        }
        catch (IOException)
        {
        }

        RemoveClient(client);
    }

    /// <summary>
    ///     Processes the last incoming packet from <paramref name="client"/>.
    /// </summary>
    /// <param name="client">The client.</param>
    private void ProcessIncomingPacket(NetworkClient client)
    {
        Console.WriteLine("Received packet {0}: {1} bytes", client.PacketType, client.PacketSize);

        try
        {
            if (!client.Subscribed)
            {
                if (client.PacketType != PacketType.Subscribe)
                    return;

                var name = client.Reader.ReadString(256);

                name = Regex.Replace(name, @"[\x00-\x19]+", "").Trim();

                if (name != string.Empty)
                {
                    lock (_clients!)
                    {
                        client.Subscribed = _clients.TryAdd(name, client);
                    }
                }

                client.Writer.Start(PacketType.Subscribe);
                client.Writer.Write(client.Subscribed);
                var buffer = client.Writer.End();

                client.Stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(DoBeginWrite), client);

                if (client.Subscribed)
                {
                    client.Name = name;
                    OnClientSubscribes?.Invoke(name);
                }

                return;
            }

            switch (client.PacketType)
            {
                case PacketType.Unsubscribe:
                    RemoveClient(client);
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
    ///     Unsubscribes <paramref name="client"/>.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="sendPacket">Should the packet be sent?</param>
    private void UnsubscribeClient(NetworkClient client)
    {
        client.Writer.Start(PacketType.Unsubscribe);
        var data = client.Writer.End();

        client.Stream.BeginWrite(data, 0, data.Length, new AsyncCallback(DoBeginWriteUnsubscribe), client);
    }


    /// <summary>
    ///     Removes the client from the client list.
    /// </summary>
    /// <param name="client">The client.</param>
    private void RemoveClient(NetworkClient client)
    {
        if (client.Subscribed)
        {
            client.Subscribed = false;

            OnClientUnsubscribes?.Invoke(client.Name!);

            lock (_clients!)
            {
                _clients.Remove(client.Name!);
            }
        }

        client.Dispose();
    }
}
