using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace NewsDistribution;

/// <summary>
///     Network client implementation.
/// </summary>
public class NetworkClient
{
    /// <summary>
    ///     Read-write buffer.
    /// </summary>
    public readonly byte[] Buffer = new byte[1024];

    /// <summary>
    ///     Client name.
    /// </summary>
    public readonly string Name;

    /// <summary>
    ///     Client socket.
    /// </summary>
    public readonly Socket Socket;

    /// <summary>
    ///     Initializes a NetworkClient instance.
    /// </summary>
    /// <param name="socket">Client socket.</param>
    /// <param name="name">Client name.</param>
    public NetworkClient(Socket socket, string name)
    {
        Socket = socket;
        Name = name;
    }
}

/// <summary>
///     TCP news server implemenation.
/// </summary>
public class NewsServer
{
    /// <summary>
    ///     Delegate for OnClientAuthenticated.
    /// </summary>
    /// <param name="name">Client name.</param>
    public delegate void ClientAuthenticated(string name);

    /// <summary>
    ///     Delegate for OnClientUnsubscribed.
    /// </summary>
    /// <param name="name">Client name.</param>
    public delegate void ClientUnsubscribed(string name);

    /// <summary>
    ///     Dictionary of clients.
    /// </summary>
    private readonly Dictionary<string, NetworkClient> _clients = new();

    /// <summary>
    ///     Thread cancellation token source.
    /// </summary>
    private CancellationTokenSource? _acceptClientsToken;

    /// <summary>
    ///     Thread for processing incoming connections.
    /// </summary>
    private Thread? _acceptThread;

    /// <summary>
    ///     Listening socket.
    /// </summary>
    private Socket? _listener;


    /// <summary>
    ///     List of client names.
    /// </summary>
    public IEnumerable<string> Clients => _clients.Keys.ToList();


    /// <summary>
    ///     Invoked when a client successfully authenticates.
    /// </summary>
    public event ClientAuthenticated? OnClientAuthenticated;

    /// <summary>
    ///     Invoked when a client disconnects.
    /// </summary>
    public event ClientUnsubscribed? OnClientUnsubscribed;


