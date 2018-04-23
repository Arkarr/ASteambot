using SteamKit2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ASteambot
{
    public class SteamProfile
    {
        [XmlRoot(ElementName = "mostPlayedGame")]
        public class MostPlayedGame
        {
            [XmlElement(ElementName = "gameName")]
            public string GameName { get; set; }
            [XmlElement(ElementName = "gameLink")]
            public string GameLink { get; set; }
            [XmlElement(ElementName = "gameIcon")]
            public string GameIcon { get; set; }
            [XmlElement(ElementName = "gameLogo")]
            public string GameLogo { get; set; }
            [XmlElement(ElementName = "gameLogoSmall")]
            public string GameLogoSmall { get; set; }
            [XmlElement(ElementName = "hoursPlayed")]
            public string HoursPlayed { get; set; }
            [XmlElement(ElementName = "hoursOnRecord")]
            public string HoursOnRecord { get; set; }
            [XmlElement(ElementName = "statsName")]
            public string StatsName { get; set; }
        }

        [XmlRoot(ElementName = "mostPlayedGames")]
        public class MostPlayedGames
        {
            [XmlElement(ElementName = "mostPlayedGame")]
            public List<MostPlayedGame> MostPlayedGame { get; set; }
        }

        [XmlRoot(ElementName = "profile")]
        public class SteamProfileInfos
        {
            [XmlElement(ElementName = "steamID64")]
            public string SteamID64 { get; set; }
            [XmlElement(ElementName = "steamID")]
            public string Name { get; set; }
            [XmlElement(ElementName = "onlineState")]
            public string OnlineState { get; set; }
            [XmlElement(ElementName = "stateMessage")]
            public string StateMessage { get; set; }
            [XmlElement(ElementName = "privacyState")]
            public string PrivacyState { get; set; }
            [XmlElement(ElementName = "visibilityState")]
            public string VisibilityState { get; set; }
            [XmlElement(ElementName = "avatarIcon")]
            public string AvatarIcon { get; set; }
            [XmlElement(ElementName = "avatarMedium")]
            public string AvatarMedium { get; set; }
            [XmlElement(ElementName = "avatarFull")]
            public string AvatarFull { get; set; }
            [XmlElement(ElementName = "vacBanned")]
            public string VacBanned { get; set; }
            [XmlElement(ElementName = "tradeBanState")]
            public string TradeBanState { get; set; }
            [XmlElement(ElementName = "isLimitedAccount")]
            public string IsLimitedAccount { get; set; }
            [XmlElement(ElementName = "customURL")]
            public string CustomURL { get; set; }
            [XmlElement(ElementName = "memberSince")]
            public string MemberSince { get; set; }
            [XmlElement(ElementName = "steamRating")]
            public string SteamRating { get; set; }
            [XmlElement(ElementName = "hoursPlayed2Wk")]
            public string HoursPlayed2Wk { get; set; }
            [XmlElement(ElementName = "headline")]
            public string Headline { get; set; }
            [XmlElement(ElementName = "location")]
            public string Location { get; set; }
            [XmlElement(ElementName = "realname")]
            public string Realname { get; set; }
            [XmlElement(ElementName = "summary")]
            public string Summary { get; set; }
            [XmlElement(ElementName = "mostPlayedGames")]
            public MostPlayedGames MostPlayedGames { get; set; }
        }

        public static SteamProfileInfos LoadSteamProfile(SteamTrade.SteamWeb steamWeb, SteamID steamID)
        {
            string response = steamWeb.Fetch("http://steamcommunity.com/profiles/"+steamID.ConvertToUInt64()+"/?xml=1", "GET");

            if (response != String.Empty)
            {
                response = response.Replace("\t", "").Replace("\n", "").Replace("\r", "").Trim();
                int index = response.IndexOf("<profile>");
                if (index != -1)
                {
                    response = response.Substring(index);
                    XmlSerializer serializer = new XmlSerializer(typeof(SteamProfileInfos));
                    using (TextReader reader = new StringReader(response))
                    {
                        return (SteamProfileInfos)serializer.Deserialize(reader);
                    }
                }
            }

            return null;
        }
    }
}
