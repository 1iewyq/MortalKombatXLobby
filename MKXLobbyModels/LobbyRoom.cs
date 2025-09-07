using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MKXLobbyModels
{
    /* Represents a chat room in the gaming lobby where players can gather.
       Each room has a unique name and maintains a list of current players.
       DataContract allows this object to be sent over WCF service calls. */
    [DataContract]
    public class LobbyRoom
    {
        //Unique name of the room
        //Players use this name to join the room
        [DataMember]
        public string RoomName { get; set; }

        //List of usernames of players currently in the room
        //Used to track who can participate in room conversations
        [DataMember]
        public List<string> Players { get; set; }

        //When this room was created
        //Used for sorting rooms and tracking room age
        [DataMember]
        public DateTime CreatedTime { get; set; }

        //Username of the player who created the room
        //Used for tracking room ownership
        [DataMember]
        public string CreatedBy { get; set; }

        //COnstructor - initializes a new room with empty player list and current time
        public LobbyRoom()
        {
            Players = new List<string>();   //start with no players
            CreatedTime = DateTime.Now;     //set creation time to now
        }

        //Override ToString to return the room name for easy display in UI
        //This makes the room object show its name when added to ListBox controls
        public override string ToString()
        {
            return RoomName;
        }
    }

}
