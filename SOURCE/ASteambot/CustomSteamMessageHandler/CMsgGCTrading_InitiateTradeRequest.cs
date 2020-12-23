using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.TF2.Internal;
using SteamKit2.Internal;

namespace ASteambot.CustomSteamMessageHandler
{
    public class MsgGCTrading_InitiateTradeRequest : IGCSerializableMessage
    {
        public uint TradeRequestID { get; set; }
        public SteamID OtherClient { get; set; }

        public uint GetEMsg()
        {
            return (uint)EGCItemMsg.k_EMsgGCTrading_InitiateTradeRequest;
        }

        public void Serialize(Stream stream)
        {
            var bw = new BinaryWriter(stream);

            bw.Write(TradeRequestID);
            bw.Write(OtherClient.ConvertToUInt64());
        }

        public void Deserialize(Stream stream)
        {
            var br = new BinaryReader(stream);

            TradeRequestID = br.ReadUInt32();
            OtherClient = br.ReadUInt64();
        }
    }
}
