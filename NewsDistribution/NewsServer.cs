using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace NewsDistribution;

public class NetworkClient
{
    public readonly Socket Socket;
    public readonly string Name;

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
    private CancellationTokenSource? _shouldAcceptClients;

    public Dictionary<string, NetworkClient> Clients
    {
        get => _clients;
    }

    public bool Start(ushort port)
    {
        if (_listener != null)
            return false;

        try
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Any, port));
            _listener.Listen();

            _shouldAcceptClients = new();

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

        _shouldAcceptClients!.Cancel();

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
        ArgumentNullException.ThrowIfNull(_shouldAcceptClients, nameof(_shouldAcceptClients));

        while (true)
        {
            try
            {
                Socket clientSocket = await _listener.AcceptAsync(_shouldAcceptClients.Token);
                clientSocket.ReceiveTimeout = 5000;

                NetworkStream clientStream = new(clientSocket);

                bool authorized = AuthorizeClient(clientStream);

                clientStream.WriteByte(authorized ? (byte)1 : (byte)0);
                clientStream.Flush();
            }
            catch (OperationCanceledException) {
                break;
            }
        }
    }

    private bool AuthorizeClient(NetworkStream stream)
    {
        int nameLength = stream.ReadByte();

        if (nameLength == -1)
            throw new InvalidOperationException();

        byte[] nameBuffer = new byte[nameLength];
        stream.Read(nameBuffer);

        string name;

        try
        {
            name = Encoding.UTF8.GetString(nameBuffer);
        }
        catch (ArgumentException)
        {
            return false;
        }

        name = name.Replace("\n", string.Empty).Trim();

        if (name == string.Empty)
            return false;

        NetworkClient client = new(stream.Socket, name);
        
        try
        {
            _clients.Add(name, client);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return true;
    }
}
