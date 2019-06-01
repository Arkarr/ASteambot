using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class DataQueue : List<byte[]>
    {
        public event EventHandler<EventArgData> OnAdd;

        public new void Add(byte[] data)
        {
            if (null != OnAdd)
                OnAdd(this, new EventArgData(data));

            base.Add(data);
        }
    }
}
