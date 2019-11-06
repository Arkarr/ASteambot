using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambotInterfaces
{
    public interface IASteambotChat
    {
        /// <summary>
        /// Triggered when the bot receive a steam chat message.
        /// Return true will intercept the message, false will block the message from being interpreted by ASteambot.
        /// </summary>
        /// <param name="steamFriends">Send message with that.</param>
        /// <param name="partenar">The steamID of the partenar wich wrote the message</param>
        /// <param name="message">The message sent by the partenar</param>
        bool HandleMessage(SteamFriends steamFriends, SteamID partenar, EChatEntryType messageType, string message, out string translationSentence);

        /// <summary>
        /// Triggered when the bot receive a steam chat message
        /// </summary>
        /// <param name="steamFriends">Send message with that.</param>
        /// <param name="InvitedID">Gets the SteamID of the user who was invited to the chat.</param>
        /// <param name="ChatRoomID">Gets the chat room SteamID.</param>
        /// <param name="PatronID">Gets the SteamID of the user who performed the invitation.</param>
        /// <param name="ChatRoomType">Gets the chat room type.</param>
        /// <param name="FriendChatID">Gets the SteamID of the chat friend.</param>
        /// <param name="ChatRoomName">Gets the name of the chat room.</param>
        /// <param name="GameID">Gets the GameID associated with this chat room, if it's a game lobby.</param>
        bool HandleInvitation(SteamFriends steamFriends, SteamID InvitedID, SteamID ChatRoomID, SteamID PatronID, EChatRoomType ChatRoomType, SteamID FriendChatID, string ChatRoomName, GameID GameID, out string translationSentence);
    }
}
