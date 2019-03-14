using ASteambot.Networking;
using ASteambot.Networking.Webinterface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace ASteambot
{
    class Program
    {
        private static Config config;
        private static HTTPServer httpsrv;
        private static LoginInfo logininfo;
        private static Manager steambotManager;
        private static Thread threadManager;

        private static string BUILD_VERSION = "V9.0";
        private static string BUILD_NAME = BUILD_VERSION + " - PUBLIC";

        public static bool DEBUG;

        private static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;

            using (var file = File.Exists("./SEND_TO_ARKARR.log") ? File.Open("./SEND_TO_ARKARR.log", FileMode.Append) : File.Open("./SEND_TO_ARKARR.log", FileMode.CreateNew))
            using (var stream = new StreamWriter(file))
                stream.WriteLine("*************************\n" + DateTime.Now.ToString() + " (Version " + BUILD_NAME + ") LINUX : " + (IsLinux() ? "YES" : "NO") + "\n*************************\n" + ex.HResult + ex.Source + "\n" + ex.TargetSite + "\n" + ex.InnerException + "\n" + ex.HelpLink + "\n" + ex.Message + "\n" + ex.StackTrace + "\n\n");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Log file (" + "SEND_TO_ARKARR.log" + ") generated ! Send it to Arkarr !!");
            Console.ForegroundColor = ConsoleColor.White;
        }

        static void Main(string[] args)
        {
            Console.Title = "Akarr's steambot";

            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            AppDomain currentDomain = default(AppDomain);
            currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            Console.Title = "Akarr's steambot";
            
            config = new Config();
            if (!config.LoadConfig())
            {
                Console.WriteLine("Config file (config.cfg) can't be found or is corrupted ! Bot can't start.");
                Console.ReadKey();
                return;
            }

            PrintWelcomeMessage();

            if(config.DisplayLocation)
                SendLocation();

            if (config.DisableAutoUpdate)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Updater disabled. Not fetching for last updates.");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Searching for updates...");
                Console.ForegroundColor = ConsoleColor.White;

                JObject json = null;
                using (var client = new WebClient())
                {
                    client.Headers.Add("Content-Type", "application/json");
                    client.Headers.Add("User-Agent", "Super-Secret-Agent");

                    try
                    {
                        json = JObject.Parse(client.DownloadString("https://api.github.com/repos/arkarr/asteambot/releases/latest"));
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Couldn't download the last release. Too many request ? Wait about 15 minutes and try again.");
                    }
                }
                
                string version = json.SelectToken("tag_name").ToString();

                Console.WriteLine("Installed version " + BUILD_VERSION + " Most up-to-date version " + version);

                if (BUILD_VERSION.Equals(version))
                {
                    Console.WriteLine("Already up to date !");
                }
                else
                {
                    Console.WriteLine("Update found ! Updating...");
                    if (Directory.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update")))
                        Directory.Delete(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update"), true);

                    Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update"));

                    
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Downloading update...");
                    Console.ForegroundColor = ConsoleColor.White;
                        
                    using (WebClient client = new WebClient())
                    {
                        JToken lastRealse = json.SelectToken("assets[0].browser_download_url");
                        client.DownloadFile((string)lastRealse, Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update.zip"));
                    }

                    var self = Assembly.GetExecutingAssembly().Location;
                    string selfFileName = Path.GetFileName(self);
                    string directory = Path.GetDirectoryName(self) + "/";

                    if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                    {
                        Console.WriteLine("Extracting the update...");
                        Console.WriteLine(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update.zip"));

                        ZipFile.ExtractToDirectory("update.zip", "./update");
                        
                        List<String> files = Directory.GetFiles(directory + "update", "*.*", SearchOption.AllDirectories).ToList();

                        foreach (string file in files)
                        {
                            if (file.EndsWith("config.cfg"))
                                continue;

                            string updateFile = directory + Path.GetFileName(file);
                            Console.WriteLine(file);
                            if (File.Exists(@updateFile))
                            {
                                File.Delete(@updateFile);
                            }
                            File.Move(@file, @updateFile);
                        }

                        File.Delete("update.zip");
                        Directory.Delete("./update", true);

                        Console.WriteLine("Press a key to restart...");
                        Console.ReadKey();

                        Process p = new Process();
                        p.StartInfo.FileName = self;
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardInput = true;
                        p.Start();
                                
                        Thread.Sleep(500);
                        Environment.Exit(0);
                    }
                    else
                    {
                        Console.WriteLine("Extracting the update...");
                        Console.WriteLine(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update.zip"));

                        ZipFile.ExtractToDirectory("update.zip", "./update");

                        using (var batFile = new StreamWriter(File.Create(directory + "Update.bat")))
                        {
                            batFile.WriteLine("@ECHO OFF");
                            batFile.WriteLine("TASKKILL /IM \"{0}\" > NUL", selfFileName);
                            batFile.WriteLine("TIMEOUT /t 5 /nobreak > NUL");

                            List<String> files = Directory.GetFiles("./update", "*.*", SearchOption.AllDirectories).ToList();

                            foreach (string file in files)
                            {
                                FileInfo mFile = new FileInfo(file);
                                if(!mFile.Name.Contains("config.cfg"))
                                    batFile.WriteLine("MOVE \"{0}\" \"{1}\"", @directory+"update\\" +mFile.Name, @directory+mFile.Name);
                            }


                            batFile.WriteLine("DEL \"%~f0\" & START /d \"{0}\" ASteambot.exe", directory);
                        }

                        ProcessStartInfo startInfo = new ProcessStartInfo(directory + "Update.bat");
                        // Hide the terminal window
                        startInfo.CreateNoWindow = true;
                        startInfo.UseShellExecute = false;
                        startInfo.WorkingDirectory = Path.GetDirectoryName(self);
                        Process.Start(startInfo);
                            
                        File.Delete("update.zip");

                        Environment.Exit(0);
                    }
                }
            }

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
                string path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                if (File.Exists("website.zip"))
                {
                    Console.WriteLine("Website not extracted ! Doing that now...");
                    //ZipFile.ExtractToDirectory("website.zip", path);

                    File.Delete("website.zip");
                    Console.WriteLine("Done !");
                }
                
                Console.WriteLine("I gave up for now on the webinterface. Worst idea ever.");
                /*if (Directory.Exists(path + "/website"))
                {
                    //Webinteface are shit anyway... Worst idea ever!
                    //httpsrv = new HTTPServer("/website", 85);
                    //Console.WriteLine("HTTP Server started on port : " + httpsrv.Port + ">>> http://localhost:" + httpsrv.Port + "/index.html");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Website folder not present, can't start web interface. Re-download ASteambot from original github.");
                    Console.ForegroundColor = ConsoleColor.White;
                }*/
            }
            else
            {
                /*Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("HTTP Server disabled for UNIX users. Wait for a fix :) !");
                Console.ForegroundColor = ConsoleColor.White;*/
            }

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
            string dataString = (data == null ? null : String.Join("&", Array.ConvertAll(data.AllKeys, key => string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(data[key])))));

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
                    if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.InternalServerError)
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
    }
}
