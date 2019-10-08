using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using ASteambot.Networking;
using System.Threading;

using System.Web;
using System.Globalization;
using ASteambot.SteamMarketUtility;
using System.IO;

namespace ASteambot
{
    public class Manager
    {
        public static Bot SelectedBot { get; private set; }
        public List<Bot> OnlineBots { get; private set; }
        
        public List<GameServer> Servers { get; private set; }
        public Config Config { get; private set; }

        private bool Running;
        private List<Bot> bots;
        private TCPInterface socketServer;
        private Thread threadSocket;

        public Manager(Config Config)
        {
            this.Config = Config;
            bots = new List<Bot>();
            OnlineBots = new List<Bot>();
            Servers = new List<GameServer>();
        }
        
        public void DisconnectServer(int serverID)
        {
            Servers.RemoveAll(gs => gs.ServerID == serverID);
        }

        public void Start()
        {
            Running = true;

            StartSocketServer(Int32.Parse(Config.TCPServerPort));

            while (Running)
            {
                lock (bots)
                {
                    foreach (Bot bot in bots)
                    {
                        bot.Run();

                        if (bot.LoggedIn && !OnlineBots.Contains(bot))
                        {
                            OnlineBots.Add(bot);
                        }
                        else if(!bot.LoggedIn)
                        {
                            OnlineBots.Remove(bot);
                        }
                    }
                }
            }
        }
        
        private void StartSocketServer(int port)
        {
            Console.WriteLine("Starting TCP server on port {0}", port);
            socketServer = new TCPInterface(port, Config.TCPPassword);
            threadSocket = new Thread(new ThreadStart(socketServer.StartListening));
            threadSocket.CurrentUICulture = new CultureInfo("en-US");
            threadSocket.Start();
        }

        public void Stop()
        {
            Running = false;

            socketServer.Stop();

            foreach (Bot bot in bots)
                bot.Disconnect();
        }
        
        public bool Command(string command)
        {
            string[] args = command.Split(' ');
            switch (args[0])
            {
                case "quit":
                    ShutdownBots();
                    break;
                case "list":
                    ListBots(args);
                    break;
                case "select":
                    Select(args);
                    break;
                case "rename":
                    Rename(args);
                    break;
                case "createto":
                    CreateTradeOffer(args);
                    break;
                case "help":
                    ShowHelp();
                    break;
                case "linkauthenticator":
                    LinkAuthenticator();
                    break;
                case "unlinkauthenticator":
                    UnlinkAuthenticator();
                    break;
                case "withdrawn":
                    WithDrawn(args);
                    break;
                case "testtcp":
                    testTCP(args);
                    break;
                case "getsteamcode":
                    GenerateCode();
                    break;
                case "debug":
                    SetDebugMode();
                    break;
                case "testapi":
                    TestAPI(args);
                    break;
                case "refreshprices":
                    RefreshPrices();
                    break;
                case "reloadtranslation":
                    ReloadTranslation();
                    break;
                default:
                    Console.WriteLine("Command \""+ command + "\" not found ! Use 'help' !");
                break;
            }

            return true;
        }

        public void ShowHelp()
        {
            Console.WriteLine("quit - Shutdown all the bots, the TCP server and everything else.");
            Console.WriteLine("list - List all the bots and there index.");
            Console.WriteLine("select - select a bot to execute commands on.");
            Console.WriteLine("rename - rename a bot through steam.");
            Console.WriteLine("help - show this text.");
            Console.WriteLine("linkauthenticator - link a mobile authenticator through the bot, required to do trade offers correctly.");
            Console.WriteLine("unlinkauthenticator - unlink a mobile authenticator through the bot.");
            Console.WriteLine("withdrawn - Create a trade offer with all the bot's items to a specific steamID.");
            Console.WriteLine("testtcp - Send a small packet to all TCP clients.");
            Console.WriteLine("getsteamcode - Generate an authenticator code.");
            Console.WriteLine("debug - Toggle debug mode.");        
            Console.WriteLine("testapi - refresh market prices.");
            Console.WriteLine("refreshprices - refresh market prices.");
            Console.WriteLine("reloadtranslation - reload the steamchattext.xml file.");
        }

