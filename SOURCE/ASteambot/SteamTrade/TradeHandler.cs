using ASteambot.Modules;
using ASteambot.Networking;
using ASteambot.SteamMarketUtility;
using ASteambotInterfaces.ASteambotInterfaces;
using SteamAuth;
using SteamKit2;
using SteamTrade;
using SteamTrade.TradeOffer;
using SteamTrade.TradeWebAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ASteambot.SteamTrade
{
    public class TradeHandler
    {
        private readonly GenericInventory mySteamInventory;
        private readonly GenericInventory OtherSteamInventory;
        private readonly Trade trade;
        private readonly Bot bot;
        private readonly SteamID partenarSteamID;
        private readonly int gameID;
        private readonly int serverID;
        private readonly int moduleID;
        private readonly string args;

        private double value;

        public TradeHandler(Trade trade, Bot bot, SteamID partenarSteamID, SteamWebCustom steamWeb, int serverID, int moduleID, string args, int gameID = -1)
        {
            this.trade = trade;
            this.bot = bot;
            this.gameID = gameID;
            this.partenarSteamID = partenarSteamID;
            this.serverID = serverID;
            this.moduleID = moduleID;
            this.args = args;
            mySteamInventory = new GenericInventory(bot.getSteamID(), steamWeb);
            OtherSteamInventory = new GenericInventory(partenarSteamID, steamWeb);
        }

        public void OnTradeError(string error)
        {
            Console.WriteLine("Oh, there was an error: {0}.", error);
        }

        public void OnTradeTimeout()
        {
            Console.WriteLine("Sorry, but you were AFK and the trade was canceled.");
        }

        public void OnTradeInit()
        {
            //HACK
            trade.SendMessage("BUFFER");
            //Don't remove me

            trade.SendMessage("Please wait...");

            if (gameID != -1)
            {
                List<long> contextId = new List<long>();
                contextId.Add(2);

                mySteamInventory.load(gameID, contextId, bot.getSteamID());
                OtherSteamInventory.load(gameID, contextId, partenarSteamID);
            }

            trade.SendMessage("Done !");
        }

        public void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            IItemDescription tmpDescription = OtherSteamInventory.getDescription(inventoryItem.Id);
            Item itemInfo = SteamMarket.GetItemByName(tmpDescription.market_hash_name, gameID);

            this.value += itemInfo.Value;

            trade.SendMessage(string.Format("The value of this item is : {0}$", itemInfo.Value));
        }

        public void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            IItemDescription tmpDescription = OtherSteamInventory.getDescription(inventoryItem.Id);
            Item itemInfo = SteamMarket.GetItemByName(tmpDescription.market_hash_name, gameID);

            this.value -= itemInfo.Value;

            trade.SendMessage(string.Format("The value of this item is : {0}$", itemInfo.Value));
        }

        public void OnTradeAwaitingConfirmation(long tradeOfferID)
        {
            trade.SendMessage("Please complete the confirmation to finish the trade");
            bot.SteamFriends.SendChatMessage(partenarSteamID, EChatEntryType.ChatMsg, "Please complete the confirmation to finish the trade");

            TradeOffer to = null;

            bot.TradeOfferManager.TryGetOffer(tradeOfferID.ToString(), out to);
            bot.UpdateTradeOfferInDatabase(to, value);

            bot.TradeoffersGS.Add(tradeOfferID.ToString(), serverID + "|" + moduleID + "|" + value + "|" + args);
            bot.TradeOfferValue.Add(tradeOfferID.ToString(), value);

            bot.Manager.Send(serverID, moduleID, NetworkCode.ASteambotCode.CreateTradeOffer, partenarSteamID.ConvertToUInt64() + "/" + tradeOfferID + "/" + value + "/" + args);
        }

        public bool Validate()
        {
            if (trade.MyOfferedItems.Count() > 0)
            {
                trade.SendMessage("Something went horribly wrong... !");
                return false;
            }

            /*foreach (TradeUserAssets tua in this.trade.OtherOfferedItems)
            {
                IItemDescription tmpDescription = OtherSteamInventory.getDescription(tua.assetid);
                Item itemInfo = SteamMarket.GetItemByName(tmpDescription.market_hash_name, gameID);

                if (itemInfo.AppID != 730 || itemInfo.AppID != 440)
                    return false;

                totalValue += itemInfo.Value;
            }*/

            trade.SendMessage(string.Format("Trade validated. Total value : {0}$ !", value));

            return true;
        }

        internal void OnStatusError(Trade.TradeStatusType statusType)
        {
            Console.WriteLine("Error : " + statusType);
        }

        public void OnTradeClose()
        {
            Console.WriteLine("Trade closed");
            bot.UnsubscribeTrade(this, trade);
        }

        internal void OnTradeMessageHandler(string msg)
        {
            Console.WriteLine(msg);
        }

        public void OnTradeAcceptHandler()
        {
            trade.AcceptTrade();
        }

        internal void OnTradeReadyHandler(bool ready)
        {
            trade.Poll();
            if (!ready)
            {
                trade.SetReady(false);
            }
            else
            {
                if (Validate())
                    trade.SetReady(true);
            }
        }
    }
}
