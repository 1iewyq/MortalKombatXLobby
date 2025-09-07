using System;
using System.Runtime.Serialization;

namespace MKXLobbyModels
{
    /* Represents a file shared by a player in a lobby room.
       Contains both the file metadata and the actual file content as binary data.
       DataContract allows this object to be sent over WCF service calls. */
    [DataContract]
    public class SharedFile
    {
        //Original filename of the shared file
        //Used for display purposes and when downloading the file
        [DataMember]
        public string FileName { get; set; }

        //The complete file content as binary data (byte array)
        //This allows any type of file to be stored and transmitted
        //Large files may impact performance due to memory usage
        [DataMember]
        public byte[] FileContent { get; set; }

        //Username of the player who shared the file
        //Used for attribution and display purposes
        [DataMember]
        public string SharedBy { get; set; }

        //When the file was shared
        //Used for sorting files
        [DataMember]
        public DateTime SharedTime { get; set; }

        //Name of the room where this file was shared
        //Files are only accessible to players in the same room
        [DataMember]
        public string RoomName { get; set; }

        //Type category of the file (e.g., "iamge", "text", "other")
        //Used to determine how to handle/display the file
        //Helps with filtering and organizing files
        [DataMember]
        public string FileType { get; set; }

        //Override ToString to display filename in UI lists
        //This makes the file object show its name when added to ListBox controls
        public override string ToString()
        {
            return FileName;
        }
    }
}
