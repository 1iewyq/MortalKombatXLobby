using System.Collections.Generic;
using System.ServiceModel;
using MKXLobbyModels;

namespace MKXLobbyContracts
{
    /* Callback interface for duplex WCF communication.
       Allows server to push updates directly to clients (real-time notifications).
       This eliminates need for client polling and provides immediate updates.
       All methods are one-way (fire-and-forget) for better performance. */
    [ServiceContract]
    public interface ILobbyServiceCallback
    {
        //Notifies client that the list of available rooms has been updated
        //Called when rooms are created or deleted
        [OperationContract(IsOneWay = true)]
        void OnRoomListUpdated(List<LobbyRoom> rooms);

        //Notifies client that data in a specific room has changed
        //Called when messages, players, or files in the room are updated
        [OperationContract(IsOneWay = true)]
        void OnRoomDataUpdated(string roomName, List<ChatMessage> messages, List<string> players, List<SharedFile> files);

        //Notifies client that a new private message has been received
        //Called when another user sends a private message to this client
        [OperationContract(IsOneWay = true)]
        void OnPrivateMessageReceived(ChatMessage message);

        //Notifies client that a player has joined a room they are in
        //Called when any player joins the same room as this client
        [OperationContract(IsOneWay = true)]
        void OnPlayerJoinedRoom(string roomName, string username);

        //Notifies client that a player has left a room they are in
        //Called when any player leaves the same room as this client
        [OperationContract(IsOneWay = true)]
        void OnPlayerLeftRoom(string roomName, string username);
    }
}
