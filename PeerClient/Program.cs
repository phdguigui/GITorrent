using System.Net;
using System.Net.Sockets;
using System.Text;

string trackerIp = "192.168.0.242";
int trackerPort = 5000;
string folderPath = @"C:\TorrentPieces";
Dictionary<string, List<string>> _piecesByPeer = new();
Dictionary<string, List<string>> _peersByPiece = new();
List<string> _myPieces = new();
bool _amISeeder = false;
int _fileLength = 0;
object myPiecesLock = new();

UdpClient udpClient = new(0);

try
{
    string localIp = GetCurrentIP();
    int localPort = (udpClient.Client?.LocalEndPoint as IPEndPoint)?.Port ??
        throw new InvalidOperationException("LocalEndPoint ou Port não está definido.");

    Console.WriteLine($"IP local detectado: {localIp}");
    Console.WriteLine($"Porta local escolhida: {localPort}\n");

    var peerIps = SendJoinRequest();
    StartPeerServer();
    StartNotificationServer();
    NotifyPeersAboutJoin(peerIps, localPort);

    // Envia periodicamente ao tracker as peças que possui
    new Thread(() =>
    {
        while (true)
        {
            Thread.Sleep(3000); // 3 segundos
            SendHavePiece(_myPieces);
        }
    })
    { IsBackground = true }.Start();

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

List<string> SendJoinRequest()
{
    var trackerEndpoint = SendMessageTracker("JOIN_REQUEST");
    var peersAndPieces = ListenMessageTracker(trackerEndpoint);

    _amISeeder = peersAndPieces.Split("|")[1].ToString() == "NONE";

    var peerIps = new List<string>();

    if (!_amISeeder)
    {
        var piersAndPiecesFormatted = peersAndPieces.Split("|")[1].Split(" ")[0].Remove(peersAndPieces.Split("|")[1].Split(" ")[0].Length - 1).Split(";");

        foreach (var peer in piersAndPiecesFormatted)
        {
            var hasPiece = peer.Split("[")[1] != "]";
            var ip = peer.Split("[")[0];
            _piecesByPeer.Add(
                ip,
                hasPiece ? peer.Split("[")[1].Split(",").Select(x => x.Replace("]", "")).ToList() : new());
            peerIps.Add(ip);
        }

        _fileLength = int.Parse(peersAndPieces.Split("SIZE")[1]);
    }
    return peerIps;
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
    if (message.Contains("HAVE_PIECE"))
    {
        message = "Atualização de peças";
    }
    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => (Tracker): {message}");

    return trackerEndPoint;
}

string ListenMessageTracker(IPEndPoint trackerEndPoint)
{
    var response = udpClient.Receive(ref trackerEndPoint);
    string responseMessage = Encoding.UTF8.GetString(response);
    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= (Tracker): {responseMessage}");

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

    var missingPieces = _peersByPiece
        .Where(p => !_myPieces.Contains(p.Key))
        .OrderBy(p => p.Value.Count)
        .ToList();

    if (missingPieces.Count == 0)
        return;

    var random = new Random();

    var piecesForFirstPeer = new List<(string piece, string peerIp)>();
    var piecesForRandomPeer = new List<(string piece, string peerIp)>();

    foreach (var pieceEntry in missingPieces)
    {
        string piece = pieceEntry.Key;
        var peers = pieceEntry.Value;

        var firstPeer = peers.FirstOrDefault();
        if (firstPeer != null)
            piecesForFirstPeer.Add((piece, firstPeer));

        var randomPeers = peers.Where(p => p != firstPeer).ToList();
        if (randomPeers.Count > 0)
        {
            var randomPeer = randomPeers[random.Next(randomPeers.Count)];
            piecesForRandomPeer.Add((piece, randomPeer));
        }
    }

    var task1 = Task.Run(() =>
    {
        foreach (var (piece, peerIp) in piecesForFirstPeer)
        {
            bool deveBaixar = false;
            lock (myPiecesLock)
            {
                if (!_myPieces.Contains(piece))
                {
                    _myPieces.Add(piece);
                    deveBaixar = true;
                }
            }
            if (deveBaixar)
                RequestPieceFromPeer(peerIp, piece);
        }
    });

    var task2 = Task.Run(() =>
    {
        foreach (var (piece, peerIp) in piecesForRandomPeer)
        {
            bool deveBaixar = false;
            lock (myPiecesLock)
            {
                if (!_myPieces.Contains(piece))
                {
                    _myPieces.Add(piece);
                    deveBaixar = true;
                }
            }
            if (deveBaixar)
                RequestPieceFromPeer(peerIp, piece);
        }
    });

    Task.WaitAll(task1, task2);
}

void RequestPieceFromPeer(string peerIp, string piece)
{
    try
    {
        using TcpClient client = new();
        client.Connect(IPAddress.Parse(peerIp), 6000);

        using NetworkStream stream = client.GetStream();
        byte[] requestBytes = Encoding.UTF8.GetBytes(piece);
        stream.Write(requestBytes, 0, requestBytes.Length);

        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({peerIp}): Peça '{piece}' solicitada");

        using MemoryStream ms = new();
        byte[] buffer = new byte[1024];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, bytesRead);
        }

        string filePath = Path.Combine(folderPath, piece + ".txt");
        File.WriteAllBytes(filePath, ms.ToArray());
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({peerIp}): Peça '{piece}' recebida");

        _myPieces.Add(piece);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({peerIp}): Erro ao requisitar peça '{piece}': {ex.Message}");
    }
}

