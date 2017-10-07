using ASteambot.Networking;
using SteamTrade.SteamMarket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASteambot
{
    class Program
    {
        private static Config config;
        private static LoginInfo logininfo;
        private static Manager steambotManager;
        private static Thread threadManager;

        private static string BUILD_VERSION = "2.4.0 - PUBLIC";

        public static bool DEBUG;

        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = default(Exception);
            ex = (Exception)e.ExceptionObject;

            using (var file = File.Exists("./SEND_TO_ARKARR.log") ? File.Open("./SEND_TO_ARKARR.log", FileMode.Append) : File.Open("./SEND_TO_ARKARR.log", FileMode.CreateNew))
            using (var stream = new StreamWriter(file))
                stream.WriteLine("*************************\n" + DateTime.Now.ToString() + " (Version " + BUILD_VERSION + ")" + "\n*************************\n" + ex.Message + "\n" + ex.StackTrace + "\n\n");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Log file (" + "SEND_TO_ARKARR.log" + ") generated ! Send it to Arkarr !!");
            Console.ForegroundColor = ConsoleColor.White;
        }

        static void Main(string[] args)
        {
            AppDomain currentDomain = default(AppDomain);
            currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            Console.Title = "Akarr's steambot";

            PrintWelcomeMessage();

            config = new Config();
            if(!config.LoadConfig())
            {
                Console.WriteLine("Config file (config.cfg) can't be found or is corrupted ! Bot can't start.");
                Console.ReadKey();
                return;
            }

            steambotManager = new Manager(config);
            threadManager = new Thread(new ThreadStart(steambotManager.Start));
            threadManager.Start();

            AttemptLoginBot(config.SteamUsername, config.SteamPassword, config.SteamAPIKey);

            while (steambotManager.OnlineBots.Count < 1)
                Thread.Sleep(TimeSpan.FromSeconds(3));

            steambotManager.SelectFirstBot();

            string command = "";
            while(command != "quit")
            {
                Console.Write("> ");
                command = Console.ReadLine();
                steambotManager.Command(command);
            }
        }

        private static void AttemptLoginBot(string username, string password, string api)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            string data = String.Format("Username : {0}  Password : X  API : {1}", username, api.Substring(api.Length - 10) + "**********");
            Console.WriteLine(data);
            Console.ForegroundColor = ConsoleColor.White;
            logininfo = new LoginInfo(username, password, api);
            steambotManager.Auth(logininfo);
        }

        private static void PrintWelcomeMessage()
        {
            for(int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");

            Console.ForegroundColor = ConsoleColor.Green;
            string centerText = "ASteambot - Arkarr's steambot";
            Console.SetCursorPosition((Console.WindowWidth - centerText.Length) / 2, Console.CursorTop);
            Console.WriteLine(centerText);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("");
            Console.WriteLine("\tAll informations related to this software can be found here :");
            Console.WriteLine("\thttps://forums.alliedmods.net/showthread.php?t=273091");
            Console.WriteLine("");
            Console.WriteLine("\tVersion "+ BUILD_VERSION);
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("\tArkarr's message for you :");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\tI... I think it's the first working, stable, version of ASteambot");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(" <3 ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("! Thanks so much for debuging it with me !");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("");
            Console.WriteLine("");
            Console.Write("\tI would like you not to remove this text ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("<3 ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("!");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("\tArkarr's Steam profile : http://steamcommunity.com/id/arkarr");

            for (int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");
        }
    }
}
