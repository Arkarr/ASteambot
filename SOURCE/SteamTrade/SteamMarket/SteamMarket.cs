
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
        private bool stop;
        private bool TF2OK;
        private bool CSGOOK;
        private bool DOTA2OK;
        private List<Item> steamMarketItemsTF2;
        private List<Item> steamMarketItemsCSGO;
        private List<Item> steamMarketItemsDOTA2;
        private List<Thread> marketsScanners;

        public SteamMarket(string apikey, bool disabled)
        {
            stop = false;
            APIkey = apikey;
            TF2OK = false;
            CSGOOK = false;
            DOTA2OK = false;
            steamMarketItemsTF2 = new List<Item>();
            steamMarketItemsCSGO = new List<Item>();
            steamMarketItemsDOTA2 = new List<Item>();
            marketsScanners = new List<Thread>();

            if (!disabled)
            {
                RefreshMarket();

                System.Timers.Timer timerMarketRefresher = new System.Timers.Timer();
                timerMarketRefresher.Elapsed += new ElapsedEventHandler(TMR_ResfreshMarkets);
                timerMarketRefresher.Interval = TimeSpan.FromHours(1).TotalMilliseconds;
                timerMarketRefresher.Enabled = true;
            }
        }

        private void TMR_ResfreshMarkets(object source, ElapsedEventArgs e)
        {
            if(!stop)
                RefreshMarket();
        }

        public void Cancel()
        {
            stop = true;
            foreach(Thread t in marketsScanners)
                t.Abort();
        }

        private void RefreshMarket(Games game = Games.None)
        {
            
            if (game == Games.None)
            {
                Console.WriteLine("Fetching market's prices...");

                /*if (!stop)
                    TF2OK = ScanMarket(Games.TF2);

                if (!stop)
                    CSGOOK = ScanMarket(Games.CSGO);

                if (!stop)
                    DOTA2OK = ScanMarket(Games.Dota2);*/

                Thread TF2marketScanner = new Thread(() =>
                {
                    DateTime dt = DateTime.Now;
                    TF2OK = ScanMarket(Games.TF2);
                    DateTime now = DateTime.Now;
                    Console.WriteLine("market scan for tf2 in : " + (now.Subtract(dt).Minutes) + " minutes !");
                });

                Thread CSGOmarketScanner = new Thread(() =>
                {
                    DateTime dt = DateTime.Now;
                    CSGOOK = ScanMarket(Games.CSGO);
                    DateTime now = DateTime.Now;
                    Console.WriteLine("market scan for csgo done in : " + (now.Subtract(dt).Minutes) + " minutes !");
                });

                Thread DOTA2marketScanner = new Thread(() =>
                {
                    DateTime dt = DateTime.Now;
                    DOTA2OK = ScanMarket(Games.Dota2);
                    DateTime now = DateTime.Now;
                    Console.WriteLine("market scan for dota2 done in : " + (now.Subtract(dt).Minutes) + " minutes !");
                });

                marketsScanners.Add(TF2marketScanner);
                marketsScanners.Add(CSGOmarketScanner);
                marketsScanners.Add(DOTA2marketScanner);

                TF2marketScanner.Start();
                CSGOmarketScanner.Start();
                DOTA2marketScanner.Start();
            }
            else
            {
                if (!stop)
                {
                    Console.WriteLine("Fetching " + game + " prices...");
                    Thread marketScanner = new Thread(() =>
                    {
                        switch (game)
                        {
                            case Games.CSGO: CSGOOK = ScanMarket(game); break;
                            case Games.TF2: TF2OK = ScanMarket(game); break;
                            case Games.Dota2: DOTA2OK = ScanMarket(game); break;
                        }
                    });
                    marketsScanners.Add(marketScanner);
                    marketScanner.Start();
                }
            }
        }

        public bool IsAvailable()
        {
            return CSGOOK && TF2OK && DOTA2OK;
        }

        public void ForceRefresh()
        {
            Console.WriteLine("Force market refresh...");
            RefreshMarket();
        }

        private bool ScanMarket(Games game, int startIndex = 0)
        {
            try
            {
                int timeout = (int)TimeSpan.FromMinutes(3).TotalMilliseconds;
                string json = Fetch("http://arkarrsourceservers.ddns.net:27019/steammarketitems?apikey=" + APIkey + "&start="+startIndex+"&appid=" + (int)game, "GET", null, true, "", false, timeout);
                RootObject ro = JsonConvert.DeserializeObject<RootObject>(json);
                List<Item> items = ro.items;
                if (ro.success && stop == false)
                {
                    List<Item> itemToAdd = new List<Item>();
                    foreach (Item item in items)
                    {
                        Item i = null;
                        if (game == Games.TF2)
                            i = steamMarketItemsTF2.FirstOrDefault(x => x.Name == item.Name);
                        else if (game == Games.CSGO)
                            i = steamMarketItemsCSGO.FirstOrDefault(x => x.Name == item.Name);
                        else if (game == Games.Dota2)
                            i = steamMarketItemsDOTA2.FirstOrDefault(x => x.Name == item.Name);

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
                        if (game == Games.TF2)
                            steamMarketItemsTF2.AddRange(itemToAdd);
                        else if (game == Games.CSGO)
                            steamMarketItemsCSGO.AddRange(itemToAdd);
                        else if (game == Games.Dota2)
                            steamMarketItemsDOTA2.AddRange(itemToAdd);

                        itemToAdd.Clear();
                    }

                    if (ro.nbritems <= startIndex + 500)
                        Console.WriteLine("Price for game " + game.ToString() + " updated !");
                    else
                        return ScanMarket(game, startIndex + 500);

                    return true;
                }
                else
                {
                    Console.WriteLine("Error while fetching " + game.ToString() + "'s market : ");
                    Console.WriteLine(ro.message);

                    return false;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Error while fetching " + game.ToString() + "'s market : ");
                Console.WriteLine(e.Message);

                return false;
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
            [JsonProperty("nbritems")]
            public int nbritems { get; set; }
        }
        
        public Item GetItemByName(string itemName)
        {
            Item i = steamMarketItemsCSGO.Find(x => x.Name == itemName);

            if (i == null)
                i = steamMarketItemsTF2.Find(x => x.Name == itemName);

            if (i == null)
                i = steamMarketItemsDOTA2.Find(x => x.Name == itemName);

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