void StartPeerServer()
{
    new Thread(() =>
    {
        TcpListener listener = new(IPAddress.Any, 6000);
        listener.Start();
        Console.WriteLine("Servidor TCP iniciado na porta 6000.");

        while (true)
        {
            try
            {
                using TcpClient client = listener.AcceptTcpClient();
                if (client.Client?.RemoteEndPoint is not IPEndPoint remoteEndPoint)
                {
                    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Interno): Falha ao obter o endereço remoto do cliente.");
                    continue;
                }

                string clientIp = remoteEndPoint.Address.ToString();
                using NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string requestedPiece = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({clientIp}): Peça '{requestedPiece}' requisitada");

                string filePath = Path.Combine(folderPath, requestedPiece + ".txt");
                if (File.Exists(filePath))
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    stream.Write(fileData, 0, fileData.Length);
                    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({clientIp}): Peça '{requestedPiece}' enviada");
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({clientIp}): Peça '{requestedPiece}' requisitada não encontrada");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Interno): Falha ao enviar peça requisitada: " + ex.Message);
            }
        }
    }).Start();
}

void NotifyPeersAboutJoin(List<string> peerIps, int localPort)
{
    foreach (var peerIp in peerIps)
    {
        if (peerIp == GetCurrentIP()) continue;
        try
        {
            using TcpClient client = new ();
            client.Connect(IPAddress.Parse(peerIp), 6001);
            using NetworkStream stream = client.GetStream();
            string msg = $"NEW_PEER|{GetCurrentIP()}|{string.Join(",", _myPieces)}";
            byte[] data = Encoding.UTF8.GetBytes(msg);
            stream.Write(data, 0, data.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Interno): Falha ao notificar peer {peerIp}: {ex.Message}");
        }
    }
}

void StartNotificationServer()
{
    new Thread(() =>
    {
        TcpListener listener = new(IPAddress.Any, 6001);
        listener.Start();
        Console.WriteLine("Servidor de notificação iniciado na porta 6001.");

        while (true)
        {
            try
            {
                using TcpClient client = listener.AcceptTcpClient();
                using NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (message.StartsWith("NEW_PEER|"))
                {
                    var parts = message.Split('|');
                    string newPeerIp = parts[1];
                    List<string> pieces = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2])
                        ? parts[2].Split(',').ToList()
                        : new();
                    _piecesByPeer[newPeerIp] = pieces;

                    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | ({newPeerIp}): Novo peer entrou na rede");

                    lock (_piecesByPeer)
                    {
                        if (_piecesByPeer.Count < 2 && !_piecesByPeer.ContainsKey(newPeerIp))
                        {
                            new Thread(() =>
                            {
                                List<string> missingPieces;
                                lock (myPiecesLock)
                                {
                                    missingPieces = Enumerable.Range(1, _fileLength)
                                        .Select(i => i.ToString())
                                        .Where(piece => !_myPieces.Contains(piece))
                                        .ToList();
                                }

                                foreach (var piece in missingPieces)
                                {
                                    bool deveBaixar = false;
                                    lock (myPiecesLock)
                                    {
                                        if (!_myPieces.Contains(piece))
                                        {
                                            _myPieces.Add(piece); 
                                            deveBaixar = true;
                                        }
                                    }
                                    if (deveBaixar)
                                        RequestPieceFromPeer(newPeerIp, piece);
                                }
                            }).Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao receber notificação de novo peer: " + ex.Message);
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