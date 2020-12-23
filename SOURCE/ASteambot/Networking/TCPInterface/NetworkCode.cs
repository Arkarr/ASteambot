using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class NetworkCode
    {
        public static readonly int MAX_CHUNK_SIZE = 900;
        public static readonly int MSG_FOR_ALL_MODULE = -2;
        
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
            InviteSteamGroup = 12,
            ScanInventoryIMG = 13,
            ExecuteCommand = 14,
            Disconnect = 15,
            SGAnnoucement = 16,
            SteamID = 17,
            TradeOfferInformation = 18,
            SendChatGroupMsg = 19,
            CreateQuickTrade = 20
        }
    }
}
