using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyModels
{
    [DataContract]
    public class ChatMessage
    {
        [DataMember]
        public string From { get; set; }

        [DataMember]
        public string To { get; set; }  // null for public messages

        [DataMember]
        public string Content { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }

        [DataMember]
        public string RoomName { get; set; }

        [DataMember]
        public bool IsPrivate { get; set; }
    }
}
