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

        private Dictionary<SteamID, int> ChatListener;

        public HandleSteamChat(Bot bot)
        {
            this.bot = bot;
            ChatListener = new Dictionary<SteamID, int>();
        }

        public void HandleMessage(SteamID partenar, string message)
        {
            string command = message.Split(' ')[0];
            message = message.Replace(command+" ", "");

            if (!command.Equals("STOPHOOK") && ChatListener.ContainsKey(partenar))
            {
                SendMessageToGameServer(-2, partenar, message);
                return;
            }

            switch(command)
            {
                case "help":
                    PrintHelp(partenar);
                break;

                case "SERVER":
                    PrintServer(partenar);
                break;

                case "HOOKCHAT":
                    HookGameServerChat(-2, partenar, message);
                break;

                case "STOPHOOK":
                    StopHook(-2, partenar);
                break;

                case "EXEC":
                    ExecuteServerCommand(partenar, message);
                break;

                default:
                    bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, "Sorry I don't understand you. Yet.");
                break;
            }            
        }

        private void SendMessageToGameServer(int moduleID, SteamID partenar, string message)
        {
            int serverID = ChatListener[partenar];
            GameServer gs = bot.GetServerByID(serverID);

            string name = bot.SteamFriends.GetFriendPersonaName(partenar).Replace('|', ' ').Trim(' ');
            string data = string.Format("{0} : {1}", name, message);
            gs.Send(moduleID, Networking.NetworkCode.ASteambotCode.Simple, data);
        }

        public void StopHook(int moduleID, SteamID partenar)
        {
            int serverID = ChatListener[partenar];
            ChatListener.Remove(partenar);
            SendChatMessage(partenar, "Disconnecting to server...");

            foreach (KeyValuePair<SteamID, int> value in ChatListener)
            {
                if (value.Value == serverID && value.Key != partenar)
                    return;
            }

            GameServer server = bot.botManager.Servers[serverID-1];
            server.Send(moduleID, Networking.NetworkCode.ASteambotCode.Unhookchat, "");
        }
        
        public void UnhookChat(int serverID, string arguments)
        {
            foreach (KeyValuePair<SteamID, int> value in ChatListener)
            {
                if (value.Value != serverID)
                    continue;


            }
        }

        private void ExecuteServerCommand(SteamID partenar, string message)
        {
            int id = 0;
            if (Int32.TryParse(message.Split(' ')[0], out id))
            {

                string cmd = message.Replace(message.Split(' ')[0], "");

                if (id < 0 || id > bot.botManager.Servers.Count)
                {
                    SendChatMessage(partenar, "Invalid server ID '" + id + "' specified !");
                    SendChatMessage(partenar, "Use SERVER command to get a valid server ID !");
                    return;
                }

                GameServer gs = bot.botManager.Servers[id];
                gs.Send(-2, Networking.NetworkCode.ASteambotCode.ExecuteCommand, cmd);

                SendChatMessage(partenar, "Command '" + cmd + "' sent to server '" + gs.Name + "' !");
            }
            else
            {
                SendChatMessage(partenar, "Invalid server ID '" + message.Split(' ')[0] + "' specified !");
                SendChatMessage(partenar, "Use SERVER command to get a valid server ID !");
            }
        }

        public void PrintHelp(SteamID partenar)
        {
            SendChatMessage(partenar, "help - Print this message.");
            SendChatMessage(partenar, "SERVER - Print all servers connected to me.");
            SendChatMessage(partenar, "HOOKCHAT - Listen to what's being sent in the game server.");
            SendChatMessage(partenar, "STOPHOOK - Stop listening to what's being sent in the game server.");
            SendChatMessage(partenar, "EXEC - Execute the specified command in the targeted server.");
        }

        public void PrintServer(SteamID partenar)
        {
            int id = 1;
            if (bot.botManager.Servers.Count > 0)
            {
                SendChatMessage(partenar, "---------------------------");
                foreach (GameServer gs in bot.botManager.Servers)
                {
                    string serverLine = String.Format("[{0}] {1} - {2}:{3}", id, gs.Name, gs.IP, gs.Port);
                    SendChatMessage(partenar, serverLine);
                    id++;
                }
                SendChatMessage(partenar, "Number of registred servers : " + bot.botManager.Servers.Count);
                SendChatMessage(partenar, "---------------------------");
            }
            else
            {
                SendChatMessage(partenar, "No servers connected to me :'( !");
            }
        }

        public void ServerRemoved(int oldServerID)
        {
            foreach (KeyValuePair<SteamID, int> value in ChatListener)
            {
                if (value.Value == oldServerID)
                    SendChatMessage(value.Key, "Disconnected from server ! Message won't be transfered anymore...");
            }
            
            for (int i = ChatListener.Count - 1; i >= 0; i--)
            {
                KeyValuePair<SteamID,int> listener = ChatListener.ElementAt(i);

                if (listener.Value == oldServerID)
                    ChatListener.Remove(listener.Key);
            }
        }

        public void ServerMessage(int serverid, string message)
        {
            foreach(KeyValuePair<SteamID, int> value in ChatListener)
            {
                if (value.Value == serverid)
                    SendChatMessage(value.Key, message);
            }
        }

        public void HookGameServerChat(int moduleID, SteamID partenar, string message)
        {
            if(ChatListener.ContainsKey(partenar))
            {
                SendChatMessage(partenar, "You are already hooking a chat ! Use : STOPHOOK");
                return;
            }

            int serverID = -1;
            Int32.TryParse(message, out serverID);

            if(serverID > bot.botManager.Servers.Count || serverID <= 0)
            {
                SendChatMessage(partenar, "Invalid game server ID. Use SERVER to get game server ID.");
                return;
            }

            GameServer gs = bot.botManager.Servers[serverID - 1];

            ChatListener.Add(partenar, gs.ServerID);

            SendChatMessage(partenar, "Connecting to server...");
            
            gs.Send(moduleID, Networking.NetworkCode.ASteambotCode.HookChat, "");
        }

        private void SendChatMessage(SteamID partenar, string message)
        {
            bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, message);
        }
    }
}
