using System;
using System.Runtime.Serialization;

namespace MKXLobbyModels
{
    /* Represents a player in the gaming lobby system.
       Tracks player state including login status and current location.
       DataContract allows this object to be sent over WCF service calls. */
    [DataContract]
    public class Player
    {
        //Unique username that identifies this player
        //Must be unique across all logged-in players
        [DataMember]
        public string Username { get; set; }

        //Name of the room the player is currently in
        //Null if player is in the main lobby (not in any room)
        [DataMember]
        public string CurrentRoom { get; set; }

        //When the player logged in
        //Used for tracking session duration and activity
        [DataMember]
        public DateTime LoginTime { get; set; }

        //Whether the player is currently online (logged in)
        //True = online, False = offline
        [DataMember]
        public bool IsOnline { get; set; }
    }
}
