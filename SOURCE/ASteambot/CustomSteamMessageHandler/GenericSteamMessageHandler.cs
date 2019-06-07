using SteamKit2;
using SteamKit2.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SteamKit2.SteamFriends;

namespace ASteambot.CustomSteamMessageHandler
{
    public class GenericSteamMessageHandler : ClientMsgHandler
    {
        public class OnSteamMessageReceived : CallbackMsg
        {
            public SteamID Partenar { get; private set; }

            public EChatEntryType ChatMsgType { get; private set; }

            public string Message { get; private set; }

            internal OnSteamMessageReceived(SteamID partenar, EChatEntryType type, string msg)
            {
                this.Partenar = partenar;
                this.ChatMsgType = type;
                this.Message = msg;
            }
        }

        public override void HandleMsg(IPacketMsg packetMsg)
        {
            if (packetMsg == null)
            {
                throw new ArgumentNullException(nameof(packetMsg));
            }

            switch (packetMsg.MsgType)
            {
                case EMsg.ClientUDSInviteToGame: HandleGameInviteMsg(packetMsg); break;

                default: return;
            }
        }

        public void HandleGameInviteMsg(IPacketMsg packetMsg)
        {
            //Console.WriteLine("fegewg");
        }
    }
}
