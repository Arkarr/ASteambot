using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class NetworkCode
    {
        public enum ASteambotCode
        {
            Core = 0,
            HookChat = 1,
            Unhookchat = 2,
            Simple = 3,
            TradeOfferSuccess = 4,
            TradeOfferDecline = 5,
            ScanInventory = 6,
            CreateTradeOffer = 7,
            NotFriends = 8,
            TradeToken = 9,
            FriendInvite = 10,
            ReportPlayer = 11,
            InviteSteamGroup = 12
        }
    }
}
