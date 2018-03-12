using SteamKit2;
using SteamTrade;
using SteamTrade.SteamMarket;
using SteamTrade.TradeOffer;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ASteambot.SteamProfile;

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
                    case NetworkCode.ASteambotCode.SGAnnoucement:
                        PostSteamGroupAnnoucement(bot, gsr);
                        break;
                    case NetworkCode.ASteambotCode.SteamID:
                        SendSteamID(bot, gsr);
                        break;
                }
            }
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e);
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
            
            int index = bot.Manager.Servers.FindIndex(f => f.IP == ipendpoint.Address);

            if (index >= 0)
                return;

            serverID++;
            GameServer gameserver = new GameServer(gsr.Socket, bot.Manager.Config.TCPPassword, serverID, gsr.Arguments);
            bot.Manager.Servers.Add(gameserver);
        }

        private void Disconnect(Bot bot, GameServerRequest gsr)
        {
            bot.Manager.DisconnectServer(gsr.ServerID);
        }

        private void ReportPlayer(Bot bot, GameServerRequest gsr)
        {
            GameServer gs = bot.Manager.GetServerByID(gsr.ServerID);

            string[] ids = gsr.Arguments.Split('/');
            SteamID steamID = new SteamID(ids[0]);
            SteamID reportedDude = new SteamID(ids[1]);

            SteamProfileInfos spGuy = LoadSteamProfile(bot.SteamWeb, steamID);
            SteamProfileInfos spDude = LoadSteamProfile(bot.SteamWeb, reportedDude);

            if (spDude != null && spGuy != null)
            {
                string firstMsg = String.Format("{0} ({1}) reported {2} ({3}) for \"{4}\" @ {5} ({6}) !", spGuy.Name, steamID.ToString(), spDude.Name, reportedDude.ToString(), ids[2], DateTime.Now.ToString("dd/MM/yyyy"), DateTime.Now.ToString("HH:mm"));
                string secondMsg = String.Format("Name of server : {0}", gs.Name);
                string thirdMsg = String.Format("Direct URL : steam://connect/{0}:{1}", gs.IP, gs.Port);

                foreach (SteamID steamid in bot.Friends)
                {
                    if (bot.Config.SteamAdmins.Contains(steamid.ToString()))
                    {
                        bot.SteamFriends.SendChatMessage(steamid, EChatEntryType.ChatMsg, firstMsg);
                        Thread.Sleep(100);
                        bot.SteamFriends.SendChatMessage(steamid, EChatEntryType.ChatMsg, secondMsg);
                        Thread.Sleep(100);
                        bot.SteamFriends.SendChatMessage(steamid, EChatEntryType.ChatMsg, thirdMsg);
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("One of the following steam ID is wrong !");
                Console.WriteLine("> " + ids[0]);
                Console.WriteLine("> " + ids[1]);
                Console.WriteLine("Report was denied !");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private void HookChat(Bot bot, GameServerRequest gsr)
        {
            bot.SteamchatHandler.ServerMessage(gsr.ServerID, gsr.Arguments);
        }

        private void UnhookChat(Bot bot, GameServerRequest gsr)
        {
            List<SteamID> toRemove = new List<SteamID>();
            foreach (KeyValuePair<SteamID, int> value in bot.ChatListener)
            {
                if (value.Value == serverID)
                {
                    SteamID partenar = value.Key;
                    toRemove.Add(partenar);

                    bot.SteamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, "Server sent a unhook chat package, disconnecting...");
                }
            }

            foreach (SteamID cl in toRemove)
            {
                bot.ChatListener.Remove(cl);
                bot.SteamFriends.SendChatMessage(cl, EChatEntryType.ChatMsg, "Done !");
            }
        }

        private void ScanInventory(Bot bot, GameServerRequest gsr, bool withImg)
        {
            if (bot.ArkarrSteamMarket == null)
                bot.ArkarrSteamMarket = new SteamMarket(bot.Config.ArkarrAPIKey, bot.Config.DisableMarketScan);

            GameServer gameServer = bot.Manager.GetServerByID(serverID);

            SteamID steamID = new SteamID(gsr.Arguments);

            if (!bot.Friends.Contains(steamID))
            {
                gameServer.Send(gsr.ModuleID, NetworkCode.ASteambotCode.NotFriends, gsr.Arguments);
                return;
            }

            Thread invScan = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                string items = gsr.Arguments + "/";

                items += AddInventoryItems(bot, Games.TF2, steamID, withImg) + "/";
                items += AddInventoryItems(bot, Games.CSGO, steamID, withImg) + "/";
                items += AddInventoryItems(bot, Games.Dota2, steamID, withImg);

                if (withImg)
                    gameServer.Send(gsr.ModuleID, NetworkCode.ASteambotCode.ScanInventoryIMG, items);
                else
                    gameServer.Send(gsr.ModuleID, NetworkCode.ASteambotCode.ScanInventory, items);
            });

            invScan.Start();
            invScan.Join();
        }
        
        private string AddInventoryItems(Bot bot, Games game, SteamID steamID, bool img)
        {
            string items = "";
            long[] contextID = new long[1];
            contextID[0] = 2;

            bot.OtherGenericInventory.load((int)game, contextID, steamID);

            if (bot.OtherGenericInventory.errors.Count > 0)
            {
                Console.WriteLine("Error while inventory scan :");
                foreach (string error in bot.OtherGenericInventory.errors)
                {
                    Console.WriteLine(error);
                }
            }
            else
            {
                bool allItemsFound = false;
                while (!allItemsFound)
                {
                    allItemsFound = true;

                    foreach (GenericInventory.Item item in bot.OtherGenericInventory.items.Values)
                    {
                        GenericInventory.ItemDescription description = bot.OtherGenericInventory.getDescription(item.assetid);

                        Item i = bot.ArkarrSteamMarket.GetItemByName(description.market_hash_name);
                        if (i != null && description.tradable)// && i.Value != 0)
                            items += item.assetid + "=" + description.market_hash_name.Replace("|", " - ") + "=" + i.Value + (img ? "=" + i.Image : "") + ",";
                    }
                }

                if (items.Length != 0)
                    items = items.Remove(items.Length - 1);
                else
                    items = "EMPTY";
            }

            return items;
        }

        private void CreateTradeOffer(Bot bot, GameServerRequest gsr)
        {
            string message = gsr.Arguments;

            string[] myAssetIDs = null;
            string[] steamIDitems = message.Split('/');
            SteamID steamid = new SteamID(steamIDitems[0]);
            string[] assetIDs = steamIDitems[1].Split(',');
            if(assetIDs.Length > 2)
                myAssetIDs = steamIDitems[2].Split(',');

            GameServer gameServer = bot.Manager.GetServerByID(gsr.ServerID);

            //SteamTrade.SteamMarket.Games game = (SteamTrade.SteamMarket.Games)Int32.Parse(steamIDitems[1]);

            List<long> contextId = new List<long>();
            contextId.Add(2);

            bot.OtherGenericInventory.load((int)Games.CSGO, contextId, steamid);

            TradeOffer to = bot.TradeOfferManager.NewOffer(steamid);

            foreach (GenericInventory.Item item in bot.OtherGenericInventory.items.Values)
            {
                if (Array.IndexOf(assetIDs, item.assetid.ToString()) > -1)
                {
                    GenericInventory.ItemDescription description = bot.OtherGenericInventory.getDescription(item.assetid);
                    to.Items.AddTheirItem(item.appid, item.contextid, (long)item.assetid);
                }
            }
            bot.OtherGenericInventory.load((int)Games.TF2, contextId, steamid);

            foreach (GenericInventory.Item item in bot.OtherGenericInventory.items.Values)
            {
                if (Array.IndexOf(assetIDs, item.assetid.ToString()) > -1)
                {
                    GenericInventory.ItemDescription description = bot.OtherGenericInventory.getDescription(item.assetid);
                    to.Items.AddTheirItem(item.appid, item.contextid, (long)item.assetid);
                }
            }
            bot.OtherGenericInventory.load((int)Games.Dota2, contextId, steamid);

            foreach (GenericInventory.Item item in bot.OtherGenericInventory.items.Values)
            {
                if (Array.IndexOf(assetIDs, item.assetid.ToString()) > -1)
                {
                    GenericInventory.ItemDescription description = bot.OtherGenericInventory.getDescription(item.assetid);
                    to.Items.AddTheirItem(item.appid, item.contextid, (long)item.assetid);
                }
            }

            string offerId;
            to.Send(out offerId, String.Format("\"{0}\" the {1}@{2}", gameServer.Name, DateTime.Now.ToString("dd/MM/yyyy"), DateTime.Now.ToString("HH:mm")));

            if (offerId != "")
            {
                gameServer.Send(gsr.ModuleID, NetworkCode.ASteambotCode.CreateTradeOffer, offerId);
                bot.TradeoffersGS.Add(offerId, gsr.ModuleID);

                bot.AcceptMobileTradeConfirmation(offerId);
            }
            else
            {
                gameServer.Send(gsr.ModuleID, NetworkCode.ASteambotCode.CreateTradeOffer, "-1");
            }
        }

        private void SendFriendInvitation(Bot bot, GameServerRequest gsr)
        {
            SteamID steamID = new SteamID(gsr.Arguments);
            if (steamID.IsValid)
                bot.SteamFriends.AddFriend(steamID);
        }

        private void SendSteamID(Bot bot, GameServerRequest gsr)
        {
            GameServer gs = bot.Manager.GetServerByID(gsr.ServerID);
            gs.Send(gsr.ModuleID, NetworkCode.ASteambotCode.SteamID, bot.getSteamID().ToString());
        }

        private void InviteToSteamGroup(Bot bot, GameServerRequest gsr)
        {
            GameServer gs = bot.Manager.GetServerByID(gsr.ServerID);

            string[] steamIDgroupID = gsr.Arguments.Split('/');

            if (steamIDgroupID.Length == 2)
            {
                SteamID steamID = new SteamID(steamIDgroupID[0]);
                SteamID groupID = new SteamID(ulong.Parse(steamIDgroupID[1]));
                if (steamID.IsValid)
                {
                    if (bot.Friends.Contains(steamID))
                    {
                        if (groupID.IsValid)
                        {
                            bot.InviteUserToGroup(steamID, groupID);
                            if(gs != null)
                                gs.Send(gsr.ModuleID, NetworkCode.ASteambotCode.InviteSteamGroup, steamID.ToString());
                            else
                                Console.WriteLine(">>>> COUDLN'T FIND SERVER; NO REPLY SENT !");
                        }
                    }
                    else
                    {
                        if (gs != null)
                            gs.Send(gsr.ModuleID, NetworkCode.ASteambotCode.NotFriends, steamIDgroupID[0]);
                        else
                            Console.WriteLine(">>>> COUDLN'T FIND SERVER; NO REPLY SENT !");
                    }
                }
            }
        }
        
        private void PostSteamGroupAnnoucement(Bot bot, GameServerRequest gsr)
        {   
            GameServer gs = bot.Manager.GetServerByID(serverID);

            string[] groupIDHeadlineBody = gsr.Arguments.Split('/');

            var data = new NameValueCollection();
            data.Add("sessionID", bot.SteamWeb.SessionId);
            data.Add("action", "post");
            data.Add("headline", groupIDHeadlineBody[1]);
            data.Add("body", groupIDHeadlineBody[2]);
            data.Add("languages[0][headline]", groupIDHeadlineBody[1]);
            data.Add("languages[0][body]", groupIDHeadlineBody[2]);

            string link = "https://steamcommunity.com/gid/" + groupIDHeadlineBody[0] + "/announcements";

            bot.SteamWeb.Fetch(link, "POST", data);

            if (gs != null)
                gs.Send(gsr.ModuleID, NetworkCode.ASteambotCode.SGAnnoucement, groupIDHeadlineBody[1]);
        }
    }
}
