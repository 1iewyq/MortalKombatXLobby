using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyModels
{
    [DataContract]
    public class Player
    {
        [DataMember]
        public string Username { get; set; }
        [DataMember]
        public string CurrentRoom { get; set; }
        [DataMember]
        public DateTime LoginTime { get; set; }
        [DataMember]
        public bool IsOnline { get; set; }
    }
}
