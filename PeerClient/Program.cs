using System.Net;
using System.Net.Sockets;
using System.Text;

string trackerIp = "192.168.0.242";
int trackerPort = 5000;
string folderPath = @"C:\TorrentPieces";
Dictionary<string, List<string>> _piecesByPeer = new();
Dictionary<string, List<string>> _peersByPiece = new();
List<string> _myPieces = new();
bool _amIFirst = false;
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

    new Thread(() =>
    {
        while (true)
        {
            Thread.Sleep(3000);
            SendHavePiece(_myPieces);
        }
    })
    { IsBackground = true }.Start();

    var initialPieces = VerifyPieces();
    if (_amIFirst || initialPieces.Count == _fileLength)
    {
        _amIFirst = true;
    }
    if (initialPieces.Count > 0)
    {
        SendHavePiece(initialPieces);
    }
    if (!_amIFirst)
    {
        new Thread(() => FirstConnection()) { IsBackground = true }.Start();
    }

    StartPeerUpdater();
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

    _amIFirst = peersAndPieces.Split("|")[1].ToString() == "NONE";

    var peerIps = new List<string>();

    if (!_amIFirst)
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
    var message = $"HAVE_PIECE{(_amIFirst ? "_SEEDER" : "")}|{string.Join(",", pieces)}";
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
        byte[] buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, bytesRead);
        }

        // Verifica se recebeu "PIECE_NOT_FOUND"
        var receivedData = ms.ToArray();
        string message = Encoding.UTF8.GetString(receivedData);

        if (message == "PIECE_NOT_FOUND")
        {
            Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({peerIp}): Peer não possui a peça '{piece}' (PIECE_NOT_FOUND)");
            return;
        }

        // Salva o arquivo e adiciona à lista somente se não recebeu "PIECE_NOT_FOUND"
        string filePath = Path.Combine(folderPath, piece + ".txt");
        File.WriteAllBytes(filePath, receivedData);
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({peerIp}): Peça '{piece}' recebida");

        lock (myPiecesLock)
        {
            if (!_myPieces.Contains(piece))
                _myPieces.Add(piece);
        }
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

                byte[] buffer = new byte[4096];
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
                    // Envia "PIECE_NOT_FOUND"
                    string errorMsg = "PIECE_NOT_FOUND";
                    byte[] errorBytes = Encoding.UTF8.GetBytes(errorMsg);
                    stream.Write(errorBytes, 0, errorBytes.Length);
                    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({clientIp}): Peça '{requestedPiece}' requisitada não encontrada (PIECE_NOT_FOUND enviado)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Interno): Falha ao enviar peça requisitada: " + ex.Message);
            }
        }
    }).Start();
}

void StartPeerUpdater()
{
    new Thread(() =>
    {
        while (true)
        {
            try
            {
                UpdatePeersFromTracker();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Interno): Erro ao atualizar peers do tracker: {ex.Message}");
            }
            Thread.Sleep(1000); // 1 segundo
        }
    })
    { IsBackground = true }.Start();
}

void UpdatePeersFromTracker()
{
    var trackerEndpoint = SendMessageTracker("GET_PEERS");
    var peersAndPieces = ListenMessageTracker(trackerEndpoint);

    var tempPiecesByPeer = new Dictionary<string, List<string>>();
    var tempPeersByPiece = new Dictionary<string, List<string>>();

    var data = peersAndPieces.Split("|");
    if (data.Length < 2 || data[1] == "NONE")
        return;

    var piersAndPiecesFormatted = data[1].Split(" ")[0].Remove(data[1].Split(" ")[0].Length - 1).Split(";");

    foreach (var peer in piersAndPiecesFormatted)
    {
        var hasPiece = peer.Split("[")[1] != "]";
        var ip = peer.Split("[")[0];
        var pieces = hasPiece ? peer.Split("[")[1].Split(",").Select(x => x.Replace("]", "")).ToList() : new List<string>();
        tempPiecesByPeer[ip] = pieces;

        foreach (var piece in pieces)
        {
            if (!tempPeersByPiece.ContainsKey(piece))
                tempPeersByPiece[piece] = new List<string>();
            tempPeersByPiece[piece].Add(ip);
        }
    }

    lock (_piecesByPeer)
    {
        _piecesByPeer.Clear();
        foreach (var kv in tempPiecesByPeer)
            _piecesByPeer[kv.Key] = kv.Value;
    }
    lock (_peersByPiece)
    {
        _peersByPiece.Clear();
        foreach (var kv in tempPeersByPiece)
            _peersByPiece[kv.Key] = kv.Value;
    }

    // Chama a rotina de requisição de peças faltantes
    RequestMissingPieces();
}

void RequestMissingPieces()
{
    List<string> missingPieces;
    Dictionary<string, List<string>> currentPeersByPiece;
    lock (_peersByPiece)
    {
        // Copia para evitar race condition
        currentPeersByPiece = _peersByPiece.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToList()
        );
    }
    lock (myPiecesLock)
    {
        missingPieces = currentPeersByPiece.Keys.Where(p => !_myPieces.Contains(p)).ToList();
    }

    if (missingPieces.Count > 0)
    {
        var tasks = new List<Task>();
        foreach (var piece in missingPieces)
        {
            var peerIps = currentPeersByPiece[piece];
            if (peerIps == null || peerIps.Count == 0)
                continue;

            // Sorteia um peer disponível para cada peça
            var chosenPeer = peerIps[new Random().Next(peerIps.Count)];
            tasks.Add(Task.Run(() =>
            {
                lock (myPiecesLock)
                {
                    if (_myPieces.Contains(piece))
                        return;
                }
                RequestPieceFromPeer(chosenPeer, piece);
            }));
        }
        Task.WaitAll(tasks.ToArray());
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