using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamTrade
{
    public class EventArgItemScanned : EventArgs
    {
        private readonly SteamMarket.Item item;

        //private readonly int port;
        //private readonly IPAddress ip;

        public EventArgItemScanned(SteamMarket.Item item)
        {
            this.item = item;

            /*IPEndPoint ipendpoint = ((IPEndPoint)socket.RemoteEndPoint);

            ip = ipendpoint.Address;
            port = ipendpoint.Port;*/
        }

        public SteamMarket.Item GetItem
        {
            get { return item; }
        }
    }
}
