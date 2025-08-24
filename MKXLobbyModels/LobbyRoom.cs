using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyModels
{
    [DataContract]
    public class LobbyRoom
    {
        [DataMember]
        public string RoomName { get; set; }

        [DataMember]
        public List<string> Players { get; set; }

        [DataMember]
        public DateTime CreatedTime { get; set; }

        [DataMember]
        public string CreatedBy { get; set; }

        public LobbyRoom()
        {
            Players = new List<string>();
            CreatedTime = DateTime.Now;
        }

        public override string ToString()
        {
            return RoomName;
        }
    }

}
