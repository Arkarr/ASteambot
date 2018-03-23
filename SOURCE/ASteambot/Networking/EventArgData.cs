using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class EventArgData : EventArgs
    {
        private readonly byte[] data;

        public EventArgData(byte[] data)
        {
            this.data = data;
        }

        public byte[] GetData
        {
            get { return data; }
        }
    }
}