    /// <summary>
    ///     Starts the server.
    /// </summary>
    /// <param name="port">Server port.</param>
    /// <returns><c>true</c> if successfully started.</returns>
    public bool Start(ushort port)
    {
        if (_listener != null)
            return false;

        try
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
            _listener.Listen();

            _acceptClientsToken = new CancellationTokenSource();

            _acceptThread = new Thread(AcceptThreadProc);
            _acceptThread.Start();

            return true;
        }
        catch (SocketException)
        {
            _listener = null;
            return false;
        }
    }

    /// <summary>
    ///     Disconnects all clients and shuts down the server.
    /// </summary>
    public void Shutdown()
    {
        if (_listener == null)
            return;

        _acceptClientsToken!.Cancel();

        foreach (var client in _clients.Values)
            SendUnsubscribePacket(client);

        _clients.Clear();

        try
        {
            _listener.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }

        _listener.Close();
        _listener = null;
    }

    /// <summary>
    ///     Processes incoming connections.
    /// </summary>
    private async void AcceptThreadProc()
    {
        ArgumentNullException.ThrowIfNull(_listener, nameof(_listener));
        ArgumentNullException.ThrowIfNull(_acceptClientsToken, nameof(_acceptClientsToken));

        while (true)
            try
            {
                var clientSocket = await _listener.AcceptAsync(_acceptClientsToken.Token);

                NetworkStream clientStream = new(clientSocket);

                var client = AuthenticateClient(clientSocket);

                clientStream.WriteByte(client != null ? (byte) 1 : (byte) 0);
                clientStream.Flush();

                if (client == null) continue;
                Thread clientThread = new(ClientThreadProc);
                clientThread.Start(client);
            }
            catch (OperationCanceledException)
            {
                break;
            }
    }

    /// <summary>
    ///     Processes client packets.
    /// </summary>
    /// <param name="networkClient">NetworkClient object.</param>
    private void ClientThreadProc(object? networkClient)
    {
        var client = (NetworkClient) networkClient!;
        client.Socket.ReceiveTimeout = -1;

        var run = true;

        while (run)
        {
            try
            {
                client.Socket.Receive(client.Buffer, 1, SocketFlags.None);
            }
            catch (SocketException)
            {
                if (!client.Socket.Connected)
                {
                    UnsubscribeClient(client);
                    return;
                }
            }

            var packetType = (PacketType) client.Buffer[0];

            if (packetType != PacketType.Unsubscribe) continue;
            UnsubscribeClient(client);
            run = false;
        }
    }

    /// <summary>
    ///     Authenticates the client.
    /// </summary>
    /// <param name="socket">Client socket.</param>
    /// <returns>Created NetworkClient or <c>null</c>.</returns>
    private NetworkClient? AuthenticateClient(Socket socket)
    {
        var buffer = new byte[256];

        socket.ReceiveTimeout = 5000;

        if (!TryReceive(socket, buffer, 1))
            return null;

        int nameLength = buffer[0];

        if (nameLength == 0 || !TryReceive(socket, buffer, nameLength))
            return null;

        string name;

        try
        {
            name = Encoding.UTF8.GetString(buffer.AsSpan(0, nameLength));
        }
        catch (ArgumentException)
        {
            return null;
        }

        name = Regex.Replace(name, @"[\x00-\x19]+", "").Trim();

        if (name == string.Empty)
            return null;

        NetworkClient client = new(socket, name);

        try
        {
            _clients.Add(name, client);
        }
        catch (ArgumentException)
        {
            return null;
        }

        OnClientAuthenticated?.Invoke(name);

        return client;
    }

    /// <summary>
    ///     Unsubscribes the client.
    /// </summary>
    /// <param name="client">Client object.</param>
    /// <param name="removeFromTable">Should the client be removed from the table?</param>
    private void UnsubscribeClient(NetworkClient client, bool removeFromTable = true)
    {
        if (removeFromTable)
            _clients.Remove(client.Name);

        client.Socket.Close();

        OnClientUnsubscribed?.Invoke(client.Name);
    }

    /// <summary>
    ///     Sends the <c>Unsubscribe</c> packet to the client.
    /// </summary>
    /// <param name="client">Client object.</param>
    /// <param name="removeFromTable">Should the client be removed from the table?</param>
    private void SendUnsubscribePacket(NetworkClient client, bool removeFromTable = true)
    {
        client.Buffer[0] = (byte) PacketType.Unsubscribe;
        client.Socket.Send(client.Buffer, 1, SocketFlags.None);

        UnsubscribeClient(client, removeFromTable);
    }

    /// <summary>
    ///     Sends the <c>News</c> packet to the client.
    /// </summary>
    /// <param name="client">Client object.</param>
    /// <param name="news">News to send.</param>
    private static void SendNewsPacket(NetworkClient client, News news)
    {
        NetworkStream stream = new(client.Socket);
        BinaryWriter writer = new(stream);

        writer.Write((byte) PacketType.News);
        var (title, description, content) = news;
        writer.Write(title);
        writer.Write(description);
        writer.Write(content);
    }

    /// <summary>
    ///     Sends news to each client.
    /// </summary>
    /// <param name="news">News to send.</param>
    public void SendNews(News news)
    {
        foreach (var client in _clients.Values)
            SendNewsPacket(client, news);
    }

    /// <summary>
    ///     Tries to receive <paramref name="size" /> bytes from
    ///     the socket and writes the received data to
    ///     <paramref name="buffer" />.
    /// </summary>
    /// <param name="socket">Socket to read from.</param>
    /// <param name="buffer">Buffer to write to.</param>
    /// <param name="size">Size of the received data.</param>
    /// <returns><c>true</c> if successful, <c>false</c> on time-out.</returns>
    private static bool TryReceive(Socket socket, byte[] buffer, int size)
    {
        var offset = 0;

        try
        {
            while (size > 0)
            {
                var received = socket.Receive(buffer, offset, size, SocketFlags.None);
                offset += received;
                size -= received;
            }
        }
        catch (SocketException)
        {
            // Time-out.
            return false;
        }

        return true;
    }
}