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

        public GameServerRequest(Socket socket, string srvid, string code, string args)
        {
            string[] idmid = srvid.Split(',');
            this.ServerID = Int32.Parse(idmid[0]);
            this.ModuleID = Int32.Parse(idmid[1]);
            this.NetworkCode = Int32.Parse(code);
            this.Arguments = args;
            this.Socket = socket;
        }
    }
}
