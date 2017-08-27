using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class HandleMessage
    {
        public HandleMessage() { }

        public void Execute(Bot bot, Socket socket, string code, string args)
        {
            switch(code)
            {
                case "0x0000":
                    RegisterBot(bot, socket, args);
                break;
            }
        }

        private void RegisterBot(Bot bot, Socket socket, string args)
        {
            IPEndPoint ipendpoint = ((IPEndPoint)socket.RemoteEndPoint);
            
            int index = bot.botManager.Servers.FindIndex(f => f.IP == ipendpoint.Address);

            if (index >= 0)
                return;

            GameServer gameserver = new GameServer(socket, args);
            bot.botManager.Servers.Add(gameserver);
        }
    }
}
