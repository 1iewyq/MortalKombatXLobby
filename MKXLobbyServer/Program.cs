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

            // Create TCP binding with large limits (like your HTTP binding had)
            var tcpBinding = new NetTcpBinding(SecurityMode.None)
            {
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue
            };
            tcpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            tcpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;

            // Host your LobbyService class
            using (var host = new ServiceHost(typeof(LobbyService)))
            {
                // Bind service endpoint — 0.0.0.0 = listen on all interfaces
                host.AddServiceEndpoint(typeof(ILobbyService),
                    tcpBinding,
                    "net.tcp://10.1.218.250:8100/LobbyService");

                // Open service
                host.Open();
                Console.WriteLine("MKX TCP Lobby Server is ONLINE");
                Console.WriteLine("Listening on net.tcp://0.0.0.0:8100/LobbyService");
                Console.WriteLine("Press Enter to stop...");
                Console.ReadLine();

                host.Close();
            }
        }

    }
}
