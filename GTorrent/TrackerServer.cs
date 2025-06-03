using System.Net;
using System.Net.Sockets;
using System.Text;

public class TrackerServer
{
    private UdpClient udpServer;
    private Dictionary<string, List<int>> peerData = new(); // IP: List of pieces

    public void Start()
    {
        int port = 5000;
        udpServer = new UdpClient(port);
        Console.WriteLine($"Tracker iniciado na porta {port}.");

        while (true)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            Console.WriteLine($"Endereço Tracker: {GetCurrentIP()}:{port}");
            byte[] data = udpServer.Receive(ref remoteEP);
            string message = Encoding.UTF8.GetString(data);

            if (message.StartsWith("REQUEST_PEERS"))
            {
                string response = SerializePeers();
                byte[] respBytes = Encoding.UTF8.GetBytes(response);
                udpServer.Send(respBytes, respBytes.Length, remoteEP);
            }
            else if (message.StartsWith("UPDATE_PIECES"))
            {
                string[] parts = message.Split('|');
                string ip = parts[1];
                List<int> pieces = new();
                foreach (var p in parts[2].Split(','))
                    pieces.Add(int.Parse(p));
                peerData[ip] = pieces;
                Console.WriteLine($"Atualizado: {ip} => [{string.Join(",", pieces)}]");
            }
        }
    }

    private string SerializePeers()
    {
        StringBuilder sb = new();
        foreach (var entry in peerData)
        {
            sb.Append($"{entry.Key}:{string.Join(",", entry.Value)}|");
        }
        return sb.ToString().TrimEnd('|');
    }

    public static string GetCurrentIP()
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
}