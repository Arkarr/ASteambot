using CsQuery;
using SteamTrade;
using SteamTrade.TradeOffer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking.Webinterface
{
    public class WebInterfaceHelper
    {
        private Dictionary<string, string> pages;

        public WebInterfaceHelper()
        {
            pages = new Dictionary<string, string>();
        }

        public void ReadyUpPage(string path)
        {
            string html = File.ReadAllText(path);

            List<TradeOfferInfo> toInfos = Manager.SelectedBot.LastTradeInfos;
            for(int i = 4; i > toInfos.Count; i--)
                toInfos.Add(new TradeOfferInfo("", "", "", "", TradeOfferState.TradeOfferStateUnknown));

            html = html.Replace("STEAM_ICO_URL", Manager.SelectedBot.SteamProfileInfo.AvatarIcon)
                       .Replace("STEAM_PROFILE_URL", "https://steamcommunity.com/id/" + Manager.SelectedBot.SteamProfileInfo.CustomURL)
                       .Replace("STEAM_INVENTORY_VALUE", Manager.SelectedBot.InventoryValue.ToString())
                       .Replace("STEAM_USERNAME", Manager.SelectedBot.SteamFriends.GetPersonaName())
                       .Replace("STEAM_TRADE_NUMBER", Manager.SelectedBot.GetNumberOfTrades()+"")
                       .Replace("STEAM_INVENTORY_COUNT", Manager.SelectedBot.SteamInventoryItemCount.ToString())
                       .Replace("LAST_TRADE_1_PICTURE", toInfos[0].Sppicture)
                       .Replace("LAST_TRADE_1_NAME", toInfos[0].Spname)
                       .Replace("LAST_TRADE_1_PROFILE_LINK", "https://steamcommunity.com/id/" + toInfos[0].Splink)
                       .Replace("LAST_TRADE_1_STATUS", toInfos[0].ToStatus.ToString())
                       .Replace("LAST_TRADE_2_PICTURE", toInfos[1].Sppicture)
                       .Replace("LAST_TRADE_2_NAME", toInfos[1].Spname)
                       .Replace("LAST_TRADE_2_PROFILE_LINK", "https://steamcommunity.com/id/" + toInfos[1].Splink)
                       .Replace("LAST_TRADE_2_STATUS", toInfos[1].ToStatus.ToString())
                       .Replace("LAST_TRADE_3_PICTURE", toInfos[2].Sppicture)
                       .Replace("LAST_TRADE_3_NAME", toInfos[2].Spname)
                       .Replace("LAST_TRADE_3_PROFILE_LINK", "https://steamcommunity.com/id/" + toInfos[2].Splink)
                       .Replace("LAST_TRADE_3_STATUS", toInfos[2].ToStatus.ToString())
                       .Replace("LAST_TRADE_4_PICTURE", toInfos[3].Sppicture)
                       .Replace("LAST_TRADE_4_NAME", toInfos[3].Spname)
                       .Replace("LAST_TRADE_4_PROFILE_LINK", "https://steamcommunity.com/id/" + toInfos[3].Splink)
                       .Replace("LAST_TRADE_4_STATUS", toInfos[3].ToStatus.ToString())
                       .Replace("TF2_ITEM_POURCENT", Manager.SelectedBot.SteamInventoryTF2Items.ToString())
                       .Replace("CSGO_ITEM_POURCENT", Manager.SelectedBot.SteamInventoryCSGOItems.ToString())
                       .Replace("DOTA2_ITEM_POURCENT", Manager.SelectedBot.SteamInventoryDOTA2Items.ToString())
                       .Replace("STEAM_FRIENDS_COUNT", Manager.SelectedBot.SteamFriends.GetFriendCount().ToString()); 

             //        
             pages[path] = html;
        }

        public Stream GetHTMLPage(string path)
        {
            return GenerateStreamFromString(pages[path]);
        }

        
        private static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        /*
         * Directory.CreateDirectory("./website");
            using (StreamWriter sw = File.AppendText(path))
            {
                int id = 4325;
                string state = "done";
                //sw.WriteLine("<tr><td>" + td.TradeOfferId  + "</td><td>" + td.OfferState + "</td></tr>");
                //sw.WriteLine("<tr><td>" + id + "</td><td>" + state + "</td></tr>");
            }
         */
    }
}