        private void RefreshPrices()
        {
            SelectedBot.ArkarrSteamMarket.ForceRefresh();
        }

        private void TestAPI(string[] args)
        {
            if(args.Count() < 2)
            {
                Console.WriteLine("Usage : testapi [ITEM NAME]");
                return;
            }

            string itemName = "";
            for (int i = 1; i < args.Length; i++)
            {
                if (args.Length - 1 != i)
                    itemName += args[i] + " ";
                else
                    itemName += args[i];
            }
            
            itemName = HttpUtility.HtmlEncode(itemName);
            Item item = SelectedBot.ArkarrSteamMarket.GetItemByName(itemName);

            if (item != null)
            {
                Console.WriteLine("Item name :" + item.Name);
                Console.WriteLine("Item price :" + item.Value);
                Console.WriteLine("Item app id :" + item.AppID);
            }
            else
            {
                Console.WriteLine("Item with name {0} not found", args[1]);
            }
        }

        private void SetDebugMode()
        {
            Program.DEBUG = !Program.DEBUG;
        }

        private void GenerateCode()
        {
            SelectedBot.GenerateCode();
        }

        private void testTCP(string[] args)
        {
            int count = 0;
            string test = "";

            foreach (string arg in args)
            {
                if(!arg.Equals(args[0]))
                    test += arg + " ";
            }

            foreach(Bot bot in bots)
            {
                foreach (GameServer gs in bot.Manager.Servers)
                {
                    if (gs.Alive == false)
                        continue;

                    bot.Manager.Send(gs.ServerID, -2, NetworkCode.ASteambotCode.Simple, test);
                    count++;
                }
            }

            Console.WriteLine("Sent "+test+" to "+ count + " connected servers ! ");
        }

        public void WithDrawn(string[] args)
        {
            if (args.Count() < 2)
            {
                Console.WriteLine("Usage : withdrawn [STEAM ID]");
                return;
            }

            SelectedBot.WithDrawn(args[1]);
        }

        public void LinkAuthenticator()
        {
            SelectedBot.LinkMobileAuth();
        }

        public void UnlinkAuthenticator()
        {
            SelectedBot.DeactivateAuthenticator();
        }

        public void CreateTradeOffer(string[] args)
        {
            if (args.Count() < 2)
            {
                Console.WriteLine("Usage : createto [STEAM ID]");
                return;
            }

            SelectedBot.CreateTradeOffer(args[1]);
        }

        public void ShutdownBots()
        {
            Console.WriteLine("Shutting down steambots...");
            Stop();
        }

        public void Rename(string[] args)
        {
            if (args.Count() < 2)
            {
                Console.WriteLine("Usage : rename [NEW NAME]");
                return;
            }

            string newname = "";
            for (int i = 1; i < args.Count(); i++)
                newname += args[i] + " ";
            newname.Substring(0, newname.Length - 2);

            Console.WriteLine("Renaming steambot...");

            SelectedBot.ChangeName(newname);
        }

        public void Select(string[] args)
        {
            if(args.Count() < 2)
            {
                Console.WriteLine("Usage : select [STEAMBOT INDEX]");
                return;
            }

            int index = 0;

            if (Int32.TryParse(args[1], out index))
            {
                index--;
                
                if (index < 0 || index > bots.Count)
                {
                    Console.WriteLine("No steambot found with index '" + index + "'");
                    return;
                }

                if (bots.Count - 1 < index || index > bots.Count - 1)
                {
                    Console.WriteLine("Index " + index + " is invalid !");
                }
                else
                {
                    SelectedBot = bots[index];
                    Console.Title = "Akarr's steambot - [" + SelectedBot.Name + "]";

                    Console.WriteLine("[" + SelectedBot.Name + "] selected as current bot. Command will be executed from this steambot.");
                }
            }
            else
            {
                Console.WriteLine(args[1] + " is not a valid number !");
            }
        }

