using CsQuery;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASteambotUpdater
{
    public class Updater
    {
        public static string ASTEAMBOT_BINARIES = "https://github.com/Arkarr/ASteambot/tree/master/BINARIES";
        public static string ASTEAMBOT_BINARIES_RAW = "https://raw.githubusercontent.com/Arkarr/ASteambot/master/BINARIES";

        public Updater()
        {
            if (Directory.Exists(Directory.GetCurrentDirectory() + "/tmp"))
                DeleteDirectory(Directory.GetCurrentDirectory() + "/tmp");
        }

        public void Update()
        {
            string actualPath = AppDomain.CurrentDomain.BaseDirectory;
            
            Console.ForegroundColor = ConsoleColor.White;

            for (int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");

            Console.WriteLine();

            Console.WriteLine(" URL set to : ");
            Console.WriteLine(" " + ASTEAMBOT_BINARIES);

            for (int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");

            Console.WriteLine();

            Console.WriteLine("1) Variables init...");

            string lastVersion = null;
            string folder_url = null;

            CQ element = null;
            CQ versions_folders = null;
            List<IDomObject> files = null;

            Console.WriteLine("2) Fetching github...");
            try
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                WebRequest req = HttpWebRequest.Create(ASTEAMBOT_BINARIES);
                req.Method = "GET";

                string source;
                using (StreamReader reader = new StreamReader(req.GetResponse().GetResponseStream()))
                    source = reader.ReadToEnd();

                element = CQ.Create(source);
            }
            catch (Exception e)
            {
                PrintErrorMessage(e.Message);
            }
            finally
            {
                PrintSucessMessage();
            }

            Console.WriteLine("3) Processing github's folder...");

            try
            {
                versions_folders = element.Select(".js-navigation-item");
                lastVersion = versions_folders.Last().Children().ToList().ElementAt(1).ChildNodes[1].LastElementChild.InnerText;

                Console.WriteLine("Last version found : ");
                Console.WriteLine(lastVersion);

                folder_url = ASTEAMBOT_BINARIES + "/" + lastVersion + "/";

                Console.WriteLine("URL set to : ");
                Console.WriteLine(folder_url);

                files = CQ.CreateFromUrl(folder_url)["tr.js-navigation-item > td"].ToList();
            }
            catch (Exception e)
            {
                PrintErrorMessage(e.Message);
            }
            finally
            {
                PrintSucessMessage();
            }

            Console.WriteLine("4) Downloading files...");

            string configPath = "";
            using (var client = new WebClient())
            {
                string updateDir = "tmp";
                Console.WriteLine("Creating directory \"ASteambot\" ...");
                Directory.CreateDirectory(actualPath + "/tmp");
                if(File.Exists("config.cfg"))
                    File.Copy("config.cfg", Directory.GetCurrentDirectory()+"/"+ updateDir + "/config.cfg");
                Directory.SetCurrentDirectory(updateDir);
                PrintSucessMessage();

                foreach (IDomObject el in files)
                {
                    if (!el.HasClass("content"))
                        continue;

                    string fileName = el.ChildNodes[1].LastElementChild.InnerText;
                    string file_url = ASTEAMBOT_BINARIES_RAW + "/" + lastVersion + "/" + fileName;

                    Console.WriteLine("Downloading " + fileName + "...");

                    if (fileName.Equals("config.cfg"))
                    {
                        configPath = file_url;
                    }
                    else if (fileName.Equals("website.zip"))
                    {
                        Console.WriteLine("Downloading website...");
                        client.DownloadFile(file_url, actualPath + "/"+ updateDir + "/"+fileName);
                        /*if (Directory.Exists(actualPath + "/"+ updateDir + "/website"))
                        {
                            Console.WriteLine("Removing old website...");
                            DeleteDirectory(actualPath + "/"+ updateDir + "/website");
                        }
                        Console.WriteLine("Extracting website...");
                        if (!Directory.Exists(actualPath + "/" + updateDir + "/" + "website"))
                            Directory.CreateDirectory(actualPath + "/" + updateDir + "/" + "website");

                        ZipFile.ExtractToDirectory(actualPath + "/"+ updateDir + "/" + fileName, actualPath + "/"+ updateDir + "/" + "website");
                        Console.WriteLine("Removing archive...");
                        File.Delete(actualPath + "/"+ updateDir + "/" + fileName);
                        Console.WriteLine("Done !");*/
                    }
                    else
                    {
                        client.DownloadFile(file_url, actualPath + "/"+ updateDir + "/" + fileName);
                    }
                }
                client.DownloadFile(configPath, actualPath + "/"+ updateDir + "/" + "config.cfg" + ".tmp");
                rewriteConfigFile(actualPath + updateDir + "/" + "config.cfg");

                PrintSucessMessage();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            int counter = 5;
            while (counter > 0)
            {
                Console.WriteLine("All done sucessfully ! Starting ASteambot in " + counter + " secondes...");
                counter--;
                System.Threading.Thread.Sleep(1000);
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);
            }
            Console.WriteLine();
            /*string exePath = actualPath + "/"+ updateDir + "/ASteambot.exe";
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath
                }
            };
            //Console.WriteLine(process.StartInfo.FileName);
            process.Start();
            process.WaitForExit();*/
        }

        public bool CheckVersion(string currentVersions)
        {
            CQ element = null;
            CQ versions_folders = null;
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            WebRequest req = HttpWebRequest.Create(ASTEAMBOT_BINARIES);
            req.Method = "GET";

            string source;
            using (StreamReader reader = new StreamReader(req.GetResponse().GetResponseStream()))
                source = reader.ReadToEnd();

            element = CQ.Create(source);

            versions_folders = element.Select(".js-navigation-item");
            string lastVersion = versions_folders.Last().Children().ToList().ElementAt(1).ChildNodes[1].LastElementChild.InnerText;

            Console.WriteLine("Current version : " + currentVersions + "\tLast version : " + lastVersion);

            return ("V "+currentVersions).Equals(lastVersion);
        }

        private static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        private static List<Option> LoadOptions(string filePath)
        {
            string line;
            int lineIndex = 0;
            string commentary = "";
            string configLine = "";

            List<Option> options = new List<Option>();

            StreamReader file = new StreamReader(@filePath);
            while ((line = file.ReadLine()) != null)
            {
                lineIndex++;
                if (line.StartsWith("//"))
                {
                    commentary = line;
                    configLine = file.ReadLine();
                    lineIndex++;
                }
                else
                {
                    continue;
                }

                if (!configLine.Contains('='))
                    continue;

                string[] cl = configLine.Split('=');
                if (cl.Length > 1)
                    options.Add(new Option(cl[0] + "=", commentary, String.Join("=", cl.Skip(1).ToArray())));
                else
                    options.Add(new Option(configLine, commentary));

            }
            file.Close();

            return options;
        }

        private static void rewriteConfigFile(string path)
        {
            List<Option> options = new List<Option>();

            options = LoadOptions(@path + ".tmp");

            if (File.Exists(path))
            {
                File.Copy(path, path + ".OLD", true);
                List<Option> oldOptions = new List<Option>();
                oldOptions = LoadOptions(@path + ".OLD");

                foreach (Option opt in oldOptions)
                    options.Where(w => w.Name == opt.Name).ToList().ForEach(s => s.Value = opt.Value);
            }

            File.Delete("config.cfg");
            using (StreamWriter w = File.AppendText("config.cfg"))
            {
                foreach (Option opt in options)
                {
                    w.WriteLine(opt.Commentary);
                    w.WriteLine(AskInput(opt.Name, opt.Commentary, opt.Value));
                }
            }

            File.Delete(@path + ".tmp");
        }

        private static string AskInput(string configLine, string commentary, string defaultValue = "")
        {
            string[] count = commentary.Split(new string[] { "/n" }, StringSplitOptions.None);

            Console.ForegroundColor = ConsoleColor.Cyan;
            for (int i = 0; i < count.Length; i++)
            {
                if (i > 0)
                    Console.Write("//");

                Console.WriteLine(count[i]);
            }
            if (defaultValue.Length > 0)
                Console.WriteLine("//Previous (default) value : " + defaultValue + " (leave empty to keep previous value)");
            Console.ResetColor();
            Console.Write(configLine);

            string input = Console.ReadLine();
            if (input.Length == 0)
                return configLine + defaultValue;
            else
                return configLine + input;
        }

        private static void PrintErrorMessage(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR : ");
            Console.WriteLine(msg);
            int counter = 5;
            while (counter > 0)
            {
                Console.WriteLine("Updater closing in " + counter + " secondes...");
                counter--;
                Thread.Sleep(1000);
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 1);
            }
            Environment.Exit(0);
        }

        private static void PrintSucessMessage()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Success !");
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void PrintWarningMessage(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNING : " + msg);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static bool MyRemoteCertificateValidationCallback(System.Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOk = true;
            // If there are errors in the certificate chain,
            // look at each error to determine the cause.
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                for (int i = 0; i < chain.ChainStatus.Length; i++)
                {
                    if (chain.ChainStatus[i].Status == X509ChainStatusFlags.RevocationStatusUnknown)
                    {
                        continue;
                    }
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                    bool chainIsValid = chain.Build((X509Certificate2)certificate);
                    if (!chainIsValid)
                    {
                        isOk = false;
                        break;
                    }
                }
            }
            return isOk;
        }
    }
}
