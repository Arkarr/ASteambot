using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamKit2;

namespace SteamTrade.TradeWebAPI
{
    public class SessionExpiredException : Exception { }

    /// <summary>
    /// This class provides the interface into the Web API for trading on the
    /// Steam network.
    /// </summary>
    public class Asset
    {
        public uint AppID { get; set; }
        public ulong AssetID { get; set; }
        public ulong ContextID { get; set; }

        public ulong ClassID { get; set; }

        public SteamID Owner { get; set; }
    }

    public class TradeSession_V2 : WebClient
    {
        const string STEAMAPI_TRADE = "https://steamcommunity.com/trade/{0}/{1}";

        public SteamID PartenarSteamID { get; private set; }
        public int Version { get; set; }

        private string location;
        private string sessionID;
        private string token;
        private string tokenSecure;

        public TradeSession_V2(string sessionID, string token, string tokenSecure, SteamID otherSteamID)
        {
            PartenarSteamID = otherSteamID;

            Version = 0;
            this.sessionID = sessionID;
            this.token = token;
            this.tokenSecure = tokenSecure;
        }

        public TradeStatus GetStatus(int logPos)
        {
            string strStatus = SendTradeQuery("tradestatus", new Dictionary<string, object>
            {
                { "logpos", logPos },
            });

            TradeStatus status = JsonConvert.DeserializeObject<TradeStatus>(strStatus);

            // Sometimes the cookies expire during the trade session.
            // We should really check for this by checking the SteamLogin cookie for the value 'deleted', but as a quick hack, let's check status.success instead
            if (status.success == false && Version == 0)
            {
                throw new Exception();// new SessionExpiredException();
            }

            return status;
        }

        public dynamic GetForeignInventory(SteamID steamid, long contextid, long appid)
        {
            string returnVal = SendTradeQuery("foreigninventory", new Dictionary<string, object>
            {
                { "appid", appid },
                { "contextid", contextid },
                { "steamid", PartenarSteamID.ConvertToUInt64().ToString() },
            }, false);
            
            return returnVal;
        }

        public bool SendMessageWebCmd(string message, int logPos)
        {
            dynamic result = JObject.Parse(SendTradeQuery("chat", new Dictionary<string, object>
            {
                { "message", HttpUtility.UrlEncode( message ) },
                { "logpos", logPos },
            }));

            return result.success;
        }

        public bool CancelTradeWebCmd()
        {
            dynamic result = JObject.Parse(SendTradeQuery("cancel", null));
            return result.success;
        }

        public bool SetReadyWebCmd(bool isReady = true)
        {
            dynamic result = JObject.Parse(SendTradeQuery("toggleready", new Dictionary<string, object>
            {
                { "ready", isReady.ToString().ToLower() },
            }));
            return result.success;
        }

        public bool AcceptTradeWebCmd()
        {
            dynamic result = JObject.Parse(SendTradeQuery("confirm", null));
            return result.success;
        }

        public bool AddItemWebCmd(ulong assetID, int slot, int appID, long contextID)
        {
            dynamic result = JObject.Parse(SendTradeQuery("additem", new Dictionary<string, object>
            {
                { "appid", appID },
                { "contextid", contextID },
                { "itemid", assetID },
                { "slot", slot },
            }));
            return result.success;
        }

        public bool RemoveItemWebCmd(ulong assetID, int slot, int appID, long contextID)
        {
            dynamic result = JObject.Parse(SendTradeQuery("removeitem", new Dictionary<string, object>
            {
                { "appid", appID },
                { "contextid", contextID },
                { "itemid", assetID },
            }));
            return result.success;
        }

        string SendTradeQuery(string command, Dictionary<string, object> args, bool isPost = true)
        {
            string addr = FormatTradeURL(command);
            string commandArgs = MakeParams(args);

            string finalResult = "";

            try
            {
                string result = null;

                if (isPost)
                {
                    result = base.UploadString(addr, commandArgs);
                }
                else
                {
                    // if we're doing a GET, the arguments go into the address
                    addr += "?" + commandArgs;

                    result = base.DownloadString(addr);
                }

                finalResult = result;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error while querying steam trade API '{0}': {1}", command, ex.ToString());
                Console.ForegroundColor = ConsoleColor.White;

                return null;
            }

            dynamic obj = JObject.Parse(finalResult);

            if (obj.version != null)
                Version = obj.version;

            return finalResult;
        }

        string FormatTradeURL(string command)
        {
            return string.Format(STEAMAPI_TRADE, PartenarSteamID.ConvertToUInt64().ToString(), command);
        }

        string MakeParams(Dictionary<string, object> args = null)
        {
            if (args == null)
                args = new Dictionary<string, object>();

            args.Add("sessionid", HttpUtility.UrlEncode(this.sessionID));
            args.Add("version", Version.ToString());

            return string.Join("&", args.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var webReq = base.GetWebRequest(address) as HttpWebRequest;

            webReq.CookieContainer = new CookieContainer();
            webReq.CookieContainer.Add(address, new Cookie("steamLogin", this.token));
            webReq.CookieContainer.Add(address, new Cookie("steamLoginSecure", this.tokenSecure));
            webReq.CookieContainer.Add(address, new Cookie("sessionid", HttpUtility.UrlEncode(this.sessionID)));

            webReq.AllowAutoRedirect = true;
            webReq.Referer = FormatTradeURL(null);

            webReq.Accept = "application/json, text/javascript;q=0.9, */*;q=0.5";
            webReq.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            webReq.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.57 Safari/537.36";
            webReq.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.Revalidate);
            webReq.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            return webReq;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var webResp = base.GetWebResponse(request) as HttpWebResponse;
            location = webResp.Headers["Location"];

            return webResp;
        }
    }
}

