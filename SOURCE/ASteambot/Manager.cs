using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using ASteambot.Networking;
using System.Threading;
using ArkarrUtilitys;

namespace ASteambot
{
    public class Manager
    {
        public Bot SelectedBot { get; private set; }
        public List<Bot> OnlineBots { get; private set; }
        public List<GameServer> Servers { get; private set; }
        public Config Config { get; private set; }

        private bool Running;
        private List<Bot> bots;
        private AsynchronousSocketListener socketServer;
        private Thread threadSocket;

        public Manager(Config Config)
        {
            this.Config = Config;
            bots = new List<Bot>();
            OnlineBots = new List<Bot>();
            Servers = new List<GameServer>();
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
                        if (bot.LoggedIn)
                            OnlineBots.Add(bot);
                        else
                            OnlineBots.Remove(bot);
                    }
                }
            }
        }
        
        private void StartSocketServer(int port)
        {
            Console.WriteLine("Starting TCP server on port {0}", port);
            socketServer = new AsynchronousSocketListener(port, Config.TCPPassword);
            threadSocket = new Thread(new ThreadStart(socketServer.StartListening));
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
                default:
                    Console.WriteLine("Command \""+ command + "\" not found ! Use 'help' !");
                break;
            }

            return true;
        }

        public void ShowHelp()
        {
            SmartConsole.WriteLine("quit - Shutdown all the bots, the TCP server and everything else.");
            SmartConsole.WriteLine("list - List all the bots and there index.");
            SmartConsole.WriteLine("select - select a bot to execute commands on.");
            SmartConsole.WriteLine("rename - rename a bot through steam.");
            //SmartConsole.WriteLine("createto - create a thread offer.");
            SmartConsole.WriteLine("help - show this text.");
            SmartConsole.WriteLine("linkauthenticator - link a mobile authenticator through the bot, required to do trade offers correctly.");
            SmartConsole.WriteLine("unlinkauthenticator - unlink a mobile authenticator through the bot.");
            SmartConsole.WriteLine("withdrawn - Create a trade offer with all the bot's items to a specific steamID.");
            SmartConsole.WriteLine("testtcp - Send a small packet to all TCP clients.");
            SmartConsole.WriteLine("getsteamcode - Generate an authenticator code.");
            SmartConsole.WriteLine("debug - Toggle debug mode.");
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
            string test = (int)NetworkCode.ASteambotCode.Simple+"|";
            foreach (string arg in args)
                test += arg + " ";

            foreach(Bot bot in bots)
            {
                foreach (GameServer gs in bot.botManager.Servers)
                {
                    gs.Send(test);
                    count++;
                }
            }

            SmartConsole.WriteLine("Sent "+test+" to "+ count + " connected servers ! ");
        }

        public void WithDrawn(string[] args)
        {
            if (args.Count() < 2)
            {
                SmartConsole.WriteLine("Usage : withdrawn [STEAM ID]");
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
                SmartConsole.WriteLine("Usage : createto [STEAM ID]");
                return;
            }

            SelectedBot.CreateTradeOffer(args[1]);
        }

        public void ShutdownBots()
        {
            SmartConsole.WriteLine("Shutting down steambots...");
            Stop();
        }

        public void Rename(string[] args)
        {
            if (args.Count() < 2)
            {
                SmartConsole.WriteLine("Usage : rename [NEW NAME]");
                return;
            }

            string newname = "";
            for (int i = 1; i < args.Count(); i++)
                newname += args[i] + " ";
            newname.Substring(0, newname.Length - 2);

            SmartConsole.WriteLine("Renaming steambot...");

            SelectedBot.ChangeName(newname);
        }

        public void Select(string[] args)
        {
            if(args.Count() < 2)
            {
                SmartConsole.WriteLine("Usage : select [STEAMBOT INDEX]");
                return;
            }

            int index = Int32.Parse(args[1])-1;

            if(index < 0 || index > bots.Count)
            {
                SmartConsole.WriteLine("No steambot found with index '"+ index + "'");
                return;
            }

            if (bots.Count-1 < index || index > bots.Count - 1)
            {
                SmartConsole.WriteLine("Index "+ index + " is invalid !");
            }
            else
            {
                SelectedBot = bots[index];
                Console.Title = "Akarr's steambot - [" + SelectedBot.Name + "]";

                SmartConsole.WriteLine("["+ SelectedBot.Name + "] selected as current bot. Command will be executed from this steambot.");
            }
        }

        public void ListBots(string[] args)
        {
            SmartConsole.WriteLine("----- Number of bots {0} -----", bots.Count);
            for(int i = 0; i < bots.Count; i++)
                SmartConsole.WriteLine("\t[{0}] Name : [{1}] | Logged in : [{2}]", i+1, bots[i].Name, bots[i].LoggedIn);
            SmartConsole.WriteLine("----------------------------", bots.Count);
        }
        

        public void SelectFirstBot()
        {
            string[] data = new string[2];
            data[0] = "select";
            data[1] = "1";
            Select(data);
        }

        public void Auth(LoginInfo loginInfo)
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

            if (SelectedBot == null)
            {
                //SelectedBot = result;
                Console.Title = "Akarr's steambot - [NO STEAMBOT SELECTED USE 'select']";
            }
        }
    }
}
