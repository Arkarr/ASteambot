using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;

namespace ASteambot
{
    public class Manager
    {
        public Bot SelectedBot { get; private set; }
        public List<Bot> OnlineBots { get; private set; }

        private bool Running;
        private List<Bot> bots;

        public Manager()
        {
            bots = new List<Bot>();
            OnlineBots = new List<Bot>();
        }

        public void Start()
        {
            Running = true;
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

        public void Stop()
        {
            Running = false;
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
                default:
                    Console.WriteLine("Command \"{0}\" not found ! Use 'help' !", command);
                break;
            }

            return true;
        }

        public void LinkAuthenticator()
        {
            SelectedBot.LinkMobileAuth();
        }

        public void UnlinkAuthenticator()
        {
            SelectedBot.DeactivateAuthenticator();
        }

        public void ShowHelp()
        {
            Console.WriteLine("quit - Shutdown all the bots, the TCP server and everything else.");
            Console.WriteLine("list - List all the bots and there index.");
            Console.WriteLine("select - select a bot to execute commands on.");
            Console.WriteLine("rename - rename a bot through steam.");
            Console.WriteLine("createto - create a thread offer.");
            Console.WriteLine("help - show this text.");
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

            int index = Int32.Parse(args[1])-1;

            if(index < 0 || index > bots.Count)
            {
                Console.WriteLine("No steambot found with index '{0}'", index);
                return;
            }

            if (bots.Count-1 < index || index > bots.Count - 1)
            {
                Console.WriteLine("Index {0} is invalid !", index);
            }
            else
            {
                SelectedBot = bots[index];
                Console.Title = "Akarr's steambot - [" + SelectedBot.Name + "]";

                Console.WriteLine("[{0}] selected as current bot. Command will be executed from this steambot.", SelectedBot.Name);
            }
        }

        public void ListBots(string[] args)
        {
            Console.WriteLine("----- Number of bots {0} -----", bots.Count);
            for(int i = 0; i < bots.Count; i++)
                Console.WriteLine("\t[{0}] Name : [{1}] | Logged in : [{2}]", i+1, bots[i].Name, bots[i].LoggedIn);
            Console.WriteLine("----------------------------", bots.Count);
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
                result = new Bot(loginInfo);
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
