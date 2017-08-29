using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamTrade
{
    public class SteamMarketPrices
    {
        private SteamWeb steamWeb;
        private RootObject data { get; set; }
        public List<Item> Items { get; private set; }
        public string ErrorMessage { get; private set; }
        public int ResponseCode { get; private set; }

        public SteamMarketPrices(SteamWeb sw)
        {
            steamWeb = sw;
            Items = new List<Item>();
        }

        public bool ScanMarket(string backpackAPIKey, int appID)
        {
            try
            {
                string response = steamWeb.Fetch("http://backpack.tf/api/IGetMarketPrices/v1/?key=" + backpackAPIKey + "&appid=" + appID, "GET");
                if (!response.StartsWith("ERROR"))
                {
                    RootObject tmpData = Parse(response);
                    if (tmpData.response.success == 0)
                    {
                        if (data == null)
                            data = tmpData;
                        return false;
                    }
                    data = tmpData;
                    ResponseCode = tmpData.response.success;

                    //items = data.response.items;

                    lock (Items)
                    {
                        foreach (Item item in data.response.items)
                        {
                            var obj = Items.FirstOrDefault(x => x.name == item.name);
                            if (obj != null)
                            {
                                obj.last_updated = item.last_updated;
                                obj.quantity = item.quantity;
                                obj.value = item.value;
                            }
                            else
                            {
                                Items.Add(item);
                            }
                        }
                    }

                    return true;
                }
                else
                {
                    data = new RootObject();
                    data.response.message = response;
                    ErrorMessage = response;
                    return false;
                }
            }
            catch (Exception e)
            {
                data = new RootObject();
                data.response.message += e.Message;
                ErrorMessage += e.Message;
                return false;
            }
        }

        public void AddItem(Item item)
        {
            Items.Add(item);
        }

        private RootObject Parse(string jsonString)
        {
            dynamic jsonObject = JsonConvert.DeserializeObject(jsonString);
            RootObject parsed = null;
            if (jsonObject.response.success == 0)
            {
                //The response object is not valid and has a message...
                parsed = new RootObject()
                {
                    response = new Response()
                    {
                        success = jsonObject.response.success,
                        message = jsonObject.response.message
                    }
                };
            }
            else
            {
                parsed = new RootObject()
                {
                    response = new Response()
                    {
                        success = jsonObject.response.success,
                        current_time = jsonObject.response.current_time,
                        items = ParseItems(jsonObject.response.items)
                    }
                };
            }
            return parsed;
        }

        private List<Item> ParseItems(dynamic items)
        {
            List<Item> itemList = new List<Item>();
            foreach (var item in items)
            {
                itemList.Add(new Item()
                {
                    name = item.Name,
                    last_updated = item.Value.last_updated,
                    quantity = item.Value.quantity,
                    value = item.Value.value
                });
            }
            return itemList;
        }

        public class Item
        {
            public Item() { }
            public Item(string name, int lastUpdated, int quantity, double value)
            {
                this.name = name;
                this.last_updated = last_updated;
                this.quantity = quantity;
                this.value = value;
            }

            public string name
            {
                get;
                set;
            }
            public int last_updated { get; set; }
            public int quantity { get; set; }
            public double value { get; set; }
        }

        public class Response
        {
            public int success { get; set; }
            public string message { get; set; }
            public int current_time { get; set; }
            public List<Item> items { get; set; }
        }

        public class RootObject
        {
            public Response response { get; set; }

            public RootObject()
            {
                response = new Response();
            }
        }
    }
}
