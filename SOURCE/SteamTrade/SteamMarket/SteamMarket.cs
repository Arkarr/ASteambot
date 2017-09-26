using CsQuery;
using CsQuery.Implementation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;

namespace SteamTrade.SteamMarket
{
    public enum Games
    {
        TF2 = 440,
        CSGO = 730,
        Dota2 = 570,
        //PUBG = 418070
    };

    public class SteamMarket
    {
        public List<Item> Items { get; private set; }
        public string ErrorMessage { get; private set; }
        public int ResponseCode { get; private set; }
        public string LastUpdate { get; private set; }

        private int itemScanned = 0;
        private int nextGame = 0;

        public event EventHandler<EventArgItemScanned> ItemUpdated;
        public event EventHandler<EventArgs> ScanFinished;

        protected virtual void OnItemUpdate(EventArgItemScanned e)
        {
            if (ItemUpdated != null)
                ItemUpdated(this, e);
        }
        protected virtual void OnScanFinished(EventArgs e)
        {
            if (ScanFinished != null)
                ScanFinished(this, e);

            ScanMarket();
        }

        public SteamMarket()
        {
            Items = new List<Item>();
        }

        public void AddItem(string name, string lastupdate, int quantity, double value, int appid)
        {
            Item item = new Item(name, lastupdate, quantity, value, appid);
            Items.Add(item);
        }

        public Item GetItem(string itemMarketHashName)
        {
            return Items.Find(x => x.Name == itemMarketHashName);
        }

        public void ScanMarket()
        {
            Games game = Games.CSGO;
            if (nextGame == 1)
                game = Games.TF2;
            if (nextGame == 2)
                game = Games.Dota2;
            /*if (nextGame == 3)
                game = Games.PUBG;*/

            if (nextGame == 2)
                nextGame = 0;
            else
                nextGame++;

            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                string url = "http://steamcommunity.com/market/search/render/?query=&start=" + itemScanned + "&count=100&appid=" + (int)game;
                while (!ProcessJSON(Fetch(url, "GET")))
                {
                    itemScanned += 100;
                    url = "http://steamcommunity.com/market/search/render/?query=&start=" + itemScanned + "&count=100&appid=" + (int)game;
                    Thread.Sleep(10000);
                }
                itemScanned = 0;
                OnScanFinished(new EventArgs());
            }).Start();
        }

        private bool ProcessJSON(string json)
        {
            int nbrItems = 0;
            JObject array = JObject.Parse(json);

            foreach (var x in array)
            {
                string name = x.Key;
                JToken value = x.Value;

                if (name.Equals("success"))
                {
                    bool success = value.ToObject<Boolean>();
                    if (!success)
                        return false;
                }
                else if (name.Equals("total_count"))
                {
                    nbrItems = value.ToObject<Int32>();
                }
                else if (name.Equals("results_html"))
                {
                    string html = value.ToObject<String>();

                    html = html.Replace("\t", "").Replace("\r", "").Replace("\n", "").Replace("\"", "");
                    CQ finalHtml = html;

                    CQ rows = finalHtml[".market_listing_row"];
                    foreach (DomElement row in rows)
                    {
                        double price = 0.0;
                        string itemName = null;
                        string game = null;

                        foreach (DomElement element in row.ChildElements)
                        {
                            if (element.HasClass("market_listing_price_listings_block"))
                            {
                                IDomObject test = element.LastChild;
                                IDomObject prices = test.FirstChild;
                                price = Double.Parse(prices.LastChild.InnerText.Replace("$", "").Replace(" USD", ""));
                            }
                            else if (element.HasClass("market_listing_item_name_block"))
                            {
                                itemName = HttpUtility.HtmlDecode(element.FirstChild.InnerText);
                                game = element.LastChild.InnerText;
                            }
                        }

                        int appID;
                        Item item;
                        switch (game)
                        {
                            case "Team Fortress 2": appID = 440; break;
                            case "Counter-Strike: Global Offensive": appID = 730; break;
                            case "PLAYERUNKNOWN'S BATTLEGROUNDS": appID = 999; break;
                            case "Dota 2": appID = 999; break;
                            default: appID = 0; break;
                        }
                        item = new Item(itemName, DateTime.Now.ToString("dd/MM/yyyy") + "@" + DateTime.Now.ToString("HH:mm"), 1, price, appID);
                        Items.Add(item);
                        OnItemUpdate(new EventArgItemScanned(item));
                    }
                }
            }

            if (itemScanned >= nbrItems)
                return true;
            else
                return false;
        }

        public string Fetch(string url, string method, NameValueCollection data = null, bool ajax = true, string referer = "", bool fetchError = false)
        {
            using (HttpWebResponse response = Request(url, method, data, ajax, referer, fetchError))
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream == null)
                        return "";

                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        public HttpWebResponse Request(string url, string method, NameValueCollection data = null, bool ajax = true, string referer = "", bool fetchError = false)
        {

            bool isGetMethod = (method.ToLower() == "get");
            string dataString = (data == null ? null : String.Join("&", Array.ConvertAll(data.AllKeys, key => string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(data[key])))));

            if (isGetMethod && !string.IsNullOrEmpty(dataString))
            {
                url += (url.Contains("?") ? "&" : "?") + dataString;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Accept = "application/json, text/javascript;q=0.9, */*;q=0.5";
            request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.57 Safari/537.36";
            request.Referer = string.IsNullOrEmpty(referer) ? "http://steamcommunity.com/trade/1" : referer;
            request.Timeout = 3000;
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.Revalidate);
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            if (ajax)
            {
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.Add("X-Prototype-Version", "1.7");
            }

            if (isGetMethod || string.IsNullOrEmpty(dataString))
            {
                try
                {
                    return request.GetResponse() as HttpWebResponse;
                }
                catch (WebException ex)
                {
                    if (fetchError)
                    {
                        var resp = ex.Response as HttpWebResponse;
                        if (resp != null)
                            return resp;
                    }
                    throw;
                }
            }

            byte[] dataBytes = Encoding.UTF8.GetBytes(dataString);
            request.ContentLength = dataBytes.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(dataBytes, 0, dataBytes.Length);
            }

            try
            {
                return request.GetResponse() as HttpWebResponse;
            }
            catch (WebException ex)
            {
                if (fetchError)
                {
                    var resp = ex.Response as HttpWebResponse;
                    if (resp != null)
                        return resp;
                }
                throw;
            }
        }
    }
}
