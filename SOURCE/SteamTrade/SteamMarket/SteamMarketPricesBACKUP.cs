using ArkarrUtilitys;
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
    public class SteamMarketPricesBACKUP
    {
        public List<Item> Items { get; private set; }
        public string ErrorMessage { get; private set; }
        public int ResponseCode { get; private set; }

        private TradeTFBACKUP tf;
        private string baseURL;
        private string tradetfapikey;
        private int CSGO_ItemScanned = 0;

        public event EventHandler<EventArgItemScanned> ItemUpdated;
        
        protected virtual void OnItemUpdate(EventArgItemScanned e)
        {
            if (ItemUpdated != null)
                ItemUpdated(this, e);
        }

        public SteamMarketPricesBACKUP(string tradetfapikey)
        {
            Items = new List<Item>();
            this.tradetfapikey = tradetfapikey;

            tf = new TradeTFBACKUP();
            tf.ScanFinished += Tf_ScanFinished;
            
            System.Timers.Timer cooldown = new System.Timers.Timer();
            cooldown.Elapsed += new ElapsedEventHandler(RescanTF2Market);
            cooldown.Interval = (1000*60)*30;
            cooldown.AutoReset = true;
            cooldown.Enabled = true;
            cooldown.Start();

            baseURL = "http://steamcommunity.com/market/priceoverview/?currency=1&appid=730&market_hash_name=";
        }

        private void Tf_ScanFinished(object sender, EventArgs e)
        {
            foreach (TradeTFBACKUP.Item item in tf.Items)
                UpdateTF2Item(item);
        }
        
        private void UpdateCSGOItem(Item item)
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

        private void UpdateTF2Item(TradeTFBACKUP.Item item)
        {
            double value = 0.0;
            switch (item.Value.Unit)
            {
                case "k": value = item.Value.Middle * TradeTFBACKUP.KeyValue; break;
                case "r": value = (TradeTFBACKUP.KeyValue / tf.MannCoKey.Value.Middle) * item.Value.Middle * 100; break;
                case "b": Console.ForegroundColor = ConsoleColor.Red; SmartConsole.WriteLine("Move yo @ss Arkarr, you got something to fix."); Console.ForegroundColor = ConsoleColor.White; break;
            }

            Item newItem = Items.Find(x => x.Name == item.Name);
            if (newItem == null)
            {
                newItem = new Item(item.Name, tf.LastUpdate, 1, value, 440);
                Items.Add(newItem);
            }
            else if (newItem.Value != item.Value.Middle)
            {
                newItem.Quantity = 1;
                newItem.Value = value;
                newItem.LastUpdated = tf.LastUpdate;
            }

            OnItemUpdate(new EventArgItemScanned(newItem));
        }

        /*public Item ScanCSGOItem(string itemMarketHashName)
        {
            Item item = null;

            string response = null;

            try
            {
                if (CSGO_ItemScanned == 20)
                {
                    Thread.Sleep(60000);
                    CSGO_ItemScanned = 0;
                }
                CSGO_ItemScanned++;
                response = Fetch(baseURL + itemMarketHashName, "GET");
                if (!response.Equals("{\"success\":false}"))
                    item = new Item(itemMarketHashName, 730, response);
                else
                    item = new Item(itemMarketHashName, DateTime.Now.ToString("dd/MM/yyyy") + "@" + DateTime.Now.ToString("HH:mm"), 0, 0.0, 730);
                
                Thread.Sleep(200);

                if (item != null)
                    UpdateCSGOItem(item);
            }
            catch (Exception e)
            {
                SmartConsole.WriteLine(response);
                SmartConsole.WriteLine(e.Message);
                SmartConsole.WriteLine(e.StackTrace);
                return null;
            }

            return item;
        }*/


        public void AddCSGOItem(string name, string lastupdate, int quantity, double value)
        {
            Item item = new Item(name, lastupdate, quantity, value, 730);
            Items.Add(item);
        }
        
        public void ScanTF2Market()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                tf.ProcessJSON(Fetch("http://www.trade.tf/api/spreadsheet.json?key=" + tradetfapikey, "GET"));
            }).Start();
        }

        private void RescanTF2Market(object source, ElapsedEventArgs e)
        {
            ScanTF2Market();
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
                    if(((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.InternalServerError)
                    {
                        var resp = ex.Response as HttpWebResponse;
                        if (resp != null)
                            return resp;
                    }

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
