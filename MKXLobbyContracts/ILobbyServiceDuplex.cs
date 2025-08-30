using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using MKXLobbyModels;

namespace MKXLobbyContracts
{
    [ServiceContract(CallbackContract = typeof(ILobbyServiceCallback))]
    public interface ILobbyServiceDuplex
    {
        //user management
        [OperationContract]
        bool LoginPlayer(string username);

        [OperationContract]
        void LogoutPlayer(string username);

        [OperationContract]
        List<string> GetOnlinePlayers();

        //room management
        [OperationContract]
        bool CreateRoom(string roomName, string createdBy, string username);

        [OperationContract]
        List<LobbyRoom> GetAvailableRooms();

        [OperationContract]
        bool JoinRoom(string roomName, string username);

        [OperationContract]
        void LeaveRoom(string username);

        [OperationContract]
        List<string> GetPlayersInRoom(string roomName);

        //messaging
        [OperationContract]
        void SendMessage(ChatMessage message);

        [OperationContract]
        List<ChatMessage> GetRoomMessages(string roomName);

        [OperationContract]
        List<ChatMessage> GetPrivateMessages(string username);

        //file sharing
        [OperationContract]
        bool ShareFile(SharedFile file);

        [OperationContract]
        List<SharedFile> GetSharedFiles(string roomName);

        [OperationContract]
        SharedFile DownloadFile(string fileName, string roomName);

        //duplex client update
        [OperationContract]
        void SubscribeToUpdate(string username);

        [OperationContract]
        void UnsubscribeFromUpdate(string username);
    }
}
