using Newtonsoft.Json;

namespace ASteambot.SteamTrade
{
    internal class GetRsaKeyResponse
    {
        [JsonProperty(PropertyName = "success")]
        internal bool Success { get; set; }
        [JsonProperty(PropertyName = "publickey_mod")]
        internal string PublicKeyMod { get; set; }
        [JsonProperty(PropertyName = "publickey_exp")]
        internal string PublicKeyExp { get; set; }
        [JsonProperty(PropertyName = "timestamp")]
        internal string Timestamp { get; set; }
        [JsonProperty(PropertyName = "token_gid")]
        internal string TokenGid { get; set; }
    }
}