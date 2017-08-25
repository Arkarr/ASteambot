using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot
{
    public class GameServer
    {
        public string Name { get; set; }
        public IPAddress IP { get; private set; }
        public int Port { get; private set; }

        public GameServer(string name, IPAddress ip, int port)
        {
            Name = name;
            IP = ip;
            Port = port;
        }
    }
}
