using ASteambot.Translation;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ASteambot.SteamProfile;

namespace ASteambot
{
    public class HandleSteamChat
    {
        private Bot bot;

        public HandleSteamChat(Bot bot)
        {
            this.bot = bot;
        }

        public void HandleMessage(SteamID partenar, string msg)
        {
            if (!bot.Config.IsAdmin(partenar))
            {
                PrintChatMessage(partenar, msg);
                return;
            }

            string command = msg.Split(' ')[0];
            string message = msg.Replace(command+" ", "");

            if (!command.Equals("STOPHOOK") && !command.Equals("UNHOOK") && bot.ChatListener.ContainsKey(partenar))
            {
                SendMessageToGameServer(-2, partenar, msg);
                return;
            }

            switch(command)
            {
                case "help":
                case "HELP":
                    PrintHelp(partenar);
                    break;

                case "SERVER":
                    PrintServer(partenar, command);
                break;

                case "HOOKCHAT":
                case "HOOKVOICE":
                    HookGameServerChat(Networking.NetworkCode.MSG_FOR_ALL_MODULE, partenar, message);
                break;

                case "UNHOOK":
                case "STOPHOOK":
                    StopHook(Networking.NetworkCode.MSG_FOR_ALL_MODULE, partenar);
                break;

                case "EXEC":
                    ExecuteServerCommand(partenar, message);
                break;

                default:
                    //bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, "Sorry I don't understand you. Yet.");
                    PrintChatMessage(partenar, msg);
                break;
            }


        }

        private void SendMessageToGameServer(int moduleID, SteamID partenar, string message)
        {
            int serverID = bot.ChatListener[partenar];
            
            string name = bot.SteamFriends.GetFriendPersonaName(partenar).Replace('|', ' ').Trim(' ');
            string data = string.Format("{0} : {1}", name, message);
            bot.Manager.Send(serverID, moduleID, Networking.NetworkCode.ASteambotCode.Simple, data);
            /*}
            else
            {
                SendChatMessage(partenar, "Unable to deliver this message to the game server, he is not connected.");
            }*/
        }

        public void StopHook(int moduleID, SteamID partenar)
        {
            if(partenar == null)
            {
                Program.PrintErrorMessage("partenar is NULL ?!");
                return;
            }
            
            if(bot.ChatListener == null)
            {
                Program.PrintErrorMessage("ChatListener is NULL ?!");
                return;
            }

            if (bot.ChatListener.ContainsKey(partenar))
            {
                int serverID = bot.ChatListener[partenar];
                bot.ChatListener.Remove(partenar);
                //SendChatMessage(partenar, "Disconnecting from server...");
                PrintChatMessage(partenar, "STOPHOOK");

                foreach (KeyValuePair<SteamID, int> value in bot.ChatListener)
                {
                    if (value.Value == serverID && value.Key != partenar)
                        return;
                }

                bot.Manager.Send(serverID, moduleID, Networking.NetworkCode.ASteambotCode.Unhookchat, "");

                PrintChatMessage(partenar, "DONE");
            }
            else
            {
                //SendChatMessage(partenar, "Not listening to any servers. Use HOOKCHAT first !");
                PrintChatMessage(partenar, "STOPHOOK_NOT_CONNECTED");
            }
        }

        public void PrintChatMessage(SteamID partenar, string message, string[] data = null)
        {
            string sentences = "";
            data = data ?? new string[0];

            try
            {
                Infos spi = bot.GetSteamProfileInfo(partenar);
            }
            catch(Exception ex)
            {
                Program.PrintErrorMessage("Error while loading the profile of '" + partenar + "' ... Sorry!");
                Program.PrintErrorMessage(ex.ToString());
                Program.PrintErrorMessage("As a result, I'll use the 'en' language !");
            }

            if(bot.Config.IsAdmin(partenar))
                sentences = String.Format(bot.TranslationAdmins.GetSentence(message, "en"), data);
            else
                sentences = String.Format(bot.TranslationPublic.GetSentence(message, "en"), data);


            foreach (string s in sentences.Split(new string[] { "\\n" }, StringSplitOptions.None))
            {
                //Timeout to prevent spam here.
                SendChatMessage(partenar, s);
            }
        }

