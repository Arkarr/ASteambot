using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using ASteambot;
using ASteambotInterfaces;
using ASteambotInterfaces.ASteambotInterfaces;
using Newtonsoft.Json;
using SteamKit2;
using SteamTrade.TradeWebAPI;

namespace SteamTrade
{

    /// <summary>
    /// Generic Steam Backpack Interface
    /// </summary>
    public class GenericInventory : IASteambotInventory
    {
        private readonly SteamWeb SteamWeb;
        
        public GenericInventory(SteamID steamID, SteamWeb steamWeb)
        {
            SteamWeb = steamWeb;

            if (steamID != null)
            {
                object[] args = new object[2];
                args[0] = steamID;
                args[1] = this;

                Program.ExecuteModuleFonction("Start", args);
            }
        }

        public Dictionary<ulong, IItem> items
        {
            get
            {
                if (_loadTask == null)
                    return null;
                _loadTask.Wait();
                return _items;
            }
        }

        public Dictionary<string, ItemDescription> descriptions
        {
            get
            {
                if (_loadTask == null)
                    return null;
                _loadTask.Wait();
                return _descriptions;
            }
        }

        public List<string> errors
        {
            get
            {
                if (_loadTask == null)
                    return null;
                _loadTask.Wait();
                return _errors;
            }
        }

        public bool isLoaded = false;

        private Task _loadTask;
        private Dictionary<string, ItemDescription> _descriptions = new Dictionary<string, ItemDescription>();
        private Dictionary<ulong, IItem> _items = new Dictionary<ulong, IItem>();
        private List<string> _errors = new List<string>();

        public class Item : TradeUserAssets, IItem
        {
            public Item(int appid, long contextid, ulong assetid, string descriptionid, int amount = 1) : base(appid, contextid, assetid, amount)
            {
                this.descriptionid = descriptionid;
            }

            public string descriptionid { get; private set; }

            public override string ToString()
            {
                return string.Format("id:{0}, appid:{1}, contextid:{2}, amount:{3}, descriptionid:{4}",
                    assetid, appid, contextid, amount, descriptionid);
            }
        }

        public class ItemDescription : IItemDescription
        {
            public string name { get; set; }
            public string type { get; set; }
            public bool tradable { get; set; }
            public bool marketable { get; set; }
            public string url { get; set; }
            public long classid { get; set; }
            public long market_fee_app_id { get; set; }
            public string market_hash_name { get; set; }

            public Dictionary<string, string> app_data { get; set; }

            public void debug_app_data()
            {
                Console.WriteLine("\n\"" + name + "\"");
                if (app_data == null)
                {
                    Console.WriteLine("Doesn't have app_data");
                    return;
                }

                foreach (var value in app_data)
                {
                    Console.WriteLine(string.Format("{0} = {1}", value.Key, value.Value));
                }
                Console.WriteLine("");
            }
        }

        /// <summary>
        /// Returns information (such as item name, etc) about the given item.
        /// This call can fail, usually when the user's inventory is private.
        /// </summary>
        public IItemDescription getDescription(ulong id)
        {
            if (_loadTask == null)
                return null;
            _loadTask.Wait();

            try
            {
                return _descriptions[_items[id].descriptionid];
            }
            catch
            {
                return null;
            }
        }

        public void load(int appid, IEnumerable<long> contextIds, SteamID steamid)
        {
            List<long> contextIdsCopy = contextIds.ToList();
            _loadTask = Task.Factory.StartNew(() => loadImplementation(appid, contextIdsCopy, steamid));
        }

        private void loadImplementation(int appid, IEnumerable<long> contextIds, SteamID steamid)
        {
            dynamic invResponse;
            string url = "<empty>";
            isLoaded = false;
            Dictionary<string, string> tmpAppData;

            _items.Clear();
            _descriptions.Clear();
            _errors.Clear();

            try
            {
                foreach (long contextId in contextIds)
                {
                    string moreStart = null;
                    bool more = false;
                    do
                    {
                        var data = String.IsNullOrEmpty(moreStart) ? null : new NameValueCollection {{"start", moreStart}};
                        url = String.Format("http://steamcommunity.com/profiles/{0}/inventory/json/{1}/{2}/", steamid.ConvertToUInt64(), appid, contextId);
                        
                        string response = SteamWeb.Fetch(url, "GET", data);
                        invResponse = JsonConvert.DeserializeObject(response);

                        if (invResponse == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Unable to find items in inventory because bot is scanning inventory too fast. Try again later.");
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        else if (invResponse.success == false)
                        {
                            _errors.Add("Fail to open backpack: " + invResponse.Error);
                            continue;
                        }
                        else
                        {
                            //rgInventory = Items on Steam Inventory 
                            foreach (var item in invResponse.rgInventory)
                            {
                                foreach (var itemId in item)
                                {
                                    ulong id = (ulong)itemId.id;
                                    if (!_items.ContainsKey(id))
                                    {
                                        string descriptionid = itemId.classid + "_" + itemId.instanceid;
                                        _items.Add((ulong)itemId.id, new Item(appid, contextId, (ulong)itemId.id, descriptionid));
                                        break;
                                    }
                                }
                            }

                            // rgDescriptions = Item Schema (sort of)
                            foreach (var description in invResponse.rgDescriptions)
                            {
                                foreach (var class_instance in description) // classid + '_' + instenceid 
                                {
                                    string key = "" + (class_instance.classid ?? '0') + "_" + (class_instance.instanceid ?? '0');
                                    if (!_descriptions.ContainsKey(key))
                                    {
                                        if (class_instance.app_data != null)
                                        {
                                            tmpAppData = new Dictionary<string, string>();
                                            foreach (var value in class_instance.app_data)
                                            {
                                                tmpAppData.Add("" + value.Name, "" + value.Value);
                                            }
                                        }
                                        else
                                        {
                                            tmpAppData = null;
                                        }

                                        _descriptions.Add(key,
                                            new ItemDescription()
                                            {
                                                name = class_instance.name,
                                                type = class_instance.type,
                                                market_hash_name = class_instance.market_hash_name,
                                                marketable = (bool)class_instance.marketable,
                                                tradable = (bool)class_instance.tradable,
                                                classid = long.Parse((string)class_instance.classid),
                                                url = (class_instance.actions != null && class_instance.actions.First["link"] != null ? class_instance.actions.First["link"] : ""),
                                                app_data = tmpAppData,
                                                market_fee_app_id = (class_instance.market_fee_app != null ? class_instance.market_fee_app : 0),
                                            }
                                        );
                                        break;
                                    }

                                }
                            }

                            try
                            {
                                moreStart = invResponse.more_start;
                            }
                            catch (Exception e)
                            {
                                moreStart = null;
                            }

                            try
                            {
                                more = invResponse.more;
                            }
                            catch (Exception e)
                            {
                                more = false;
                            }
                        }

                    } while (!String.IsNullOrEmpty(moreStart) && moreStart.ToLower() != "false" && more == true);
                }//end for (contextId)
            }//end try
            catch (Exception e)
            {
                _errors.Add("Exception: " + e);
                _errors.Add("For more infos; visit this link :\n" + url);
            }

            isLoaded = true;
        }
    }
}
