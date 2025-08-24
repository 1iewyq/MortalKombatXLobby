using MKXLobbyContracts;
using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the MKX TCP Lobby Server");

            var tcp = new NetTcpBinding();

            var host = new ServiceHost(typeof(LobbyService));
            host.AddServiceEndpoint(typeof(ILobbyService), tcp, "net.tcp://localhost:8100/LobbyService");
            host.Open();
            Console.WriteLine("Mortal Kombat X Lobby Server is ONLINE");
            Console.WriteLine("Press Enter to stop...");
            Console.ReadLine();

            host.Close();
        }

    }
}
