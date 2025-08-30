using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using MKXLobbyModels;

namespace MKXLobbyContracts
{
    [ServiceContract]
    public interface ILobbyServiceCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnRoomListUpdated(List<LobbyRoom> rooms);

        [OperationContract(IsOneWay = true)]
        void OnRoomDataUpdated(string roomName, List<ChatMessage> messages, List<string> players, List<SharedFile> files);

        [OperationContract(IsOneWay = true)]
        void OnPrivateMessageReceived(ChatMessage message);

        [OperationContract(IsOneWay = true)]
        void OnPlayerJoinedRoom(string roomName, string username);

        [OperationContract(IsOneWay = true)]
        void OnPlayerLeftRoom(string roomName, string username);
    }
}
