using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using MKXLobbyModels;

namespace MKXLobbyContracts
{
    /* Main Duplex WCF service interface that defines all operations available to clients
       This interface is implemented by the server and used by clients to communicate
       Allows server to push updates directly to clients (real-time notifications).
       Defines a duplex service contract for lobby management with callback capabilities.*/
    [ServiceContract(CallbackContract = typeof(ILobbyServiceCallback))]
    public interface ILobbyServiceDuplex
    {
        /* --- USER MANAGEMENT --- */

        //Attempts to log in a player with the given username
        //Server checks if username is unique and not already logged in
        //Returns true if login successful, false if username taken
        [OperationContract]
        bool LoginPlayer(string username);

        //Logs out a player and removes them from any room they are in
        //Cleans up player session and notifies other players in the room
        [OperationContract]
        void LogoutPlayer(string username);

        //Gets list of all currently online players
        //Used to display who is available in the lobby
        //Returns list of usernames of all online players
        [OperationContract]
        List<string> GetOnlinePlayers();

        /* --- ROOM MANAGEMENT --- */

        //Creates a new room with the specified name
        //Room name must be unique
        //Returns true if room created successfully, false if name taken
        [OperationContract]
        bool CreateRoom(string roomName, string createdBy, string username);

        //Gets all currently available rooms
        //Used by clients to display list of rooms to join
        //Returns list of LobbyRoom objects
        [OperationContract]
        List<LobbyRoom> GetAvailableRooms();

        //Adds a player to an existing room
        //Player is automatically removed from any previous room
        //Returns true if join successful, false if room does not exist
        [OperationContract]
        bool JoinRoom(string roomName, string username);

        //Removes a player from their current room
        //If room becomes empty, it may be deleted
        [OperationContract]
        void LeaveRoom(string username);

        //Gets list of all players currently in the specified room
        //Used to display who is in the room
        //Returns list of usernames of players in the room
        [OperationContract]
        List<string> GetPlayersInRoom(string roomName);

        /* --- MESSAGING OPERATIONS --- */

        //Sends a chat message (either public or private)
        //Server stores message and makes it available for retrieval
        [OperationContract]
        void SendMessage(ChatMessage message);

        //Gets all messages for a specific room
        //Private messages are filtered out - only room participants see them
        //Returns list of ChatMessage objects for the room
        [OperationContract]
        List<ChatMessage> GetRoomMessages(string roomName);

        //Gets all private messages sent to or from the specified user
        //Returns messages where user is either sender or recipient
        //Returns list of ChatMessage objects
        [OperationContract]
        List<ChatMessage> GetPrivateMessages(string username);

        /* --- FILE SHARING OPERATIONS --- */

        //Shares a file in a lobby room
        //File content is stored on server and made available to room participants
        // Returns true if file shared successfully, false on error
        [OperationContract]
        bool ShareFile(SharedFile file);

        //Gets list of all files shared in the specified room
        //Used to display available shared files to room participants
        // Returns list of SharedFile objects
        [OperationContract]
        List<SharedFile> GetSharedFiles(string roomName);

        //Downloads a specific shared file by name from a room
        //Returns complete file data for client to open
        // Returns null if file not found
        [OperationContract]
        SharedFile DownloadFile(string fileName, string roomName);

        /* --- DUPLEX CLIENT UPDATE OPERATIONS --- */
        //Subscribes a client to receive real-time updates (callbacks) from the service.
        //Typically called after successful login.
        [OperationContract]
        void SubscribeToUpdate(string username);

        //Unsubscribes a client from receiving real-time updates.
        //Typically called before logout or when client disconnects.
        [OperationContract]
        void UnsubscribeFromUpdate(string username);
    }
}
