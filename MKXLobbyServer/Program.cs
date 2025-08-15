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
            try
            {
                // Create service host for basic HTTP binding
                ServiceHost basicHost = new ServiceHost(typeof(LobbyService));

                // Configure HTTP endpoint
                BasicHttpBinding httpBinding = new BasicHttpBinding();
                httpBinding.MaxBufferSize = int.MaxValue;
                httpBinding.MaxReceivedMessageSize = int.MaxValue;
                httpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
                httpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;

                basicHost.AddServiceEndpoint(typeof(ILobbyService), httpBinding, "http://localhost:8080/LobbyService");

                // Create service host for duplex communication
                ServiceHost duplexHost = new ServiceHost(typeof(ILobbyDuplexService));

                // Configure TCP endpoint for duplex
                NetTcpBinding tcpBinding = new NetTcpBinding();
                tcpBinding.MaxBufferSize = int.MaxValue;
                tcpBinding.MaxReceivedMessageSize = int.MaxValue;
                tcpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
                tcpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;

                duplexHost.AddServiceEndpoint(typeof(ILobbyDuplexService), tcpBinding, "net.tcp://localhost:8081/LobbyDuplexService");

                // Open hosts
                basicHost.Open();
                duplexHost.Open();

                Console.WriteLine("Mortal Kombat X Lobby Server is running...");
                Console.WriteLine("HTTP Service: http://localhost:8080/LobbyService");
                Console.WriteLine("TCP Duplex Service: net.tcp://localhost:8081/LobbyDuplexService");
                Console.WriteLine("Press any key to stop the server...");

                Console.ReadKey();

                // Close hosts
                basicHost.Close();
                duplexHost.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}