        public void ListBots(string[] args)
        {
            Console.WriteLine("----- Number of bots {0} -----", bots.Count);
            for(int i = 0; i < bots.Count; i++)
                Console.WriteLine("\t[{0}] Name : [{1}] | Logged in : [{2}]", i+1, bots[i].Name, bots[i].LoggedIn);
            Console.WriteLine("----------------------------", bots.Count);
        }

        public void ReloadTranslation()
        {
            Console.WriteLine("Does nothing now.");
        }

        public void SelectFirstBot()
        {
            string[] data = new string[2];
            data[0] = "select";
            data[1] = "1";
            Select(data);
        }

        public bool Auth(LoginInfo loginInfo)
        {
            try
            {
                Bot result = bots.Find(x => x.LoginInfoMatch(loginInfo) == true);

                if (result == null)
                {
                    while (socketServer == null || !socketServer.Running)
                        Thread.Sleep(2000);

                    result = new Bot(this, loginInfo, Config, socketServer);
                    lock (bots) { bots.Add(result); }
                }

                result.Auth();
            }
            catch(FileNotFoundException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("DLL is missing. Re-Download ASteambot from the original repository :");
                Console.WriteLine("https://github.com/Arkarr/ASteambot/releases/latest");
                Console.WriteLine("Also read this :");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("https://www.reddit.com/r/SteamBot/comments/3h3xau/steamtradedll_shows_up_as_a_virus/");
                Console.WriteLine("https://www.reddit.com/r/SteamBot/comments/2ynxv6/steamtradedll_missing/");
                Console.WriteLine("https://forums.alliedmods.net/showpost.php?p=2352454&postcount=8");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("More info below :");
                Console.WriteLine("DLL missing : " + e.FileName);
                Console.WriteLine("ASteambot won't continue.");
                Console.ForegroundColor = ConsoleColor.White;

                return false;
            }

            if (SelectedBot == null)
            {
                //SelectedBot = result;
                Console.Title = "Akarr's steambot - [NO STEAMBOT SELECTED USE 'select']";
            }

            return true;
        }

        public bool Send(int serverID, int moduleID, NetworkCode.ASteambotCode netcode, string data)
        {
            bool sendSuccess = false;

            do
            {
                GameServer gs = GetServerByID(serverID);

                if(gs == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Could NOT send mesage to server; server has no valid ID. Restart ASteambot_Core on the problematic server.");
                    Console.ForegroundColor = ConsoleColor.White;

                    return false;
                }

                Console.WriteLine("Sending to " + gs.Name + "(" + gs.IP + ":" + gs.Port + ")");
                sendSuccess = gs.Send(moduleID, netcode, data);

                if (!sendSuccess)
                {
                    Console.WriteLine("Send data failed. Re-trying.");
                    foreach (Bot bot in OnlineBots)
                    {
                        foreach (GameServer g in bot.Manager.Servers)
                        {
                            if (g.Alive == false || g.SocketConnected() == false)
                                bot.SteamchatHandler.ServerRemoved(gs.ServerID);
                        }
                        //bot.Manager.Servers.RemoveAll(gs => gs.Alive == false);
                    }
                }

            } while (sendSuccess == false);

            /*if(gs == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Could NOT send mesage to server; server has no valid ID. Restart ASteambot_Core on the problematic server.");
                Console.ForegroundColor = ConsoleColor.White;

                return false;
            }

            gs.Send(moduleID, netcode, data);*/

            return true;
        }
        
        public GameServer GetServerByID(int serverID)
        {
            GameServer g = Servers.Find(gs => gs.ServerID == serverID);

            if(g == null || g.SocketConnected() == false)
            {
                g = null;
                foreach (GameServer gs in Servers)
                {
                    if(g == null)
                    {
                        if (gs != g && gs.Alive == true && gs.SocketConnected())
                            return gs;
                    }
                    else
                    {
                        if (gs != g && gs.Alive == true && gs.SteamID == g.SteamID && gs.SocketConnected())
                            return gs;
                    }
                }
            }

            return g;
        }

        public List<GameServer> GetGameServers()
        {
            return Servers;
        }
    }
}
