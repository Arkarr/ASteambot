using CsQuery;
using CsQuery.Implementation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamTrade;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
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

namespace ASteambot.SteamMarketUtility
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
        private SteamWeb fetcher;
        private Thread TF2marketScanner;
        private Thread CSGOmarketScanner;
        private Thread DOTA2marketScanner;
        private Thread marketScanner;
        private List<Item> steamMarketItemsTF2;
        private List<Item> steamMarketItemsCSGO;
        private List<Item> steamMarketItemsDOTA2;

        public SteamMarket(string apikey, bool disabled, SteamWeb fetcher)
        {
            stop = false;
            APIkey = apikey;
            TF2OK = false;
            CSGOOK = false;
            DOTA2OK = false;
            this.fetcher = fetcher;
            steamMarketItemsTF2 = new List<Item>();
            steamMarketItemsCSGO = new List<Item>();
            steamMarketItemsDOTA2 = new List<Item>();

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
        }

        private void RefreshMarket(Games game = Games.None)
        {
            if (game == Games.None)
            {
                Console.WriteLine("Fetching market's prices...");

                if (TF2marketScanner == null || !TF2marketScanner.IsAlive)
                {
                    TF2marketScanner = new Thread(() =>
                    {
                        DateTime dt = DateTime.Now;
                        TF2OK = ScanMarket(Games.TF2);
                        DateTime now = DateTime.Now;
                        TimeSpan difference = now.Subtract(dt);
                        Console.WriteLine("market scan for tf2 in : " + difference.Hours.ToString("00") + "h:" + difference.Minutes.ToString("00") + "m:" + difference.Seconds.ToString("00") + "s");
                    });
                }

                if (CSGOmarketScanner == null || !CSGOmarketScanner.IsAlive)
                {
                    CSGOmarketScanner = new Thread(() =>
                    {
                        DateTime dt = DateTime.Now;
                        CSGOOK = ScanMarket(Games.CSGO);
                        DateTime now = DateTime.Now;
                        TimeSpan difference = now.Subtract(dt);
                        Console.WriteLine("market scan for CS:GO in : " + difference.Hours.ToString("00") + "h:" + difference.Minutes.ToString("00") + "m:" + difference.Seconds.ToString("00") + "s");
                    });
                }


                if (DOTA2marketScanner == null || !DOTA2marketScanner.IsAlive)
                {
                    DOTA2marketScanner = new Thread(() =>
                    {
                        DateTime dt = DateTime.Now;
                        DOTA2OK = ScanMarket(Games.Dota2);
                        DateTime now = DateTime.Now;
                        TimeSpan difference = now.Subtract(dt);
                        Console.WriteLine("market scan for DOTA 2 in : " + difference.Hours.ToString("00") + "h:" + difference.Minutes.ToString("00") + "m:" + difference.Seconds.ToString("00") + "s");
                    });
                }

                TF2marketScanner.Start();
                CSGOmarketScanner.Start();
                DOTA2marketScanner.Start();
            }
            else
            {
                if (!stop)
                {
                    if (marketScanner == null || !marketScanner.IsAlive)
                    {
                        Console.WriteLine("Fetching " + game + " prices...");
                        marketScanner = new Thread(() =>
                        {
                            switch (game)
                            {
                                case Games.CSGO: CSGOOK = ScanMarket(game); break;
                                case Games.TF2: TF2OK = ScanMarket(game); break;
                                case Games.Dota2: DOTA2OK = ScanMarket(game); break;
                            }
                        });
                        marketScanner.Start();
                    }
                }
            }
        }

        public bool IsAvailable()
        {
            //return false;
            return CSGOOK && TF2OK && DOTA2OK;
        }

        public void ForceRefresh()
        {
            Console.WriteLine("Force market refresh...");

            Cancel();

            while (TF2marketScanner != null && TF2marketScanner.IsAlive)
                Thread.Sleep(1000);

            while (CSGOmarketScanner != null && CSGOmarketScanner.IsAlive)
                Thread.Sleep(1000);

            while (DOTA2marketScanner != null && DOTA2marketScanner.IsAlive)
                Thread.Sleep(1000);

            stop = false;

            RefreshMarket();
        }

        private bool ScanItem(Games game, string itemName, int startIndex = 0)
        {
            try
            {
                RootObject ro = null;
                do
                {
                    int timeout = (int)TimeSpan.FromMinutes(3).TotalMilliseconds;
                    string target = "http://arkarrsourceservers.ddns.net:27019/steammarketitems?apikey=" + APIkey + "&appid=" + (int)game +  "&market_hash_name=" + itemName + "&version=2";
                    string json = fetcher.Fetch(target, "GET", null, true, "", true, timeout);
                    ro = JsonConvert.DeserializeObject<RootObject>(json);

                    if (ro == null)
                    {
                        Console.WriteLine("Error fetching : " + target + " !");
                        Console.WriteLine("Trying again.");
                    }
                }
                while (ro == null && !stop);

                if (stop)
                    return false;

                if (ro.success && stop == false && ro.items.Count > 0)
                {
                    Item item = ro.items.First();

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
                        if (game == Games.TF2)
                            steamMarketItemsTF2.Add(item);
                        else if (game == Games.CSGO)
                            steamMarketItemsCSGO.Add(item);
                        else if (game == Games.Dota2)
                            steamMarketItemsDOTA2.Add(item);
                    }

                    return true;
                }
                else if(ro.items.Count == 0)
                {
                    Console.WriteLine("Error while fetching " + game.ToString() + "'s market : ");
                    Console.WriteLine("No item with that name (" + itemName + ") in API");

                    return false;
                }
                else
                {
                    Console.WriteLine("Error while fetching " + game.ToString() + "'s market : ");
                    Console.WriteLine(ro.message);

                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while fetching " + game.ToString() + "'s market : ");
                Console.WriteLine(e.Message);

                return false;
            }
        }

        private bool ScanMarket(Games game, int startIndex = 0)
        {
            try
            {
                RootObject ro = null;
                do
                {
                    int timeout = (int)TimeSpan.FromMinutes(3).TotalMilliseconds;
                    string target = "http://arkarrsourceservers.ddns.net:27019/steammarketitems?apikey=" + APIkey + "&appid=" + (int)game + "&version=2";
                    string json = fetcher.Fetch(target, "GET", null, true, "", true, timeout);
                    ro = JsonConvert.DeserializeObject<RootObject>(json);

                    if (ro == null)
                    {
                        Console.WriteLine("Error fetching : " + target + " !");
                        Console.WriteLine("Trying again.");
                    }
                }
                while (ro == null && !stop);

                if (stop)
                    return false;

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
        
        public Item GetItemByName(string itemName, int appid)
        {
            Games game = (Games)appid;

            Item i = null;
            switch (game)
            {
                case Games.CSGO:
                    i = steamMarketItemsCSGO.Find(x => x.Name == itemName);
                    return i;
                case Games.TF2:
                    i = steamMarketItemsTF2.Find(x => x.Name == itemName);
                    return i;
                case Games.Dota2:
                    i = steamMarketItemsDOTA2.Find(x => x.Name == itemName);
                    return i;
            }
           
            return null;
        }
    }
}
