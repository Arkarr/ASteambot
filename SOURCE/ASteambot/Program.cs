using ASteambot.AutoUpdater;
using ASteambot.Modules;
using ASteambot.Networking;
using ASteambot.Networking.Webinterface;
using ASteambot.Networking.Webinterface.Models;
using ASteambot.Networking.Webinterface.Models.SimpleHttpServer.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
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

        private static string BUILD_VERSION = "V10.3";
        private static string BUILD_NAME = BUILD_VERSION + " - PUBLIC";

        public static bool DEBUG;
        public static HTTPServer httpsrv;

        private static List<Modules.Module> modules;

        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;

            using (var file = File.Exists("./SEND_TO_ARKARR.log") ? File.Open("./SEND_TO_ARKARR.log", FileMode.Append) : File.Open("./SEND_TO_ARKARR.log", FileMode.CreateNew))
            using (var stream = new StreamWriter(file))
                stream.WriteLine("*************************\n" + DateTime.Now.ToString() + " (Version " + BUILD_NAME + ") LINUX : " + (IsLinux() ? "YES" : "NO") + "\n*************************\n" + ex.HResult + " - " +  ex.Source + "\n" + ex.TargetSite + "\n" + ex.InnerException + "\n" + ex.HelpLink + "\n" + ex.Message + "\n" + ex.StackTrace + "\n\n");
            
            PrintErrorMessage("Log file (" + "SEND_TO_ARKARR.log" + ") generated ! Send it to Arkarr !!");
        }

        private static Assembly LoadFromSameFolder(object sender, ResolveEventArgs args)
        {
            string folderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/libraries/";

            string assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
            if (!File.Exists(assemblyPath))
                return null;

            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            
            return assembly;
        }

        private static void LoadModules()
        {
            modules = new List<Modules.Module>();
            
            string folderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/modules/";

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string[] files = Directory.GetFiles(folderPath, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (string file in files)
            {
                Assembly ass = Assembly.LoadFrom(file);
                Modules.Module m = null;
                try
                {
                   m = ModuleLoader.LoadASteambotModule(ass);
                }
                catch(Exception e)
                {
                    PrintErrorMessage("Could not load module "+ file +". Reason :");
                    PrintErrorMessage(e.ToString());
                }

                if (m == null || ass == null) //B.L.
                    PrintErrorMessage(">>> Contact the creator of this module ! <<<");
                else
                    modules.Add(m);
            }
        }

        public static Dictionary<bool, string> ExecuteModuleFonction(string i, object[] args)
        {
            Dictionary<bool, string> results = new Dictionary<bool, string>();
            foreach (Modules.Module m in modules)
            {
                try
                {
                    results.Add((bool)m.RunMethod(i, args), (string)args[args.Length-1]);
                }
                catch (Exception e)
                {
                    PrintErrorMessage(e.ToString());
                }
            }

            return results;
        }

        static void Main(string[] args)
        {
            DEBUG = false;

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += LoadFromSameFolder;
            currentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            Start();            
        }

        private static void Start()
        {
            Console.Title = "Akarr's steambot";

            Console.ForegroundColor = ConsoleColor.White;

            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            config = new Config();
            if (!config.LoadConfig())
            {
                Console.WriteLine("Config file (config.cfg) can't be found or is corrupted ! Bot can't start.");
                Console.ReadKey();
                return;
            }

            PrintWelcomeMessage();

            Updater updater = new Updater(config.DisableAutoUpdate, BUILD_VERSION);

            LoadModules();

            Task.WaitAll(updater.Update());

            httpsrv = new HTTPServer(config.WebinterfacePort);
            httpsrv.Listen();

            if (config.DisplayLocation)
                SendLocation();

            steambotManager = new Manager(config);
            threadManager = new Thread(new ThreadStart(steambotManager.Start));
            threadManager.CurrentUICulture = new CultureInfo("en-US");
            threadManager.Start();

            AttemptLoginBot(config.SteamUsername, config.SteamPassword, config.SteamAPIKey);

            while (steambotManager.OnlineBots.Count < 1)
                Thread.Sleep(TimeSpan.FromSeconds(3));

            steambotManager.SelectFirstBot();

            string command = "";
            while (command != "quit")
            {
                Console.Write("> ");
                command = Console.ReadLine();
                steambotManager.Command(command);
            }

            steambotManager.Stop();
            /*if(httpsrv != null)
                httpsrv.Stop();*/
        }

        private static void AttemptLoginBot(string username, string password, string api)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            string hidenAPI = api.Substring(api.Length - 10) + "**********";
            string data = String.Format("Username : {0}  Password : X  API : {1}", username, hidenAPI);
            Console.WriteLine(data);
            Console.ForegroundColor = ConsoleColor.White;
            logininfo = new LoginInfo(username, password, api);

            if(!steambotManager.Auth(logininfo))
                steambotManager.Stop();
        }

        private static void SendLocation()
        {
            try
            {
                string ip = new WebClient().DownloadString("http://ipinfo.io/ip").Replace("\n", "");
                string country = new WebClient().DownloadString("http://ipinfo.io/" + ip + "/country").Replace("\n", "").ToLower();

                var data = new NameValueCollection();
                data.Add("ip", ip);
                data.Add("c", country);

                Fetch("http://raspberrypimaison.ddns.net/website/public/ASteambot/map/register.php", "POST", data);
            }
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Unable to locate ASteambot. Not publishing location on world map.");
                Console.WriteLine(e);
                Console.WriteLine(">>>>>>>>> You can ignore this.");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static string Fetch(string url, string method, NameValueCollection data = null, bool ajax = true, string referer = "", bool fetchError = false)
        {
            using (HttpWebResponse response = Request(url, method, data, ajax, referer, fetchError))
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    if (responseStream == null)
                        return "";

                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private static HttpWebResponse Request(string url, string method, NameValueCollection data = null, bool ajax = true, string referer = "", bool fetchError = false)
        {

            bool isGetMethod = (method.ToLower() == "get");
            string dataString = (data == null ? null : String.Join("&", Array.ConvertAll(data.AllKeys, key => string.Format("{0}={1}", System.Web.HttpUtility.UrlEncode(key), System.Web.HttpUtility.UrlEncode(data[key])))));

            if (isGetMethod && !string.IsNullOrEmpty(dataString))
            {
                url += (url.Contains("?") ? "&" : "?") + dataString;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Accept = "application/json, text/javascript;q=0.9, */*;q=0.5";
            request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/31.0.1650.57 Safari/537.36";
            request.Referer = string.IsNullOrEmpty(referer) ? "http://steamcommunity.com/trade/1" : referer;
            request.Timeout = 3000;
            request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.Revalidate);
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

            if (ajax)
            {
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.Add("X-Prototype-Version", "1.7");
            }

            if (isGetMethod || string.IsNullOrEmpty(dataString))
            {
                try
                {
                    return request.GetResponse() as HttpWebResponse;
                }
                catch (WebException ex)
                {
                    if (((HttpWebResponse)ex.Response).StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    {
                        var resp = ex.Response as HttpWebResponse;
                        if (resp != null)
                            return resp;
                    }

                    if (fetchError)
                    {
                        var resp = ex.Response as HttpWebResponse;
                        if (resp != null)
                            return resp;
                    }
                    throw;
                }
            }

            byte[] dataBytes = Encoding.UTF8.GetBytes(dataString);
            request.ContentLength = dataBytes.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(dataBytes, 0, dataBytes.Length);
            }

            try
            {
                return request.GetResponse() as HttpWebResponse;
            }
            catch (WebException ex)
            {
                if (fetchError)
                {
                    var resp = ex.Response as HttpWebResponse;
                    if (resp != null)
                        return resp;
                }
                throw;
            }
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
            Console.WriteLine("\tVersion " + BUILD_NAME);
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

        public static void PrintErrorMessage(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
