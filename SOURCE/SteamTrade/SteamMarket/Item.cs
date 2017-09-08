using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamTrade.SteamMarket
{
    public class Item
    {
        public Item(string name, string json)
        {
            JObject jObject = JObject.Parse(json);
            Name = name;
            LastUpdated = DateTime.Now.ToString("dd/MM/yyyy") + DateTime.Now.ToString("HH:mm");
            Quantity = (jObject["volume"] == null ? 0 : Int32.Parse(((string)jObject["volume"]).Replace(",", "")));
            Value = GetValue(jObject);
        }

        private double GetValue(JObject jobject)
        {
            string value = (string)jobject["lowest_price"];
            if (value == null)
                value = (string)jobject["median_price"];

            if (value == null)
                return 0.0;

            return Double.Parse(value.Replace(",", String.Empty).Replace("$", String.Empty));
        }

        public Item(string Name, string LastUpdate, int quantity, double value, int appID)
        {
            this.Name = Name;
            this.LastUpdated = LastUpdate;
            this.Value = value;
            this.Quantity = quantity;
            this.AppID = appID;
        }

        public string Name { get; set; }
        public string LastUpdated { get; set; }
        public int Quantity { get; set; }
        public double Value { get; set; }
        public int AppID { get; set; }
    }
}
