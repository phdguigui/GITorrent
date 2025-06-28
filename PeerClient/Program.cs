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
object myPiecesLock = new();
HashSet<string> _downloadingPieces = new();
object downloadingPiecesLock = new();

UdpClient udpClient = new(0);

try
{
    // Pega IP local
    string localIp = GetCurrentIP();
    // Pega a porta local
    int localPort = (udpClient.Client?.LocalEndPoint as IPEndPoint)?.Port ??
        throw new InvalidOperationException("LocalEndPoint ou Port não está definido.");

    // Mostra as informações iniciais
    PeerClientLogger.Log($"IP local detectado: {localIp}");
    PeerClientLogger.Log($"Porta local escolhida: {localPort}\n");

    // Envia o JoinRequest para o tracker
    var peerIps = SendJoinRequest();
    // Inicia servidor para requisição de peças
    StartPeerServer();

    var initialPieces = VerifyPieces();
    // Se houver peças iniciais, envia a mensagem HAVE_PIECE
    if (initialPieces.Count > 0)
    {
        SendHavePiece();
    }
    // Se não for o primeiro tenta buscar a primeira peça/conexão
    if (!_amIFirst)
    {
        FirstConnection();
    }

    // Servidor que envia o HAVE_PIECE a cada 3 segundos
    StartHavePieceUpdater();
    // Atualiza os peers a cada segundo para o tracker
    StartPeerUpdater();
}
catch (Exception ex)
{
    PeerClientLogger.Log("Erro: " + ex.Message);
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
    // Chama método de envio de mensagens ao tracker
    var trackerEndpoint = SendMessageTracker("JOIN_REQUEST");
    // Escuta a resposta do tracker
    var peersAndPieces = ListenMessageTracker(trackerEndpoint);

    // Sou o primeiro?
    _amIFirst = peersAndPieces.Split("|")[1].ToString() == "NONE";

    var peerIps = new List<string>();

    // Se não for o primeiro, armazena os peers e peças
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
    }
    return peerIps;
}

void SendHavePiece()
{
    var pieces = VerifyPieces();
    lock (myPiecesLock)
    {
        _myPieces = pieces;
    }
    var message = $"HAVE_PIECE|{string.Join(",", pieces)}";
    SendMessageTracker(message);
}

IPEndPoint SendMessageTracker(string message)
{
    // Transforma mensagem em cadeia de bytes
    byte[] data = Encoding.UTF8.GetBytes(message);
    // Cria instância UDP do tracker
    IPEndPoint trackerEndPoint = new IPEndPoint(IPAddress.Parse(trackerIp), trackerPort);

    // Envia a mensagem para o tracker
    udpClient.Send(data, data.Length, trackerEndPoint);
    // Tratamento de mensagens para HAVE_PIECE
    if (message.Contains("HAVE_PIECE"))
    {
        message = "Atualização de peças";
    }
    // Apresenta a info no terminal
    PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => (Tracker): {message}");

    return trackerEndPoint;
}

string ListenMessageTracker(IPEndPoint trackerEndPoint)
{
    var response = udpClient.Receive(ref trackerEndPoint);
    string responseMessage = Encoding.UTF8.GetString(response);
    PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= (Tracker): {responseMessage}");

    return responseMessage;
}

void FirstConnection()
{
    RarestFirst();
}

void RarestFirst()
{
    _peersByPiece.Clear();
    foreach (var peer in _piecesByPeer)
    {
        foreach (var piece in peer.Value)
        {
            if (!_peersByPiece.ContainsKey(piece))
                _peersByPiece[piece] = new List<string>();
            _peersByPiece[piece].Add(peer.Key);
        }
    }

    // Verifica os pedaços faltantes
    var missingPieces = _peersByPiece
        .Where(p => !_myPieces.Contains(p.Key))
        .ToList();

    if (missingPieces.Count == 0)
        return;

    int minRarity = missingPieces.Min(p => p.Value.Count);

    // Verifica a peça mais rara
    var rarestPiece = missingPieces
        .Where(p => p.Value.Count == minRarity)
        .Select(p => p.Key)
        .FirstOrDefault();

    lock (downloadingPiecesLock)
    {
        lock (myPiecesLock)
        {
            if (!_myPieces.Contains(rarestPiece) && !_downloadingPieces.Contains(rarestPiece))
            {
                _downloadingPieces.Add(rarestPiece);
            }
        }
    }

    // Procura peers que possuem a peça mais rara
    var peers = _peersByPiece[rarestPiece];
    // Escolhe de forma aleatória
    var chosenPeer = peers[new Random().Next(peers.Count)];

    // Download síncrono, aguarda terminar antes de pegar o próximo rarest
    RequestPieceFromPeer(chosenPeer, rarestPiece, true);
}

