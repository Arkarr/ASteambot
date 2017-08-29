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
        private readonly int id;
        private readonly int code;
        private readonly string args;
        private readonly Socket socket;

        //private readonly int port;
        //private readonly IPAddress ip;

        public EventArgGameServer(Socket socket, string id, string code, string args)
        {
            this.id = Int32.Parse(id);
            this.code = Int32.Parse(code);
            this.args = args;
            this.socket = socket;

            /*IPEndPoint ipendpoint = ((IPEndPoint)socket.RemoteEndPoint);

            ip = ipendpoint.Address;
            port = ipendpoint.Port;*/
        }

        public int GetServerID
        {
            get { return id; }
        }

        public int GetNetworkCode
        {
            get { return code; }
        }

        public string GetArguments
        {
            get { return args; }
        }

        public Socket GetSocket
        {
            get { return socket; }
        }

        /*public IPAddress GetIP
        {
            get { return ip; }
        }
        public int GetPort
        {
            get { return port; }
        }*/
    }
}
