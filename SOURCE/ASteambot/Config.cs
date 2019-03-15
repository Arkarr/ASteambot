using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot
{
    //Maybe make it static
    public class Config
    {
        public string SteamUsername { get; private set; }
        public string SteamPassword { get; private set; }
        public string SteamAPIKey { get; private set; }
        public string SteamAdmins { get; private set; }
        public string DatabaseServer { get; private set; }
        public string DatabaseUser { get; private set; }
        public string DatabasePassword { get; private set; }
        public string DatabaseName { get; private set; }
        public string DatabasePort { get; private set; }
        public string TCPServerPort { get; private set; }
        public string TCPPassword { get; private set; }
        public bool SteamMarket_CSGO { get; private set; }
        public bool SteamMarket_TF2 { get; private set; }
        public bool SteamMarket_DOTA2 { get; private set; }
        public string ArkarrAPIKey { get; private set; }
        public bool DisableMarketScan { get; private set; }
        public bool DisableWelcomeMessage { get; private set; }
        public bool DisableAutoUpdate { get; private set; }
        public bool DisplayLocation { get; private set; }

        public Config() { }

        public bool LoadConfig()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "config.cfg";

            if (!File.Exists(path))
                return false;

            string line;
            StreamReader file = new StreamReader(@path);
            while ((line = file.ReadLine()) != null)
            {
                if (line.StartsWith("//"))
                    continue;

                if (line.StartsWith("steam_username="))
                    SteamUsername = line.Replace("steam_username=", "");
                else if (line.StartsWith("steam_password="))
                    SteamPassword = line.Replace("steam_password=", "");
                else if (line.StartsWith("steam_apikey="))
                    SteamAPIKey = line.Replace("steam_apikey=", "");
                else if (line.StartsWith("steam_admins="))
                    SteamAdmins = line.Replace("steam_admins=", "");
                else if (line.StartsWith("database_server="))
                    DatabaseServer = line.Replace("database_server=", "");
                else if (line.StartsWith("database_user="))
                    DatabaseUser = line.Replace("database_user=", "");
                else if (line.StartsWith("database_password="))
                    DatabasePassword = line.Replace("database_password=", "");
                else if (line.StartsWith("database_name="))
                    DatabaseName = line.Replace("database_name=", "");
                else if (line.StartsWith("database_port="))
                    DatabasePort = line.Replace("database_port=", "");
                else if (line.StartsWith("TCP_ServerPort="))
                    TCPServerPort = line.Replace("TCP_ServerPort=", "");
                else if (line.StartsWith("TCP_Password="))
                    TCPPassword = line.Replace("TCP_Password=", "");
                else if (line.StartsWith("SteamMarket_CSGO="))
                    SteamMarket_CSGO = line.Replace("SteamMarket_CSGO=", "").Equals("YES");
                else if (line.StartsWith("SteamMarket_TF2="))
                    SteamMarket_TF2 = line.Replace("SteamMarket_TF2=", "").Equals("YES");
                else if (line.StartsWith("SteamMarket_DOTA2="))
                    SteamMarket_DOTA2 = line.Replace("SteamMarket_DOTA2=", "").Equals("YES");
                else if (line.StartsWith("ArkarrAPIKey="))
                    ArkarrAPIKey = line.Replace("ArkarrAPIKey=", "");
                else if (line.StartsWith("DisableMarketScan="))
                    DisableMarketScan = line.Replace("DisableMarketScan=", "").Equals("YES");
                else if (line.StartsWith("DisableWelcomeMessage="))
                    DisableWelcomeMessage = line.Replace("DisableWelcomeMessage=", "").Equals("YES");
                else if (line.StartsWith("DisableUpdater="))
                    DisableAutoUpdate = line.Replace("DisableUpdater=", "").Equals("YES");
                else if (line.StartsWith("DisplayLocation="))
                    DisplayLocation = line.Replace("DisplayLocation=", "").Equals("YES");
            }

            if (!ValidateConfig())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Some fields haven't been set in config file. Press a key to close ASteambot.");
                Console.ForegroundColor = ConsoleColor.White;
                Console.ReadKey();
                Environment.Exit(0);
            }

            if (!IsValidTCPPassword(TCPPassword))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("TCP_Password can not contains '/', '&' or '|' characters.");
                Console.ForegroundColor = ConsoleColor.White;
                Console.ReadKey();
                Environment.Exit(0);
            }

            file.Close();

            return true;
        }

        private bool ValidateConfig()
        {
            if (SteamUsername == null || SteamUsername.Length == 0) return false;
            if (SteamPassword == null || SteamPassword.Length == 0) return false;
            if (SteamAPIKey == null || SteamAPIKey.Length == 0) return false;
            if (SteamAdmins == null || SteamAdmins.Length == 0) return false;
            if (DatabaseServer == null || DatabaseServer.Length == 0) return false;
            if (DatabaseUser == null || DatabaseUser.Length == 0) return false;
            if (DatabasePassword == null || DatabasePassword.Length == 0) return false;
            if (DatabaseName == null || DatabaseName.Length == 0) return false;
            if (DatabasePort == null || DatabasePort.Length == 0) return false;
            if (TCPServerPort == null || TCPServerPort.Length == 0) return false;
            if (TCPPassword == null || TCPPassword.Length == 0) return false;
            if (ArkarrAPIKey == null || ArkarrAPIKey.Length == 0) return false;

            return true;
        }

        private bool IsValidTCPPassword(string pswd)
        {
            foreach (char c in pswd)
            {
                if (c == '|' || c == '&' || c == '/')
                    return false;
            }

            return true;
        }
    }
}
