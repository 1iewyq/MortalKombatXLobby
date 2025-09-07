using MKXLobbyContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyServer
{
    /* Main program class that starts the WCF server for the gaming lobby
       Sets up and configures the service host that listens for client connections */
    class Program
    {
        //main entry point of the application
        //sets up WCF service binding, creates service host, and starts listening for clients
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the MKX TCP Lobby Server");

            //configure NetTcpBinding for large file sharing
            var tcp = new NetTcpBinding();

            //set buffer sizes to 100MB to handle large file transfers
            tcp.MaxBufferSize = 104857600; //100MB
            tcp.MaxReceivedMessageSize = 104857600; //100MB

            //use buffered transfer mode (entire message buffered before processing)
            tcp.TransferMode = TransferMode.Buffered;

            //Configure XML reader quotas to handle large messages
            tcp.ReaderQuotas.MaxArrayLength = 104857600; //maxim array length in XML
            tcp.ReaderQuotas.MaxStringContentLength = 104857600; //maximum string content length
            tcp.ReaderQuotas.MaxDepth = 32; //maximum nesting depth of XML
            tcp.ReaderQuotas.MaxBytesPerRead = 4096; //Maximum bytes read at once
            tcp.ReaderQuotas.MaxNameTableCharCount = 16384; //max characters in name table

            //create the WCF service host using our LobbyService implementation
            var host = new ServiceHost(typeof(LobbyService));

            //add service endpoint - this is where clients will connect
            //net.tcp://localhost:8100/LobbyService = TCP protocol on localhost at port 8100
            host.AddServiceEndpoint(typeof(ILobbyService), tcp, "net.tcp://localhost:8100/LobbyService");

            //start the service host to begin listening for client connections
            host.Open();

            //display server status
            Console.WriteLine("Mortal Kombat X Lobby Server is ONLINE");
            Console.WriteLine("Press Enter to stop...");

            //wait for user input to terminate the server
            Console.ReadLine();

            //close the service host gracefully when pressing Enter
            host.Close();
        }

    }
}
