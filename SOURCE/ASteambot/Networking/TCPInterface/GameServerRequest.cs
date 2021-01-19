using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class GameServerRequest
    {
        public int ServerID { get; private set; }
        public int ModuleID { get; private set; }
        public int NetworkCode { get; private set; }
        public string Arguments { get; private set; }
        public Socket Socket { get; private set; }
        public bool isWebSocket { get; private set; }

        public GameServerRequest(Socket socket, string srvid, string code, string args, bool isWebSocket)
        {
            string[] idmid = srvid.Split(',');

            int tmpInt;
            Int32.TryParse(idmid[0], out tmpInt);
            ServerID = tmpInt;

            Int32.TryParse(idmid[1], out tmpInt);

            ModuleID = tmpInt;
            NetworkCode = Int32.Parse(code);
            Arguments = args;
            this.Socket = socket;
            this.isWebSocket = isWebSocket;
        }
    }
}