        private void ExecuteServerCommand(SteamID partenar, string message)
        {
            int id = 0;
            if (Int32.TryParse(message.Split(' ')[0], out id))
            {
                string cmd = message.Replace(message.Split(' ')[0], "");

                if (id < 0)
                {
                    //SendChatMessage(partenar, "Invalid server ID '" + id + "' specified !");
                    string[] param = { id.ToString() };
                    PrintChatMessage(partenar, "INVALID_SERVER_ID", param);
                    //SendChatMessage(partenar, "Use SERVER command to get a valid server ID !");
                    PrintChatMessage(partenar, "SERVER_INVALID_ID");
                    return;
                }

                GameServer gs = bot.Manager.GetServerByID(id);
                if (gs != null)
                {
                    bot.Manager.Send(id, -2, Networking.NetworkCode.ASteambotCode.ExecuteCommand, cmd);
                    //SendChatMessage(partenar, "Command '" + cmd + "' sent to server '" + gs.Name + "' !");
                    string[] param = { cmd, gs.Name };
                    PrintChatMessage(partenar, "EXEC_DONE", param);
                }
                else
                {
                    //SendChatMessage(partenar, "No server found with id " + id + " ! No command sent.");
                    string[] param = { id.ToString() };
                    PrintChatMessage(partenar, "INVALID_SERVER_ID", param);
                    PrintChatMessage(partenar, "SERVER_INVALID_ID");
                }
            }
            else
            {
                //SendChatMessage(partenar, "Invalid server ID '" + message.Split(' ')[0] + "' specified !");
                //SendChatMessage(partenar, "Use SERVER command to get a valid server ID !");

                string[] param = { id.ToString() };
                PrintChatMessage(partenar, "INVALID_SERVER_ID", param);
                PrintChatMessage(partenar, "SERVER_INVALID_ID");
            }
        }

        public void PrintHelp(SteamID partenar)
        {
            /*SendChatMessage(partenar, "help - Print this message.");
            SendChatMessage(partenar, "SERVER - Print all servers connected to me.");
            SendChatMessage(partenar, "HOOKCHAT - Listen to what's being sent in the game server.");
            SendChatMessage(partenar, "STOPHOOK - Stop listening to what's being sent in the game server.");
            SendChatMessage(partenar, "EXEC - Execute the specified command in the targeted server.");*/
            PrintChatMessage(partenar, "HELP");
        }

        public void PrintServer(SteamID partenar, string cmd)
        {
            if (bot.Manager.Servers.Count > 0)
            {
                foreach (GameServer gs in bot.Manager.Servers)
                {
                    string serverLine = String.Format("[{0}] {1} - {2}:{3}", gs.ServerID, gs.Name, gs.IP, gs.Port);
                    SendChatMessage(partenar, serverLine);
                }
                string[] param = { bot.Manager.Servers.Count.ToString() };
                //SendChatMessage(partenar, "Number of registred servers : {0}" + bot.Manager.Servers.Count);
                PrintChatMessage(partenar, cmd, param);
            }
            else
            {
                //SendChatMessage(partenar, "No servers connected to me :'( !");
                PrintChatMessage(partenar, "NO_SERVER_CONNECTED");
            }
        }

        public void ServerRemoved(int oldServerID)
        {
            foreach (KeyValuePair<SteamID, int> value in bot.ChatListener)
            {
                if (value.Value == oldServerID)
                    SendChatMessage(value.Key, "Disconnected from server ! Message won't be transfered anymore...");
            }
            
            for (int i = bot.ChatListener.Count - 1; i >= 0; i--)
            {
                KeyValuePair<SteamID,int> listener = bot.ChatListener.ElementAt(i);

                if (listener.Value == oldServerID)
                    bot.ChatListener.Remove(listener.Key);
            }
        }

        public void ServerMessage(int serverid, string message)
        {
            foreach(KeyValuePair<SteamID, int> value in bot.ChatListener)
            {
                if (value.Value == serverid)
                    SendChatMessage(value.Key, message);
            }
        }

        public void HookGameServerChat(int moduleID, SteamID partenar, string message)
        {
            if(bot.ChatListener.ContainsKey(partenar))
            {
                PrintChatMessage(partenar, "HOOKCHAT_DONE");
                //SendChatMessage(partenar, "You are already hooking a chat ! Use : STOPHOOK");
                return;
            }

            int serverID = -1;
            Int32.TryParse(message, out serverID);

            if(serverID <= 0)
            {
                //SendChatMessage(partenar, "Invalid game server ID. Use SERVER to get game server ID.");
                string[] param = { serverID.ToString() };
                PrintChatMessage(partenar, "INVALID_SERVER_ID", param);
                PrintChatMessage(partenar, "SERVER_INVALID_ID");
                return;
            }

            GameServer gs = bot.Manager.GetServerByID(serverID);

            if (gs != null)
            {
                bot.ChatListener.Add(partenar, gs.ServerID);

                //SendChatMessage(partenar, "Connecting to server...");
                PrintChatMessage(partenar, "HOOKCHAT_CONNECTING");

                bot.Manager.Send(serverID, moduleID, Networking.NetworkCode.ASteambotCode.HookChat, "");

                //SendChatMessage(partenar, "Done !");
                PrintChatMessage(partenar, "DONE");
            }
            else
            {
                //SendChatMessage(partenar, "Invalid server ID !");
                string[] param = { serverID.ToString() };
                PrintChatMessage(partenar, "INVALID_SERVER_ID", param);
            }
        }

        private void SendChatMessage(SteamID partenar, string message)
        {
            bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, message);
        }
    }
}
