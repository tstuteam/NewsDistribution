using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace NewsDistribution;

public class NetworkClient
{
    public readonly Socket Socket;
    public readonly string Name;
    public readonly byte[] Buffer = new byte[1024];

    public NetworkClient(Socket socket, string name)
    {
        Socket = socket;
        Name = name;
    }
}

public class NewsServer
{
    private readonly Dictionary<string, NetworkClient> _clients = new();
    private Socket? _listener;
    private Thread? _acceptThread;
    private CancellationTokenSource? _acceptClientsToken;

    public delegate void ClientAuthorized(string name);
    public delegate void ClientUnsubscribed(string name);

    public event ClientAuthorized? OnClientAuthorized;
    public event ClientUnsubscribed? OnClientUnsubscribed;

    public List<string> Clients => _clients.Keys.ToList();

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

    public void Shutdown()
    {
        if (_listener == null)
            return;

        _acceptClientsToken!.Cancel();

        foreach (NetworkClient client in _clients.Values)
            SendUnsubscribePacket(client);

        _clients.Clear();

        try
        {
            _listener.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException) { }

        _listener.Close();
        _listener = null;
    }

    private async void AcceptThreadProc()
    {
        ArgumentNullException.ThrowIfNull(_listener, nameof(_listener));
        ArgumentNullException.ThrowIfNull(_acceptClientsToken, nameof(_acceptClientsToken));

        while (true)
        {
            try
            {
                Socket clientSocket = await _listener.AcceptAsync(_acceptClientsToken.Token);

                NetworkStream clientStream = new(clientSocket);

                NetworkClient? client = AuthorizeClient(clientSocket);

                clientStream.WriteByte(client != null ? (byte)1 : (byte)0);
                clientStream.Flush();

                if (client != null)
                {
                    Thread clientThread = new(ClientThreadProc);
                    clientThread.Start(client);
                }
            }
            catch (OperationCanceledException) {
                break;
            }
        }
    }

    private void ClientThreadProc(object? _client)
    {
        NetworkClient client = (NetworkClient)_client!;
        client.Socket.ReceiveTimeout = -1;

        bool run = true;

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

            PacketType packetType = (PacketType)client.Buffer[0];

            switch (packetType)
            {
                case PacketType.Unsubscribe:
                    UnsubscribeClient(client);
                    run = false;

                    break;
            }
        }
    }

    private NetworkClient? AuthorizeClient(Socket socket)
    {
        byte[] buffer = new byte[256];

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

        OnClientAuthorized?.Invoke(name);

        return client;
    }

    private void UnsubscribeClient(NetworkClient client, bool removeFromTable = true)
    {
        if (removeFromTable)
            _clients.Remove(client.Name);

        client.Socket.Close();

        OnClientUnsubscribed?.Invoke(client.Name);
    }

    private void SendUnsubscribePacket(NetworkClient client, bool removeFromTable = true)
    {
        client.Buffer[0] = (byte)PacketType.Unsubscribe;
        client.Socket.Send(client.Buffer, 1, SocketFlags.None);

        UnsubscribeClient(client, removeFromTable);
    }

    private void SendNewsPacket(NetworkClient client, News news)
    {
        NetworkStream stream = new(client.Socket);
        BinaryWriter writer = new(stream);

        writer.Write((byte)PacketType.News);
        writer.Write(news.Title);
        writer.Write(news.Description);
        writer.Write(news.Content);
    }

    public void SendNews(News news)
    {
        foreach (NetworkClient client in _clients.Values)
            SendNewsPacket(client, news);
    }

    private static bool TryReceive(Socket socket, byte[] buffer, int size)
    {
        int offset = 0;

        try
        {
            while (size > 0)
            {
                int received = socket.Receive(buffer, offset, size, SocketFlags.None);
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
