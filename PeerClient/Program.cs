using System.Net;
using System.Net.Sockets;
using System.Text;

string trackerIp = "192.168.56.1"; // IP do Tracker (você pode passar por arquivo depois)
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
            continue;
        }
        
        if (piece.Value.Count < rarestCount)
        {
            rarestPiece = piece.Key;
            rarestCount = piece.Value.Count;
        }
    }

    var firstIp = _peersByPiece[rarestPiece]?.FirstOrDefault();

    if (firstIp != null)
    {
        
    }
    else
    {
        Console.WriteLine("Nenhum peer disponível para baixar a peça rara.");
    }
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