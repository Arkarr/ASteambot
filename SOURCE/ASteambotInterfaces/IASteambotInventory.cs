using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambotInterfaces
{
    public interface IASteambotInventory
    {
        /// <summary>
        /// Triggered when the bot's inventory change.
        /// </summary>
        /// <param name="steamFriends">Send message with that.</param>
        void InventoryItemsChanged(string test);
    }
}