void RequestPieceFromPeer(string peerIp, string piece, bool firstPiece)
{
    try
    {
        // Conecta ao peer na porta 6000
        using TcpClient client = new();
        client.Connect(IPAddress.Parse(peerIp), 6000);

        using NetworkStream stream = client.GetStream();
        byte[] requestBytes = Encoding.UTF8.GetBytes(piece);
        stream.Write(requestBytes, 0, requestBytes.Length);

        // Se primeira peça
        string firstPieceAppend = firstPiece ? "(First Piece)" : "";

        PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({peerIp}:6000): Peça '{piece}' solicitada {firstPieceAppend}");

        using MemoryStream ms = new();
        byte[] buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, bytesRead);
        }

        var receivedData = ms.ToArray();
        // Passa a mensagem para string
        string message = Encoding.UTF8.GetString(receivedData);

        if (message == "PIECE_NOT_FOUND")
        {
            PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({peerIp}:6000): Peer não possui a peça '{piece}' (PIECE_NOT_FOUND)");
            return;
        }

        string filePath = Path.Combine(folderPath, piece + ".txt");
        // Escreve o arquivo com base nos bytes recebidos
        File.WriteAllBytes(filePath, receivedData);
        PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({peerIp}:6000): Peça '{piece}' recebida");

        lock (myPiecesLock)
        {
            if (!_myPieces.Contains(piece))
                _myPieces.Add(piece);
        }
    }
    catch (Exception ex)
    {
        PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({peerIp}:6000): Erro ao requisitar peça '{piece}': {ex.Message}");
    }
    finally
    {
        lock (downloadingPiecesLock)
        {
            _downloadingPieces.Remove(piece);
        }
    }
}

void StartPeerServer()
{
    // Cria e inicia uma nova thread para executar o servidor TCP
    new Thread(() =>
    {
        // Cria um listener TCP que escuta em todas as interfaces de rede na porta 6000
        TcpListener listener = new(IPAddress.Any, 6000);
        listener.Start();
        PeerClientLogger.Log("Servidor TCP iniciado na porta 6000.");

        // Loop principal do servidor: aceita conexões indefinidamente
        while (true)
        {
            try
            {
                // Aceita uma conexão de cliente TCP
                using TcpClient client = listener.AcceptTcpClient();

                // Obtém o endereço IP e porta do cliente conectado
                if (client.Client?.RemoteEndPoint is not IPEndPoint remoteEndPoint)
                {
                    PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Interno): Falha ao obter o endereço remoto do cliente.");
                    continue; // Pula para a próxima iteração se não conseguir o endereço
                }

                // Extrai o IP do cliente para uso em logs
                string clientIp = remoteEndPoint.Address.ToString();

                // Obtém o stream de rede associado ao cliente
                using NetworkStream stream = client.GetStream();

                // Cria um buffer para ler os dados recebidos do cliente
                byte[] buffer = new byte[4096];

                // Lê os dados enviados pelo cliente (nome da peça/arquivo solicitado)
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                // Converte os bytes recebidos para string (nome do arquivo requisitado)
                string requestedPiece = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Loga a requisição recebida
                PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({clientIp}:{remoteEndPoint.Port}): Peça '{requestedPiece}' requisitada");

                // Monta o caminho completo do arquivo solicitado (adiciona .txt)
                string filePath = Path.Combine(folderPath, requestedPiece + ".txt");

                // Verifica se o arquivo existe
                if (File.Exists(filePath))
                {
                    // Lê o arquivo em bytes
                    byte[] fileData = File.ReadAllBytes(filePath);

                    // Envia o arquivo para o cliente pelo stream
                    stream.Write(fileData, 0, fileData.Length);

                    // Loga que a peça foi enviada
                    PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({clientIp}:{remoteEndPoint.Port}): Peça '{requestedPiece}' enviada");
                }
                else
                {
                    // Caso o arquivo não exista, envia mensagem de erro "PIECE_NOT_FOUND"
                    string errorMsg = "PIECE_NOT_FOUND";
                    byte[] errorBytes = Encoding.UTF8.GetBytes(errorMsg);
                    stream.Write(errorBytes, 0, errorBytes.Length);

                    // Loga que a peça não foi encontrada
                    PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({clientIp}:{remoteEndPoint.Port}): Peça '{requestedPiece}' requisitada não encontrada (PIECE_NOT_FOUND enviado)");
                }
            }
            catch (Exception ex)
            {
                // Em caso de erro, loga a exceção
                PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Interno): Falha ao enviar peça requisitada: " + ex.Message);
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
                PeerClientLogger.Log($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Interno): Erro ao atualizar peers do tracker: {ex.Message}");
            }
            Thread.Sleep(1000); // 1 segundo
        }
    })
    { IsBackground = true }.Start();
}

void StartHavePieceUpdater()
{
    new Thread(() =>
    {
        while (true)
        {
            Thread.Sleep(3000);
            SendHavePiece();
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
        currentPeersByPiece = _peersByPiece.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToList()
        );
    }
    lock (myPiecesLock)
    {
        // Verifica peças faltantes
        missingPieces = currentPeersByPiece.Keys.Where(p => !_myPieces.Contains(p)).ToList();
    }

    // Se houver peças, requisita
    if (missingPieces.Count > 0)
    {
        var tasks = new List<Task>();
        foreach (var piece in missingPieces)
        {
            bool deveBaixar = false;
            lock (downloadingPiecesLock)
            {
                lock (myPiecesLock)
                {
                    if (!_myPieces.Contains(piece) && !_downloadingPieces.Contains(piece))
                    {
                        _downloadingPieces.Add(piece);
                        deveBaixar = true;
                    }
                }
            }
            if (!deveBaixar)
                continue;

            // Seleciona peers por peça
            var peerIps = currentPeersByPiece[piece];
            if (peerIps == null || peerIps.Count == 0)
                continue;

            // Escolhe um peer aleatório, por peça escolhida
            var chosenPeer = peerIps[new Random().Next(peerIps.Count)];
            tasks.Add(Task.Run(() =>
            {
                // Requisita a peça do peer escolhido
                RequestPieceFromPeer(chosenPeer, piece, false);
            }));
        }
        // Cria as tasks e as aguarda
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