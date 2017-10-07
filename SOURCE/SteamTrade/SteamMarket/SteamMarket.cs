
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
        None = -1,
        TF2 = 440,
        CSGO = 730,
        Dota2 = 570,
        //PUBG = 418070
    };

    public class SteamMarket
    {
        private string APIkey;
        private List<Item> steamMarketItems;

        public SteamMarket(string apikey)
        {
            APIkey = apikey;
            steamMarketItems = new List<Item>();

            RefreshMarket();

            System.Timers.Timer timerMarketRefresher = new System.Timers.Timer();
            timerMarketRefresher.Elapsed += new ElapsedEventHandler(TMR_ResfreshMarkets);
            timerMarketRefresher.Interval = TimeSpan.FromHours(1).TotalMilliseconds;
            timerMarketRefresher.Enabled = true;
        }

        private void TMR_ResfreshMarkets(object source, ElapsedEventArgs e)
        {
            RefreshMarket();
        }

        private void RefreshMarket()
        {
            new Thread(() =>
            {
                ScanMarket(Games.TF2);
                ScanMarket(Games.CSGO);
                ScanMarket(Games.Dota2);
            }).Start();
        }

        private void ScanMarket(Games game)
        {
            try
            {
                int timeout = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
                string json = Fetch("http://arkarrsourceservers.ddns.net:27019/steammarketitems?apikey=" + APIkey + "&appid=" + (int)game, "GET", null, true, "", false, timeout);
                RootObject ro = JsonConvert.DeserializeObject<RootObject>(json);
                List<Item> items = ro.items;
                if (ro.success)
                {
                    List<Item> itemToAdd = new List<Item>();
                    foreach (Item item in items)
                    {
                        Item i = steamMarketItems.FirstOrDefault(x => x.Name == item.Name);
                        if (i != null && i.Value != item.Value)
                        {
                            i.Value = item.Value;
                            i.LastUpdated = item.LastUpdated;
                        }
                        else if (i == null)
                        {
                            itemToAdd.Add(item);
                        }
                    }

                    if (itemToAdd.Count != 0)
                    {
                        steamMarketItems.AddRange(itemToAdd);
                        itemToAdd.Clear();
                    }
                }
                else
                {
                    Console.WriteLine("Error while fetching " + game.ToString() + "'s market : ");
                    Console.WriteLine(ro.message);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Error while fetching " + game.ToString() + "'s market : ");
                Console.WriteLine(e.Message);
            }
        }

        public class RootObject
        {
            [JsonProperty("items")]
            public List<Item> items { get; set; }
            [JsonProperty("Message")]
            public string message { get; set; }
            [JsonProperty("Success")]
            public Boolean success { get; set; }
        }
        
        public Item GetItemByName(string itemName)
        {
            Item i = steamMarketItems.Find(x => x.Name == itemName);

            return i;
        }

        public string Fetch(string url, string method, NameValueCollection data = null, bool ajax = true, string referer = "", bool fetchError = true, int timeout = 15000)
        {
            using (HttpWebResponse response = Request(url, method, data, ajax, referer, fetchError, timeout))
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream == null)
                        return "";
                    
                    if (response.StatusCode != HttpStatusCode.OK)
                        return response.StatusDescription;

                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        public HttpWebResponse Request(string url, string method, NameValueCollection data = null, bool ajax = true, string referer = "", bool fetchError = false, int timeout = 15000)
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
            request.Timeout = timeout;
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
