using System;
using System.Net.Sockets;
using System.Net;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Modo de execução:");
        Console.WriteLine("1 - Tracker");
        Console.WriteLine("2 - Peer");
        Console.WriteLine("3 - File Manager");

        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                new TrackerServer().Start();
                break;
            case "2":
                Console.Write("Digite o Peer ID: ");
                string peerId = Console.ReadLine();
                //new PeerClient(peerId).Start();
                break;
            default:
                Console.WriteLine("Opção inválida.");
                break;
        }
    }
}
