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
            var chatMsg = new ClientMsgProtobuf<CMsgClientUDSInviteToGame>(packetMsg);
            //
            //steam_id_src = 76561198044361291
            // I got the message, now to create it is for later.
            int i = 0;
        }

        public void SendGameInvite(SteamID inviter, SteamID target, string url)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var chatMsg = new ClientMsgProtobuf<CMsgClientUDSInviteToGame>(EMsg.ClientUDSInviteToGame);

            chatMsg.Body.connect_string = "+tf_party_request_join_user " + 76561197991854757;
            chatMsg.Body.connect_stringSpecified = true;
            chatMsg.Body.steam_id_dest = target;//new SteamID(76561197991854757);
            chatMsg.Body.steam_id_destSpecified = true;
            chatMsg.Body.steam_id_src = inviter;
            chatMsg.Body.steam_id_srcSpecified = true;

            chatMsg.SteamID = target;

            this.Client.Send(chatMsg);
        }
    }
}
