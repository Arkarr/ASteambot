using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class HandleMessage
    {
        public HandleMessage() { }

        public void Execute(Bot bot, IPAddress ip, int port, string code, string args)
        {
            switch(code)
            {
                case "0x0000":
                    RegisterBot(bot, ip, port, args);
                break;
            }
        }

        private void RegisterBot(Bot bot, IPAddress ip, int port, string args)
        {
            int index = bot.botManager.Servers.FindIndex(f => f.IP == ip);

            if (index >= 0)
                return;

            GameServer gameserver = new GameServer(args, ip, port);
            bot.botManager.Servers.Add(gameserver);
        }
    }
}
