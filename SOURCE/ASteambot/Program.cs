using ASteambot.Networking;
using ASteambot.Networking.Webinterface;
using ASteambotUpdater;
using SteamTrade.SteamMarket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ASteambot
{
    class Program
    {
        private static Config config;
        private static Updater updater;
        private static HTTPServer httpsrv;
        private static LoginInfo logininfo;
        private static Manager steambotManager;
        private static Thread threadManager;
        
        private static string BUILD_VERSION = "4.5 - PUBLIC";

        public static bool DEBUG;

        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;

            using (var file = File.Exists("./SEND_TO_ARKARR.log") ? File.Open("./SEND_TO_ARKARR.log", FileMode.Append) : File.Open("./SEND_TO_ARKARR.log", FileMode.CreateNew))
            using (var stream = new StreamWriter(file))
                stream.WriteLine("*************************\n" + DateTime.Now.ToString() + " (Version " + BUILD_VERSION + ") LINUX : " + (IsLinux() ? "YES" : "NO") + "\n*************************\n" + ex.HResult + ex.Source + "\n" + ex.TargetSite + "\n" + ex.InnerException + "\n" + ex.HelpLink + "\n" + ex.Message + "\n" + ex.StackTrace + "\n\n");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Log file (" + "SEND_TO_ARKARR.log" + ") generated ! Send it to Arkarr !!");
            Console.ForegroundColor = ConsoleColor.White;
        }

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            Console.Title = "Akarr's steambot";

            if (args.Count() >= 1)
            {
                if(args[0] == "-update" && Directory.GetCurrentDirectory().ToString().EndsWith("tmp"))
                {
                    string destination = Directory.GetParent(Directory.GetCurrentDirectory()).ToString();
                    foreach (string newPath in Directory.GetFiles(Directory.GetCurrentDirectory(), "*.*", SearchOption.AllDirectories))
                    {
                        string update = destination + "\\" + Path.GetFileName(newPath);
                        File.Copy(newPath, update, true);
                    }
                    string process = Directory.GetParent(Directory.GetCurrentDirectory()) + @"\ASteambot.exe";
                    Console.WriteLine("ASteambot PATCHED ! Restarting...");
                    Console.WriteLine(process);
                    Thread.Sleep(5000);
                    Process newAS = new Process();
                    newAS.StartInfo.WorkingDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).ToString();
                    newAS.StartInfo.FileName = process;
                    newAS.StartInfo.Arguments = "";
                    newAS.Start();
                    Environment.Exit(0);
                }
            }

            updater = new Updater();

            AppDomain currentDomain = default(AppDomain);
            currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Searching for updates...");
            Console.ForegroundColor = ConsoleColor.White;
            
            config = new Config();
            if (!config.LoadConfig())
            {
                Console.WriteLine("Config file (config.cfg) can't be found or is corrupted ! Bot can't start.");
                Console.ReadKey();
                return;
            }

            if(config.DisableAutoUpdate)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Updater disabled. Not fetching for last updates.");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else if (!updater.CheckVersion(Regex.Match(BUILD_VERSION, "([^\\s]+)").Value))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Update found ! Updating...");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Title = "Akarr's steambot - Updating...";
                updater.Update();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Update done ! Restarting...");
                Console.ForegroundColor = ConsoleColor.White;

                string path = Directory.GetCurrentDirectory() + "/ASteambot.exe";
                Process asteambot = new Process();

                asteambot.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

                if (IsLinux())
                {
                    asteambot.StartInfo.FileName = "/usr/bin/mono";
                    asteambot.StartInfo.Arguments = "ASteambot.exe -update";
                }
                else
                {
                    asteambot.StartInfo.FileName = path;
                    asteambot.StartInfo.Arguments = "-update";
                }

                Thread.Sleep(3000);

                asteambot.Start();
                Environment.Exit(0);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Already up to date !");
                Console.ForegroundColor = ConsoleColor.White;
            }
            
            PrintWelcomeMessage();

            //WebInterfaceHelper.AddTrade(new SteamTrade.TradeOffer.TradeOffer(null, new SteamKit2.SteamID()));

            steambotManager = new Manager(config);
            threadManager = new Thread(new ThreadStart(steambotManager.Start));
            threadManager.CurrentUICulture = new CultureInfo("en-US");
            threadManager.Start();

            AttemptLoginBot(config.SteamUsername, config.SteamPassword, config.SteamAPIKey);

            while (steambotManager.OnlineBots.Count < 1)
                Thread.Sleep(TimeSpan.FromSeconds(3));

            steambotManager.SelectFirstBot();

            if (!IsLinux())
            {
                if (File.Exists("website.zip"))
                {
                    Console.WriteLine("Website not extracted ! Doing that now...");
                    if (!Directory.Exists("website"))
                        Directory.CreateDirectory("website");
                    ZipFile.ExtractToDirectory("website.zip", "./website");
                    File.Delete("website.zip");
                    Console.WriteLine("Done !");
                }

                if (Directory.Exists("/website/"))
                {
                    httpsrv = new HTTPServer("/website/", 85);
                    Console.WriteLine("HTTP Server started on port : " + httpsrv.Port + ">>> http://localhost:" + httpsrv.Port + "/index.html");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Website folder not present, can't start web interface. Re-download ASteambot from original github.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("HTTP Server disabled for UNIX users. Wait for a fix :) !");
                Console.ForegroundColor = ConsoleColor.White;
            }
            
            Console.Title = "Akarr's steambot";

            string command = "";
            while (command != "quit")
            {
                Console.Write("> ");
                command = Console.ReadLine();
                steambotManager.Command(command);
            }

            if(httpsrv != null)
                httpsrv.Stop();
        }

        private static void AttemptLoginBot(string username, string password, string api)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            string hidenAPI = api.Substring(api.Length - 10) + "**********";
            string data = String.Format("Username : {0}  Password : X  API : {1}", username, hidenAPI);
            Console.WriteLine(data);
            Console.ForegroundColor = ConsoleColor.White;
            logininfo = new LoginInfo(username, password, api);
            steambotManager.Auth(logininfo);
        }

        private static void PrintWelcomeMessage()
        {
            if (config.DisableWelcomeMessage)
                return;

            for (int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");

            Console.ForegroundColor = ConsoleColor.Green;
            string centerText = "ASteambot - Arkarr's steambot";

            int move = (Console.WindowWidth - centerText.Length) / 2;
            int top = Console.CursorTop;
            if (top < 0) top = 0;
            Console.SetCursorPosition(move, top);
            Console.WriteLine(centerText);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("");
            Console.WriteLine("\tAll informations related to this software can be found here :");
            Console.WriteLine("\thttps://forums.alliedmods.net/showthread.php?t=273091");
            Console.WriteLine("");
            Console.WriteLine("\tVersion " + BUILD_VERSION);
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("\tArkarr's message for you :");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\tI think it's the first stable version of ASteambot !!");
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

        public static bool IsLinux()
        {
            int p = (int)Environment.OSVersion.Platform;
            return (p == 4) || (p == 6) || (p == 128);
        }
    }
}
