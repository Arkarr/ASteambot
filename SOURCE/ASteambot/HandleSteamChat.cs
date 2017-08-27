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
            string command = message.Split(' ')[0];
            switch(command)
            {
                case "help":
                    PrintHelp(partenar);
                break;

                case "SERVER":
                    PrintServer(partenar);
                break;

                case "HOOKCHAT":
                    HookGameServerChat(partenar, message);
                break;

                default:
                    bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, "Sorry I don't understand you. Yet.");
                break;
            }            
        }

        public void PrintHelp(SteamID partenar)
        {
            SendChatMessage(partenar, "SERVER - Print all servers connected to me.");
            SendChatMessage(partenar, "HOOKCHAT - Listen to what's being sent in the game server.");
        }

        public void PrintServer(SteamID partenar)
        {
            if (bot.botManager.Servers.Count > 0)
            {
                SendChatMessage(partenar, "---------------------------");
                foreach (GameServer gs in bot.botManager.Servers)
                {
                    string serverLine = String.Format("{0} - {1}:{2}", gs.Name, gs.IP, gs.Port);
                    SendChatMessage(partenar, serverLine);
                }
                SendChatMessage(partenar, "Number of registred servers : " + bot.botManager.Servers.Count);
                SendChatMessage(partenar, "---------------------------");
            }
            else
            {
                SendChatMessage(partenar, "No servers connected to me :'( !");
            }
        }

        public void HookGameServerChat(SteamID partenar, string message)
        {
            message = message.Replace("HOOKCHAT ", String.Empty);

            int serverID = -1;
            Int32.TryParse(message, out serverID);

            if(serverID > bot.botManager.Servers.Count || serverID <= 0)
            {
                SendChatMessage(partenar, "Invalid game server ID. Use SERVER to get game server ID.");
                return;
            }

            GameServer gs = bot.botManager.Servers[serverID - 1];

            SendChatMessage(partenar, "Connecting to server...");

            gs.SendMessage("0x0001");
        }

        private void SendChatMessage(SteamID partenar, string message)
        {
            bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, message);
        }
    }
}
