using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot
{
    public class GameServer
    {
        public string Name { get; set; }
        public IPAddress IP { get; private set; }
        public int Port { get; private set; }

        private Socket socket;

        public GameServer(Socket socket, string ipportname)
        {
            string[] srvinfos = ipportname.Split('|');
            Name = srvinfos[2];
            IP = IPAddress.Parse(srvinfos[0]);
            Port = Int32.Parse(srvinfos[1]);
            this.socket = socket;
        }

        public void SendMessage(string message)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            socket.Send(bytes);
        }
    }
}
