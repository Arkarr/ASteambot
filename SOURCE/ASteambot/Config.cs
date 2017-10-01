﻿using System;
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
        //public string BackpacktfAPIKey { get; private set; }
        //public string TradeTFAPIKey { get; private set; }
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


        public Config() { }

        public bool LoadConfig()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "/config.cfg";

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
                /*else if (line.StartsWith("backpacktf_apikey="))
                    BackpacktfAPIKey = line.Replace("backpacktf_apikey=", "");
                else if (line.StartsWith("tradetf_apikey="))
                    TradeTFAPIKey = line.Replace("tradetf_apikey=", "");*/
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
            }

            file.Close();

            return true;
        }
    }
}
