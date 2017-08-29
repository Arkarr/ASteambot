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

        private int serverID;

        public void Execute(Bot bot, Socket socket, int srvid, int code, string args)
        {
            switch((NetworkCode.ASteambotCode)code)
            {
                case NetworkCode.ASteambotCode.Core:
                    RegisterBot(bot, socket, args);
                break;
                case NetworkCode.ASteambotCode.HookChat:
                    HookChat(bot, socket, srvid, args);
                break;
            }
        }

        private void RegisterBot(Bot bot, Socket socket, string args)
        {
            bot.botManager.Servers.RemoveAll(gs => gs.SocketConnected() == false);

            IPEndPoint ipendpoint = ((IPEndPoint)socket.RemoteEndPoint);
            
            int index = bot.botManager.Servers.FindIndex(f => f.IP == ipendpoint.Address);

            if (index >= 0)
                return;

            serverID++;
            GameServer gameserver = new GameServer(socket, serverID, args);
            bot.botManager.Servers.Add(gameserver);
        }

        private void HookChat(Bot bot, Socket socket, int serverid, string args)
        {
            bot.steamchatHandler.ServerMessage(serverid, args);
        }
    }
}
