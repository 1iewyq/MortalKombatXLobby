using MKXLobbyContracts;
using System;
using System.ServiceModel;

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
