using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

UdpClient _udpServer = new(5000);
Dictionary<string, List<string>> _peerList = [];
Dictionary<string, DateTime> _lastSeen = new();
string _clientAddress = string.Empty;

IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
Console.WriteLine($"Tracker iniciado no endereço: {GetCurrentIP()}:5000\n");

// Thread de limpeza de peers inativos
new Thread(() =>
{
    while (true)
    {
        Thread.Sleep(1000);
        var now = DateTime.Now;
        var toRemove = _lastSeen.Where(kv => (now - kv.Value).TotalSeconds > 5).Select(kv => kv.Key).ToList();
        foreach (var peer in toRemove)
        {
            _peerList.Remove(peer);
            _lastSeen.Remove(peer);
            Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | (Tracker): Removido peer inativo: {peer}");
        }
    }
})
{ IsBackground = true }.Start();

while (true)
{
    byte[] data = _udpServer.Receive(ref remoteEP);
    string message = Encoding.UTF8.GetString(data);
    _clientAddress = $"{remoteEP.Address}";

    if (message == "JOIN_REQUEST")
    {
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({_clientAddress}:{remoteEP.Port}): {message}");
        JoinRequestHandler();
    }
    else if (message.StartsWith("HAVE_PIECE"))
    {
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | <= ({_clientAddress}:{remoteEP.Port}): Atualização de peças");
        HavePieceHandler(message);
    }
    else if (message == "GET_PEERS")
    {
        // Atualiza o tempo do peer
        lock (_lastSeen)
        {
            _lastSeen[_clientAddress] = DateTime.Now;
        }
        GetPeersHandler();
    }
}

void JoinRequestHandler()
{
    string peerAddress = $"{remoteEP.Address}";

    if (!_peerList.ContainsKey(peerAddress))
    {
        _peerList[peerAddress] = [];
    }
    lock (_lastSeen)
    {
        _lastSeen[peerAddress] = DateTime.Now;
    }

    string response = BuildPeersResponse(excludePeer: peerAddress);
    byte[] responseData = Encoding.UTF8.GetBytes(response);
    _udpServer.Send(responseData, responseData.Length, remoteEP);
    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({peerAddress}:{remoteEP.Port}): Lista de peers e respectivos pedaços (Join Request)");
}

void GetPeersHandler()
{
    string peerAddress = $"{remoteEP.Address}";
    lock (_lastSeen)
    {
        _lastSeen[peerAddress] = DateTime.Now;
    }
    string response = BuildPeersResponse(excludePeer: null);
    byte[] responseData = Encoding.UTF8.GetBytes(response);
    _udpServer.Send(responseData, responseData.Length, remoteEP);
    Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | => ({peerAddress}:{remoteEP.Port}): Lista atualizada de peers e respectivos pedaços");
}

string BuildPeersResponse(string? excludePeer)
{
    StringBuilder responseBuilder = new("PEER_LIST|");
    foreach (var peer in _peerList)
    {
        if (excludePeer == null || peer.Key != excludePeer)
        {
            string pieces = string.Join(",", peer.Value);
            responseBuilder.Append($"{peer.Key}[{pieces}];");
        }
    }
    var response = responseBuilder.ToString() == "PEER_LIST|" ? "PEER_LIST|NONE" : responseBuilder.ToString();
    return response;
}

void HavePieceHandler(string message)
{
    _peerList[_clientAddress] = message.Split("|")[1].Split(",").ToList();
    lock (_lastSeen)
    {
        _lastSeen[_clientAddress] = DateTime.Now;
    }
}

#region Helpers
static string GetCurrentIP()
{
    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (ni.OperationalStatus == OperationalStatus.Up &&
            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
            ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
        {
            IPInterfaceProperties ipProps = ni.GetIPProperties();

            // Verifica se há gateway padrão (sugere acesso à internet)
            bool hasGateway = ipProps.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

            if (hasGateway)
            {
                foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.Address.ToString();
                    }
                }
            }
        }
    }

    return "127.0.0.1"; // fallback
}
#endregion