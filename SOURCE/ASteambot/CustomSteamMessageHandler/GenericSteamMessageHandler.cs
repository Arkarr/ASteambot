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
            var chatMsg = new ClientMsgProtobuf<CMsgClientChatInvite>(packetMsg);
            int i = 0;
        }

        public void SendGameInvite(SteamID inviter, SteamID target, string url)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var chatMsg = new ClientMsgProtobuf<CMsgClientChatInvite>(EMsg.AMChatInvite);

            /*
             * 
            object[] args = new object[9];
            args[0] = SteamFriends;
            args[1] = callback.InvitedID;
            args[2] = callback.ChatRoomID;
            args[3] = callback.PatronID;
            args[4] = callback.ChatRoomType;
            args[5] = callback.FriendChatID;
            args[6] = callback.ChatRoomName;
            args[7] = callback.GameID;
            args[8] = "";

             */

            chatMsg.Body.chat_name = "";
            chatMsg.Body.chatroom_type = (int)EChatRoomType.Friend;
            //chatMsg.Body.steam_id_dest = target.ConvertToUInt64();
            chatMsg.Body.chatroom_typeSpecified = true;
            chatMsg.Body.chat_name = "";
            chatMsg.Body.chat_nameSpecified = false;
            chatMsg.Body.game_id = 440;
            chatMsg.Body.game_idSpecified = true;
            chatMsg.Body.steam_id_chat = inviter;
            chatMsg.Body.steam_id_chatSpecified = true;
            chatMsg.Body.steam_id_friend_chat = inviter;
            chatMsg.Body.steam_id_friend_chatSpecified = true;
            chatMsg.Body.steam_id_invited = target;
            chatMsg.Body.steam_id_invitedSpecified = true;
            chatMsg.Body.steam_id_patron = inviter;
            chatMsg.Body.steam_id_patronSpecified = true;

            chatMsg.SteamID = inviter;

            this.Client.Send(chatMsg);
        }
    }
}
