using System;
using System.IO;
using System.Text;
using SteamKit2;
using SteamKit2.Internal;

namespace ASteambot.CustomSteamMessageHandler
{
	internal sealed class CMsgClientAcknowledgeClanInvite : ISteamSerializableMessage
	{
		internal bool AcceptInvite { private get; set; }
		internal ulong ClanID { private get; set; }

		void ISteamSerializable.Deserialize(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using BinaryReader binaryReader = new BinaryReader(stream, Encoding.UTF8, true);

			ClanID = binaryReader.ReadUInt64();
			AcceptInvite = binaryReader.ReadBoolean();
		}

		EMsg ISteamSerializableMessage.GetEMsg() => EMsg.ClientAcknowledgeClanInvite;

		void ISteamSerializable.Serialize(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using BinaryWriter binaryWriter = new BinaryWriter(stream, Encoding.UTF8, true);

			binaryWriter.Write(ClanID);
			binaryWriter.Write(AcceptInvite);
		}
	}
}
