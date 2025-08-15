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

                // Open hosts
                basicHost.Open();

                Console.WriteLine("Mortal Kombat X Lobby Server is running...");
                Console.WriteLine("HTTP Service: http://localhost:8080/LobbyService");
                Console.WriteLine("Press any key to stop the server...");

                Console.ReadKey();

                // Close hosts
                basicHost.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}
