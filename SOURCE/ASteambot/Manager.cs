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
        
        public void Command(string command)
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
                default:
                    Console.WriteLine("Command \"" + command + "\" not found !");
                break;
            }
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

            SelectedBot.ChangeName(newname);
            
            Console.WriteLine("Renaming steambot...");
        }

        public void Select(string[] args)
        {
            if(args.Count() < 2)
            {
                Console.WriteLine("Usage : select [STEAMBOT INDEX]");
                return;
            }

            int index = Int32.Parse(args[1]);

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
                SelectedBot = result;
                Console.Title = "Akarr's steambot - [NO STEAMBOT SELECTED USE 'select']";
            }
        }
    }
}
