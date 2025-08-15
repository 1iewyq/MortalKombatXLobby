using MKXLobbyModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyContracts
{
    [ServiceContract]
    public interface ILobbyCallback
    {
        [OperationContract(IsOneWay = true)]
        void OnMessageReceived(ChatMessage message);

        [OperationContract(IsOneWay = true)]
        void OnPlayerJoined(string username, string roomName);

        [OperationContract(IsOneWay = true)]
        void OnPlayerLeft(string username, string roomName);

        [OperationContract(IsOneWay = true)]
        void OnFileShared(SharedFile file);

        [OperationContract(IsOneWay = true)]
        void OnRoomCreated(LobbyRoom room);
    }

}
