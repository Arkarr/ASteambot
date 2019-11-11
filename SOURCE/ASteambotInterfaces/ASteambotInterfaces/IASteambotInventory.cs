using ASteambotInterfaces.ASteambotInterfaces;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambotInterfaces
{
    public interface IASteambotInventory
    {
        Dictionary<ulong, IItem> items { get; }

        IItemDescription getDescription(ulong id);

        void load(int appid, IEnumerable<long> contextIds, SteamID steamid);
    }
}
