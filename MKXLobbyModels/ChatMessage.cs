using System;
using System.Runtime.Serialization;

namespace MKXLobbyModels
{
    /* Represents a chat message in the gaming lobby system.
       Can be either a public room message or private message between two users.
       DataContract allows this object to be sent over WCF service calls */
    [DataContract]
    public class ChatMessage
    {
        //Username of the player who sent the message
        [DataMember]
        public string From { get; set; }

        //Username of the player who should receive the message
        //For public messages, this is null (everyone in room receives it)
        //For private messages, this contains the recipient's username
        [DataMember]
        public string To { get; set; }

        //The actual text content of the message
        [DataMember]
        public string Content { get; set; }

        //When the message was sent
        //Used for sorting messages in sequence and display purposes
        [DataMember]
        public DateTime Timestamp { get; set; }

        //Name of the chat room where the message was sent
        //For private messages, this may be null or the room where conversation started
        [DataMember]
        public string RoomName { get; set; }

        //Whether this is a private message between two users
        //True = private message (only sender and recipient can see)
        //False = public message (all players in room can see)
        [DataMember]
        public bool IsPrivate { get; set; }
    }
}
