using MKXLobbyContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyServerDuplex
{
    class ProgramD
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the MKX TCP Lobby Server");

            //configure NetTcpBinding for large file sharing
            var tcp = new NetTcpBinding();
            tcp.MaxBufferSize = 104857600; //100MB
            tcp.MaxReceivedMessageSize = 104857600; //100MB
            tcp.TransferMode = TransferMode.Buffered;
            tcp.ReaderQuotas.MaxArrayLength = 104857600;
            tcp.ReaderQuotas.MaxStringContentLength = 104857600;
            tcp.ReaderQuotas.MaxDepth = 32;
            tcp.ReaderQuotas.MaxBytesPerRead = 4096;
            tcp.ReaderQuotas.MaxNameTableCharCount = 16384;

            var host = new ServiceHost(typeof(LobbyServiceD));
            host.AddServiceEndpoint(typeof(ILobbyServiceDuplex), tcp, "net.tcp://localhost:8100/LobbyService");
            host.Open();
            Console.WriteLine("Mortal Kombat X Lobby Server is ONLINE");
            Console.WriteLine("Press Enter to stop...");
            Console.ReadLine();
            host.Close();
        }

    }
}
