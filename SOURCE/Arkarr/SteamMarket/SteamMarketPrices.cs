using Newtonsoft.Json.Linq;
using SteamTrade.SteamMarket;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;

namespace SteamTrade
{
    public class SteamMarketPrices
    {
        public List<Item> Items { get; private set; }
        public string ErrorMessage { get; private set; }
        public int ResponseCode { get; private set; }

        public event EventHandler<EventArgItemScanned> ItemUpdated;

        private TradeTF tf;
        private string baseURL;
        private string tradetfapikey;
        private bool scanCSGOMarket;
        private int csgoItemMarketIndex;
        private List<string> csgoItemsName;

        protected virtual void OnItemUpdate(EventArgItemScanned e)
        {
            if (ItemUpdated != null)
                ItemUpdated(this, e);
        }

        public SteamMarketPrices(string tradetfapikey)
        {
            csgoItemsName = new List<string>();
            Items = new List<Item>();
            this.tradetfapikey = tradetfapikey;

            tf = new TradeTF();
            tf.ScanFinished += Tf_ScanFinished;
            RescanTF2Market(null, null);

            ScanTF2Market();
            ScanCSGOMarket();

            baseURL = "http://steamcommunity.com/market/priceoverview/?currency=1&appid=730&market_hash_name=";
        }

        private void Tf_ScanFinished(object sender, EventArgs e)
        {
            foreach (TradeTF.Item item in tf.Items)
                UpdateItem(item);
        }

        public void Stop()
        {
            scanCSGOMarket = false;
        }

        private void UpdateItem(Item item)
        {
            Item i = Items.Find(x => x.Name == item.Name);
            if (i == null)
            {
                Items.Add(item);
            }
            else if(i.Value != item.Value)
            {
                i.Value = item.Value;
                i.Quantity = item.Quantity;
                i.LastUpdated = item.LastUpdated;
            }

            OnItemUpdate(new EventArgItemScanned(item));
        }
        
        private void UpdateItem(TradeTF.Item item)
        {
            double value = 0.0;
            switch (item.Value.Unit)
            {
                case "k": value = item.Value.Middle * TradeTF.KeyValue; break;
                case "r": value = (TradeTF.KeyValue / tf.MannCoKey.Value.Middle) * item.Value.Middle; break;
                case "b": Console.WriteLine("Move yo @ss Arkarr, you got something to fix."); break;
            }

            Item i = Items.Find(x => x.Name == item.Name);
            if (i == null)
            {
                Item newItem = new Item(item.Name, tf.LastUpdate, 1, value, 440);
                Items.Add(newItem);
            }
            else if (i.Value != item.Value.Middle)
            {
                i.Quantity = 1;
                i.Value = value;
                i.LastUpdated = tf.LastUpdate;
            }
        }

        public void ScanCSGOMarket()
        {
            scanCSGOMarket = true;
            
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                try
                {

                    while (scanCSGOMarket)
                    {
                        string response = null;
                        for (int i = csgoItemMarketIndex; i < csgoItemsName.Count; i++)
                        {
                            string itemName = csgoItemsName[i];
                            response = Fetch(baseURL + itemName, "GET", null, true, "", true);
                            SteamMarket.Item item = new SteamMarket.Item(itemName, response);
                            UpdateItem(item);
                            csgoItemMarketIndex++;
                            Thread.Sleep(3000);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e.HResult == 500)
                    {
                        csgoItemMarketIndex++;
                        ScanCSGOMarket();
                    }
                }
            }).Start();

            ScanCSGOMarket();
        }

        public void ScanTF2Market()
        {
            System.Timers.Timer cooldown = new System.Timers.Timer();
            cooldown.Elapsed += new ElapsedEventHandler(RescanTF2Market);
            cooldown.Interval = 1000*180;
            cooldown.AutoReset = true;
            cooldown.Enabled = true;
        }

        private void RescanTF2Market(object source, ElapsedEventArgs e)
        {
            tf.ProcessJSON(Fetch("http://www.trade.tf/api/spreadsheet.json?key=" + tradetfapikey, "GET"));
        }

        public void AddItemToCSGOscan(string itemName)
        {
            csgoItemsName.Insert(csgoItemMarketIndex, itemName);
        }

        public void AddCSGOItem(string name, string lastupdate, int quantity, double value)
        {
            SteamMarket.Item item = new SteamMarket.Item(name, lastupdate, quantity, value, 730);
            Items.Add(item);
            AddItemToCSGOscan(name);
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
                        {
                            return resp;
                        }
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
                    {
                        return resp;
                    }
                }
                throw;
            }
        }
    }
}
