using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambotInterfaces
{
    public interface ASteambotEntryPointInventory
    {
        bool Start(SteamID botSteamID, IASteambotInventory inventory);
    }
}
