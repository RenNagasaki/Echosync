using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echosync_Server
{
    public class UserState
    {
        public string WebSocketId {  get; set; } = "";
        public string NetworkId { get; set; } = "";
        public string IpAdress { get; set; } = "";
        public string UserName { get; set; } = "";
        public string NpcId { get; set; } = "";
        public string Channel { get; set; } = "";
        public int DialogueCount { get; set; } = 0;
        public bool Ready { get; set; } = false;

        public UserState(string webSocketId, string networkId, string ipAdress, string userName, string channel) 
        { 
            this.WebSocketId = webSocketId;
            this.NetworkId = networkId;
            this.IpAdress = ipAdress;
            this.UserName = userName; 
            this.Channel = channel;
        }
    }
}
