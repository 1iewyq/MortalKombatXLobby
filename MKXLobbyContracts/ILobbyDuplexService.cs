using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyContracts
{
    [ServiceContract(CallbackContract = typeof(ILobbyCallback))]
    public interface ILobbyDuplexService : ILobbyService
    {
        [OperationContract]
        void RegisterForCallbacks(string username);

        [OperationContract]
        void UnregisterFromCallbacks(string username);
    }
}
