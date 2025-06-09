using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

UdpClient _udpServer = new(5000);
Dictionary<string, List<string>> _peerList = [];
string _clientAddress = string.Empty;
int _fileLength = 0;

IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
Console.WriteLine($"Tracker iniciado no endereço: {GetCurrentIP()}:5000\n");

while (true)
{
    byte[] data = _udpServer.Receive(ref remoteEP);
    string message = Encoding.UTF8.GetString(data);
    _clientAddress = $"{remoteEP.Address}";
    Console.WriteLine($"Recebido de {_clientAddress} # {message}\n");

    if (message == "JOIN_REQUEST")
    {
        JoinRequestHandler();
    }
    if (message.StartsWith("HAVE_PIECE"))
    {
        HavePieceHandler(message);
    }
}

void JoinRequestHandler()
{
    string peerAddress = $"{remoteEP.Address}";

    if (!_peerList.ContainsKey(peerAddress))
    {
        _peerList[peerAddress] = [];
    }

    StringBuilder responseBuilder = new("PEER_LIST|");
    foreach (var peer in _peerList)
    {
        if(peer.Key != $"{remoteEP.Address}")
        {
            string pieces = string.Join(",", peer.Value);
            responseBuilder.Append($"{peer.Key}[{pieces}];");
        }
    }

    var response = responseBuilder.ToString() == "PEER_LIST|" ? "PEER_LIST|NONE" : responseBuilder.ToString();
    response += _fileLength == 0 ? "" : $" SIZE{_fileLength}";

    byte[] responseData = Encoding.UTF8.GetBytes(response.ToString());
    _udpServer.Send(responseData, responseData.Length, remoteEP);
    Console.WriteLine($"Enviado para {peerAddress}: {response}\n");
}

void HavePieceHandler(string message)
{
    _peerList[_clientAddress] = message.Split("|")[1].Split(",").ToList();
    if (message.Contains("SEEDER"))
    {
        _fileLength = _peerList[_clientAddress].Count;
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
