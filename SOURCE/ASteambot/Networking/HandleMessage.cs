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

        public void Execute(Bot bot, GameServerRequest gsr)
        {
            try
            {
                switch ((NetworkCode.ASteambotCode)gsr.NetworkCode)
                {
                    case NetworkCode.ASteambotCode.Core:
                        RegisterBot(bot, gsr);
                        break;
                    case NetworkCode.ASteambotCode.Disconnect:
                        Disconnect(bot, gsr);
                        break;
                    case NetworkCode.ASteambotCode.HookChat:
                        HookChat(bot, gsr);
                        break;
                    case NetworkCode.ASteambotCode.ScanInventory:
                        ScanInventory(bot, gsr, false);
                        break;
                    case NetworkCode.ASteambotCode.ScanInventoryIMG:
                        ScanInventory(bot, gsr, true);
                        break;
                    case NetworkCode.ASteambotCode.CreateTradeOffer:
                        CreateTradeOffer(bot, gsr);
                        break;
                    case NetworkCode.ASteambotCode.FriendInvite:
                        SendFriendInvitation(bot, gsr);
                        break;
                    case NetworkCode.ASteambotCode.ReportPlayer:
                        ReportPlayer(bot, gsr);
                        break;
                    case NetworkCode.ASteambotCode.InviteSteamGroup:
                        InviteToSteamGroup(bot, gsr);
                        break;
                    case NetworkCode.ASteambotCode.Unhookchat:
                        UnhookChat(bot, gsr);
                        break;
                }
            }
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Crashing while executing net code " + gsr.NetworkCode + " !");
                Console.WriteLine("Full detail message [MAY CONTAIN SENSITIVE INFOS] :");
                Console.WriteLine("SRV ID : " + gsr.ServerID + " MDL ID: " + gsr.ModuleID);
                Console.WriteLine(gsr.Arguments);
                Console.ForegroundColor = ConsoleColor.Red;
            }
        }

        private void RegisterBot(Bot bot, GameServerRequest gsr)
        {
            IPEndPoint ipendpoint = ((IPEndPoint)gsr.Socket.RemoteEndPoint);
            
            int index = bot.botManager.Servers.FindIndex(f => f.IP == ipendpoint.Address);

            if (index >= 0)
                return;

            serverID++;
            GameServer gameserver = new GameServer(gsr.Socket, bot.botManager.Config.TCPPassword, serverID, gsr.Arguments);
            bot.botManager.Servers.Add(gameserver);
        }

        private void Disconnect(Bot bot, GameServerRequest gsr)
        {
            bot.botManager.DisconnectServer(gsr.ServerID);
        }

        private void ReportPlayer(Bot bot, GameServerRequest gsr)
        {
            bot.ReportPlayer(gsr.ServerID, gsr.Arguments);
        }

        private void HookChat(Bot bot, GameServerRequest gsr)
        {
            bot.steamchatHandler.ServerMessage(gsr.ServerID, gsr.Arguments);
        }

        private void UnhookChat(Bot bot, GameServerRequest gsr)
        {
            bot.UnhookChat(gsr.ServerID, gsr.Arguments);
        }

        private void ScanInventory(Bot bot, GameServerRequest gsr, bool withImg)
        {
            bot.ScanInventory(gsr.ServerID, gsr.ModuleID, gsr.Arguments, withImg);
        }

        private void CreateTradeOffer(Bot bot, GameServerRequest gsr)
        {
            bot.TCPCreateTradeOffer(gsr.ServerID, gsr.ModuleID, gsr.Arguments);
        }

        private void SendFriendInvitation(Bot bot, GameServerRequest gsr)
        {
            bot.InviteFriend(gsr.Arguments);
        }

        private void InviteToSteamGroup(Bot bot, GameServerRequest gsr)
        {
            bot.InviteUserToGroup(gsr.ServerID, gsr.ModuleID, gsr.Arguments);
        }
        
    }
}
