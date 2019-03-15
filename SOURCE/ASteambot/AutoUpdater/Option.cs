using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.AutoUpdater
{
    public class Option
    {
        public string Name { get; private set; }
        public string Commentary { get; private set; }
        public string Value { get; set; }

        public Option(string name, string commentary, string value = "")
        {
            Name = name;
            Commentary = commentary;
            Value = value;
        }
    }
}
