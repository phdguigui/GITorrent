using System.Net;
using System.Net.Sockets;
using System.Text;

string trackerIp = "192.168.0.242";
int trackerPort = 5000;
string folderPath = @"C:\AaaTeste2";
Dictionary<string, List<string>> _piecesByPeer = new();
Dictionary<string, List<string>> _peersByPiece = new();
List<string> _myPieces = new();
bool _amISeeder = false;
int _fileLength = 0;

UdpClient udpClient = new UdpClient(0);

try
{
    string localIp = GetCurrentIP();
    int localPort = (udpClient.Client.LocalEndPoint as IPEndPoint).Port;

    Console.WriteLine($"IP local detectado: {localIp}");
    Console.WriteLine($"Porta local escolhida: {localPort}\n");

    SendJoinRequest();
    StartPeerServer();

    var initialPieces = VerifyPieces();
    if (_amISeeder || initialPieces.Count == _fileLength)
    {
        _amISeeder = true;
    }
    if (initialPieces.Count > 0)
    {
        SendHavePiece(initialPieces);
    }
    if (!_amISeeder)
    {
        FirstConnection();
    }
}
catch (Exception ex)
{
    Console.WriteLine("Erro: " + ex.Message);
}

List<string> VerifyPieces()
{
    // Verificar existência da pasta
    if (Directory.Exists(folderPath))
    {
        var files = Directory.GetFiles(folderPath);
        if (files.Length > 0)
        {
            List<string> pieces = new(files.ToList().Select(x => Path.GetFileName(x).Split(".")[0]));
            return pieces;
        }
        Directory.CreateDirectory(folderPath);
        return [];
    }
    else
    {
        Directory.CreateDirectory(folderPath);
        return [];
    }
}

void SendJoinRequest()
{
    var trackerEndpoint = SendMessageTracker("JOIN_REQUEST");
    var peersAndPieces = ListenMessageTracker(trackerEndpoint);

    _amISeeder = peersAndPieces.Split("|")[1].ToString() == "NONE";

    if (!_amISeeder)
    {
        // 192.168.0.1[1,2,3]
        var piersAndPiecesFormatted = peersAndPieces.Split("|")[1].Split(" ")[0].Remove(peersAndPieces.Split("|")[1].Split(" ")[0].Length - 1).Split(";");

        foreach (var peer in piersAndPiecesFormatted)
        {
            var hasPiece = peer.Split("[")[1] != "]";
            _piecesByPeer.Add(
                peer.Split("[")[0],
                hasPiece ? peer.Split("[")[1].Split(",").Select(x => x.Replace("]", "")).ToList() : new());
        }

        _fileLength = int.Parse(peersAndPieces.Split("SIZE")[1]);
    }
}

void SendHavePiece(List<string> pieces)
{
    _myPieces = pieces;
    var message = $"HAVE_PIECE{(_amISeeder ? "_SEEDER" : "")}|{string.Join(",", pieces)}";
    SendMessageTracker(message);
}

IPEndPoint SendMessageTracker(string message)
{
    byte[] data = Encoding.UTF8.GetBytes(message);
    IPEndPoint trackerEndPoint = new IPEndPoint(IPAddress.Parse(trackerIp), trackerPort);

    udpClient.Send(data, data.Length, trackerEndPoint);
    Console.WriteLine($"Enviado para Tracker: {message}\n");

    return trackerEndPoint;
}

string ListenMessageTracker(IPEndPoint trackerEndPoint)
{
    var response = udpClient.Receive(ref trackerEndPoint);
    string responseMessage = Encoding.UTF8.GetString(response);
    Console.WriteLine($"Recebido do Tracker: {responseMessage}\n");

    return responseMessage;
}

void FirstConnection()
{
    RarestFirst();
}

void RarestFirst()
{
    foreach (var peer in _piecesByPeer)
    {
        foreach (var piece in peer.Value)
        {
            if (!_peersByPiece.ContainsKey(piece))
            {
                _peersByPiece[piece] = new List<string>();
            }
            _peersByPiece[piece].Add(peer.Key);
        }
    }

    // Verify rarest piece
    string rarestPiece = "";
    int rarestCount = 0;
    bool firstSearch = true;
    foreach(var piece in _peersByPiece)
    {
        if (_myPieces.Contains(piece.Key))
        {
            continue;
        }

        if (firstSearch)
        {
            rarestPiece = piece.Key;
            rarestCount = piece.Value.Count;
            firstSearch = false;
            continue;
        }
        
        if (piece.Value.Count < rarestCount)
        {
            rarestPiece = piece.Key;
            rarestCount = piece.Value.Count;
        }
    }

    var firstIp = _peersByPiece[rarestPiece]?.FirstOrDefault();

    RequestPieceFromPeer(firstIp!, rarestPiece);
}

void RequestPieceFromPeer(string peerIp, string piece)
{
    try
    {
        using TcpClient client = new TcpClient();
        client.Connect(IPAddress.Parse(peerIp), 6000);

        using NetworkStream stream = client.GetStream();
        byte[] requestBytes = Encoding.UTF8.GetBytes(piece);
        stream.Write(requestBytes, 0, requestBytes.Length);

        Console.WriteLine($"Solicitada peça '{piece}' do peer {peerIp}");

        using MemoryStream ms = new();
        byte[] buffer = new byte[1024];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, bytesRead);
        }

        string filePath = Path.Combine(folderPath, piece + ".txt");
        File.WriteAllBytes(filePath, ms.ToArray());
        Console.WriteLine($"Peça '{piece}' recebida e salva em '{filePath}'");

        _myPieces.Add(piece);
        SendHavePiece(_myPieces);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao requisitar peça '{piece}' do peer {peerIp}: {ex.Message}");
    }
}

void StartPeerServer()
{
    new Thread(() =>
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 6000);
        listener.Start();
        Console.WriteLine("Servidor TCP iniciado na porta 6000.");

        while (true)
        {
            try
            {
                using TcpClient client = listener.AcceptTcpClient();
                using NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string requestedPiece = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Console.WriteLine($"Requisição recebida para a peça '{requestedPiece}'");

                string filePath = Path.Combine(folderPath, requestedPiece + ".txt");
                if (File.Exists(filePath))
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    stream.Write(fileData, 0, fileData.Length);
                    Console.WriteLine($"Peça '{requestedPiece}' enviada com sucesso.");
                }
                else
                {
                    Console.WriteLine($"Peça '{requestedPiece}' não encontrada.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao responder requisição de peça: " + ex.Message);
            }
        }
    }).Start();
}

#region Helpers
static string GetCurrentIP()
{
    var host = Dns.GetHostEntry(Dns.GetHostName());

    foreach (var ip in host.AddressList)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return ip.ToString();
        }
    }

    return "";
}
#endregion