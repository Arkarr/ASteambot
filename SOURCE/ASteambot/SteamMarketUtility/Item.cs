using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.SteamMarketUtility
{
    public class Item
    {

        [JsonProperty("market_hash_name")]
        public string Name { get; set; }
        [JsonProperty("last_updated")]
        public long LastUpdated { get; set; }
        [JsonProperty("quantity")]
        public int Quantity { get; set; }
        [JsonProperty("normal_price")]
        public double Value { get; set; }
        [JsonProperty("appid")]
        public int AppID { get; set; }
        [JsonProperty("img")]
        public string Image { get; set; }

        public Item(string Name, long LastUpdate, int quantity, double value, int appID, string img)
        {
            this.Name = Name;
            this.LastUpdated = LastUpdate;
            this.Value = value;
            this.Quantity = quantity;
            this.AppID = appID;
            this.Image = img;
        }
    }
}
