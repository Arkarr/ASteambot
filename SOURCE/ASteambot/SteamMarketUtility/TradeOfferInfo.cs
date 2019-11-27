using System;
using SteamTrade.TradeOffer;

namespace ASteambot.SteamMarketUtility
{
    public class TradeOfferInfo
    {
        public string Splink { get; private set; }
        public string Spname { get; private set; }

        public string Sppicture { get; private set; }

        public string TradeofferID { get; private set; }

        public string ToStatus { get; private set; }

        public TradeOfferInfo(string spl, string spn, string spp, string toID, TradeOfferState tostatus)
        {
            this.Splink = spl;
            this.Spname = spn;
            this.Sppicture = spp;
            this.TradeofferID = toID;
            this.ToStatus = GetStatus(tostatus);
        }

        private string GetStatus(TradeOfferState tostatus)
        {
            switch(tostatus)
            {
                case TradeOfferState.TradeOfferStateAccepted: return "<span class=\"badge badge-success\">ACCEPTED</span>";
                case TradeOfferState.TradeOfferStateActive: return "<span class=\"badge badge-primary\">ACTIVE</span>";
                case TradeOfferState.TradeOfferStateCanceled: return "<span class=\"badge badge-danger\">CANCELED</span>";
                case TradeOfferState.TradeOfferStateDeclined: return "<span class=\"badge badge-danger\">DECLINED</span>";
                case TradeOfferState.TradeOfferStateCountered: return "<span class=\"badge badge-warning\">COUNTERED</span>";
                case TradeOfferState.TradeOfferStateExpired: return "<span class=\"badge badge-danger\">EXPIRED</span>";
                case TradeOfferState.TradeOfferStateInvalid: return "<span class=\"badge badge-danger\">INVALID</span>";
                case TradeOfferState.TradeOfferStateInvalidItems: return "<span class=\"badge badge-danger\">INVALID</span>";
                case TradeOfferState.TradeOfferStateUnknown: return "<span class=\"badge badge-danger\">UNKNOW</span>";
                default: return "<span class=\"badge badge-success\">UKNOW ("+ tostatus.ToString() + ")</span>";
            }
        }
    }
}
