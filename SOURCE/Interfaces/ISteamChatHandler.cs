using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Interfaces
{
    public interface ISteamChatHandler
    {
        /// <summary>
        /// Triggered when the bot receive a steam chat message
        /// </summary>
        /// <param name="partenar">The steamID of the partenar wich wrote the message</param>
        /// <param name="message">The message sent by the partenar</param>
        void HandleMessage(SteamFriends steamFriends, SteamID partenar, string message);
    }
}
