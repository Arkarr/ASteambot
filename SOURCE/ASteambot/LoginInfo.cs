using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using SteamKit2;

namespace ASteambot
{
    public class LoginInfo
    {
        public string API { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public string AuthCode { get; private set; }
        public byte[] SentryFileHash { get; private set; }
        public string TwoFactorCode { get; private set; }
        public string SentryFileName { get; private set; }
        public int LoginFailCount { get; set; }

        public LoginInfo(string username, string password, string api)
        {
            Username = username;
            Password = password;
            API = api;

            SentryFileName = "sentry-" + username + ".bin";
            
            if (File.Exists(SentryFileName))
            {
                byte[] sentryFile = File.ReadAllBytes(SentryFileName);
                SentryFileHash = CryptoHelper.SHAHash(sentryFile);
            }
        }

        public void SetTwoFactorCode(string twoFactorCode)
        {
            TwoFactorCode = twoFactorCode;
        }

        public void SetAuthCode(string authCode)
        {
            AuthCode = authCode;
        }
    }
}
