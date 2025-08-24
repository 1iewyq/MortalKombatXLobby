using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKXLobbyModels
{
    [DataContract]
    public class SharedFile
    {
        [DataMember]
        public string FileName { get; set; }

        [DataMember]
        public byte[] FileContent { get; set; }

        [DataMember]
        public string SharedBy { get; set; }

        [DataMember]
        public DateTime SharedTime { get; set; }

        [DataMember]
        public string RoomName { get; set; }

        [DataMember]
        public string FileType { get; set; } 

        public override string ToString()
        {
            return FileName;
        }
    }
}
