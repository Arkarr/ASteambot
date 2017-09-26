using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace SteamTrade.SteamMarket
{
    public class TradeTFBACKUP
    {
        public static double KeyValue = 2.49;
        public Item MannCoKey { get; private set; }
        public Units UnitsValue { get; private set; }
        public List<Item> Items { get; private set; }
        public string LastUpdate { get; private set; }

        public event EventHandler<EventArgs> ScanFinished;

        protected virtual void OnScannFinished(EventArgs e)
        {
            if (ScanFinished != null)
                ScanFinished(this, e);
        }

        public TradeTFBACKUP()
        {
            Items = new List<Item>();
        }

        public void ProcessJSON(string json)
        {
            JObject array = JObject.Parse(json);

            foreach (var x in array)
            {
                string name = x.Key;
                JToken value = x.Value;

                if (name.Equals("units"))
                {
                    UnitsValue = value.ToObject<Units>();
                }
                else if (name.Equals("items"))
                {
                    foreach (JProperty item in value)
                    {
                        string itemName = item.Name;// item.Name;
                        foreach (JProperty quality in item.Value)
                        {
                            TF2Quality QualityNumber = (TF2Quality)(Int32.Parse(quality.Name));
                            foreach (JProperty painted in quality.Value)
                            {
                                Value v = painted.Value.ToObject<Value>();
                                if (painted.Name == "regular")
                                    Items.Add(new Item(itemName, QualityNumber, v));
                                else
                                    Items.Add(new Item(itemName, QualityNumber, v, true));

                                if (itemName == "5021")
                                    MannCoKey = new Item(itemName, QualityNumber, v);
                            }
                        }
                    }
                }
                else if (name.Equals("last_modified"))
                {
                    LastUpdate = value.ToString();
                }
            }

            OnScannFinished(null);
        }

        public class Units
        {
            [JsonProperty(PropertyName = "k")]
            public double Key { get; private set; }

            [JsonProperty(PropertyName = "r")]
            public int Refined { get; private set; }

            [JsonProperty(PropertyName = "b")]
            public double Bud { get; private set; }
        }

        public enum TF2Quality
        {
            Normal,
            Genuine,
            Vintage = 3,
            Unusual = 5,
            Unique = 6,
            Community = 7,
            Valve = 8,
            SelfMade = 9,
            Strange = 11,
            Haunted = 13,
            Collector = 14,
            DecoratedWeapon = 15
        }

        public class Item
        {
            public Item(string name, TF2Quality quality, Value value, bool painted = false)
            {
                Name = name;
                Quality = quality;
                Value = value;
                Painted = painted;
            }

            public string Name { get; private set; }

            public TF2Quality Quality { get; private set; }

            public Value Value { get; private set; }

            public bool Painted { get; private set; }
        }

        public class Value
        {
            [JsonProperty(PropertyName = "hi")]
            public double Higest { get; private set; }

            [JsonProperty(PropertyName = "unsure")]
            public bool Unsure { get; private set; }

            [JsonProperty(PropertyName = "mid")]
            public double Middle { get; private set; }

            [JsonProperty(PropertyName = "unit")]
            public string Unit { get; private set; }

            [JsonProperty(PropertyName = "low")]
            public double Lowest { get; private set; }
        }
    }
}
