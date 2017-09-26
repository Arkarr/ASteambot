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
        public string Name { get; set; }
        public string LastUpdated { get; set; }
        public int Quantity { get; set; }
        public double Value { get; set; }
        public int AppID { get; set; }
        
        public Item(string Name, string LastUpdate, int quantity, double value, int appID)
        {
            this.Name = Name;
            this.LastUpdated = LastUpdate;
            this.Value = value;
            this.Quantity = quantity;
            this.AppID = appID;
        }
    }
}
