using System.Net;
using System.Net.Sockets;
using System.Text;

public class PeerClient
{
    private string trackerIP;
    private int trackerPort;
    private int tcpPort;
    private List<int> myPieces = new();
    private Dictionary<string, List<int>> peerMap = new();

    public PeerClient(string trackerIP, int trackerPort, int tcpPort)
    {
        this.trackerIP = trackerIP;
        this.trackerPort = trackerPort;
        this.tcpPort = tcpPort;
    }

    public void Start()
    {
        // Start TCP server in background
        new Thread(StartTcpServer).Start();

        // Get peer list
        peerMap = RequestPeers();

        // Choose rarest piece
        int rarePiece = SelectRarestPiece();
        string peerIP = FindPeerWithPiece(rarePiece);

        // Download piece
        DownloadPiece(peerIP, rarePiece);
        myPieces.Add(rarePiece);

        // Start periodic update
        Timer t = new Timer(SendPieceUpdate, null, 0, 30000);
    }

    private Dictionary<string, List<int>> RequestPeers()
    {
        UdpClient udpClient = new();
        byte[] request = Encoding.UTF8.GetBytes("REQUEST_PEERS");
        udpClient.Send(request, request.Length, trackerIP, trackerPort);

        IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
        byte[] response = udpClient.Receive(ref ep);

        string data = Encoding.UTF8.GetString(response);
        return ParsePeers(data);
    }

    private Dictionary<string, List<int>> ParsePeers(string data)
    {
        var map = new Dictionary<string, List<int>>();
        var peers = data.Split('|');
        foreach (var peer in peers)
        {
            var parts = peer.Split(':');
            string ip = parts[0];
            var pieces = new List<int>();
            foreach (var p in parts[1].Split(','))
                pieces.Add(int.Parse(p));
            map[ip] = pieces;
        }
        return map;
    }

    private int SelectRarestPiece()
    {
        var pieceCount = new Dictionary<int, int>();
        foreach (var entry in peerMap)
        {
            foreach (var piece in entry.Value)
            {
                if (!pieceCount.ContainsKey(piece))
                    pieceCount[piece] = 0;
                pieceCount[piece]++;
            }
        }
        int minCount = int.MaxValue, rarest = -1;
        foreach (var kv in pieceCount)
        {
            if (!myPieces.Contains(kv.Key) && kv.Value < minCount)
            {
                minCount = kv.Value;
                rarest = kv.Key;
            }
        }
        return rarest;
    }

    private string FindPeerWithPiece(int piece)
    {
        foreach (var entry in peerMap)
        {
            if (entry.Value.Contains(piece))
                return entry.Key;
        }
        return null;
    }

    private void DownloadPiece(string ip, int piece)
    {
        TcpClient client = new();
        client.Connect(ip, tcpPort);
        NetworkStream stream = client.GetStream();
        byte[] req = Encoding.UTF8.GetBytes($"GET:{piece}");
        stream.Write(req, 0, req.Length);

        using FileStream fs = new FileStream($"piece_{piece}.bin", FileMode.Create);
        byte[] buffer = new byte[1024];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            fs.Write(buffer, 0, bytesRead);
        }
        Console.WriteLine($"Recebido pedaço {piece} de {ip}.");
    }

    private void StartTcpServer()
    {
        TcpListener server = new TcpListener(IPAddress.Any, tcpPort);
        server.Start();
        Console.WriteLine($"Peer TCP server rodando na porta {tcpPort}");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (request.StartsWith("GET:"))
            {
                int pieceId = int.Parse(request.Split(':')[1]);
                byte[] piece = File.ReadAllBytes($"piece_{pieceId}.bin");
                stream.Write(piece, 0, piece.Length);
                Console.WriteLine($"Enviado pedaço {pieceId}");
            }
            client.Close();
        }
    }

    private void SendPieceUpdate(object state)
    {
        UdpClient udpClient = new();
        string msg = $"UPDATE_PIECES|{GetLocalIPAddress()}|{string.Join(",", myPieces)}";
        byte[] data = Encoding.UTF8.GetBytes(msg);
        udpClient.Send(data, data.Length, trackerIP, trackerPort);
    }

    private string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        throw new Exception("Local IP não encontrado!");
    }
}