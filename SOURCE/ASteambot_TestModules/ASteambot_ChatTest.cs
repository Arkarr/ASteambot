using ASteambot.Interfaces;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot_TestModules
{
    public class ASteambot_ChatTest : ISteamChatHandler
    {
        public void HandleMessage(SteamFriends steamFriends, SteamID partenar, string message)
        {
            steamFriends.SendChatMessage(partenar, EChatEntryType.ChatMsg, "Hey ! You wrote me a message : '" + message + "' !");
        }
    }
}
