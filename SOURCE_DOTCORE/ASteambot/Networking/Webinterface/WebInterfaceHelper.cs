using ASteambot.SteamMarketUtility;
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


            html = html.Replace("STEAM_ICO_URL", Manager.SelectedBot.SteamProfileInfo.AvatarIcon)
                       .Replace("STEAM_PROFILE_URL", "https://steamcommunity.com/id/" + Manager.SelectedBot.SteamProfileInfo.CustomURL)
                       .Replace("STEAM_INVENTORY_VALUE", Manager.SelectedBot.InventoryValue.ToString())
                       .Replace("STEAM_USERNAME", Manager.SelectedBot.SteamFriends.GetPersonaName())
                       .Replace("STEAM_TRADE_NUMBER", Manager.SelectedBot.GetNumberOfTrades()+"")
                       .Replace("STEAM_INVENTORY_COUNT", Manager.SelectedBot.SteamInventoryItemCount.ToString())
                       .Replace("TF2_ITEM_POURCENT", Manager.SelectedBot.SteamInventoryTF2Items.ToString())
                       .Replace("CSGO_ITEM_POURCENT", Manager.SelectedBot.SteamInventoryCSGOItems.ToString())
                       .Replace("DOTA2_ITEM_POURCENT", Manager.SelectedBot.SteamInventoryDOTA2Items.ToString())
                       .Replace("STEAM_FRIENDS_COUNT", Manager.SelectedBot.SteamFriends.GetFriendCount().ToString());

            List<TradeOfferInfo> toInfos = Manager.SelectedBot.LastTradeInfos;

            for(int i = 0; i < toInfos.Count; i++)
            {
                html = html.Replace("LAST_TRADE_"+ i + "_PICTURE", toInfos[i].Sppicture)
                .Replace("LAST_TRADE_" + i + "_NAME", toInfos[i].Spname)
                .Replace("LAST_TRADE_" + i + "_PROFILE_LINK", "https://steamcommunity.com/id/" + toInfos[i].Splink)
                .Replace("LAST_TRADE_" + i + "_STATUS", toInfos[i].ToStatus.ToString());
            }

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
