using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class EventArgGameServer : EventArgs
    {
        private readonly GameServerRequest gsr;

        public EventArgGameServer(GameServerRequest gsr)
        {
            this.gsr = gsr;
        }

        public GameServerRequest GetGameServerRequest
        {
            get { return gsr; }
        }
    }
}
