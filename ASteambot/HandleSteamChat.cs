using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot
{
    public class HandleSteamChat
    {
        private Bot bot;

        public HandleSteamChat(Bot bot)
        {
            this.bot = bot;
        }

        public void HandleMessage(SteamID partenar, string message)
        {
            switch(message)
            {
                case "help":
                    PrintHelp(partenar);
                break;

                case "SERVER":
                    PrintServer(partenar);
                break;

                default:
                    bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, "Sorry I don't understand you. Yet.");
                break;
            }            
        }

        public void PrintHelp(SteamID partenar)
        {
            SendChatMessage(partenar, "SERVER - Print all servers connected to me.");
        }

        public void PrintServer(SteamID partenar)
        {
            if (bot.botManager.Servers.Count > 0)
            {
                foreach (GameServer gs in bot.botManager.Servers)
                {
                    string serverLine = String.Format("{0} - {1}", gs.Name, gs.IP);
                    SendChatMessage(partenar, serverLine);
                }
            }
            else
            {
                SendChatMessage(partenar, "No servers connected to me :'( !");
            }
        }

        private void SendChatMessage(SteamID partenar, string message)
        {
            bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, message);
        }
    }
}
