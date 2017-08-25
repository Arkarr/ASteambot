using SteamKit2;
using SteamKit2.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.SteamGroups
{
    /// <summary>
    /// Message used to invite a user to a group(clan).
    /// </summary>
    public class CMsgInviteUserToGroup : ISteamSerializableMessage, ISteamSerializable
    {
        EMsg ISteamSerializableMessage.GetEMsg()
        {
            return EMsg.ClientInviteUserToClan;
        }

        public CMsgInviteUserToGroup()
        {

        }

        /// <summary>
        /// Who is being invited.
        /// </summary>
        public ulong Invitee = 0;

        /// <summary>
        /// Group to invite to
        /// </summary>
        public ulong GroupID = 0;

        /// <summary>
        /// Not known yet. All data seen shows this as being true.
        /// See what happens if its false? 
        /// </summary>
        public bool UnknownInfo = true;

        void ISteamSerializable.Serialize(Stream stream)
        {
            try
            {
                BinaryWriter bw = new BinaryWriter(stream);
                bw.Write(Invitee);
                bw.Write(GroupID);
                bw.Write(UnknownInfo);
            }//try
            catch
            {
                throw new IOException();
            }//catch
        }//Serialize()

        void ISteamSerializable.Deserialize(Stream stream)
        {
            try
            {
                BinaryReader br = new BinaryReader(stream);
                Invitee = br.ReadUInt64();
                GroupID = br.ReadUInt64();
                UnknownInfo = br.ReadBoolean();
            }//try
            catch
            {
                throw new IOException();
            }//catch
        }//Deserialize()
    }
}
