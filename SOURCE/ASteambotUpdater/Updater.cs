using CsQuery;
using Mono.Posix;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASteambotUpdater
{
    public class Updater
    {
        private bool downloadFinished = false;
        public static readonly string ASTEAMBOT_LATEST_BINARIES = "https://github.com/Arkarr/ASteambot/releases/latest";

        public Updater()
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/..");

            if (Directory.Exists(Directory.GetCurrentDirectory() + "/tmp"))
                DeleteDirectory(Directory.GetCurrentDirectory() + "/tmp");
        }

        public bool Update(string currentVersion)
        {
            string lastVersion;
            string actualPath = Directory.GetCurrentDirectory();//Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            if (IsLastVersion(currentVersion, out lastVersion))
            {
                PrintInfoMessage("ASteambot already up to date !");
                return true;
            }
            else
            {
                PrintInfoMessage("New version found - "+ lastVersion + " ! Updating...");
            }
            
            Console.ForegroundColor = ConsoleColor.White;

            for (int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");

            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Cyan;
            for (int i = 0; i < Console.WindowWidth; i++)
                Console.Write("*");
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine();

            Console.WriteLine("1) Processing github's release...");

            try
            {
                string downloadURL = "https://github.com/Arkarr/ASteambot/releases/download/"+ lastVersion + "/ASteambot_"+lastVersion+".zip";

                PrintInfoMessage("Last version found : ");
                PrintInfoMessage(lastVersion);

                PrintInfoMessage("2) Downloading files...");
                
                using (var client = new WebClient())
                {
                    Directory.CreateDirectory("tmp");
                    if (IsLinux())
                    {
                        Console.WriteLine("OS detected: Unix");
                        Thread.Sleep(1000);
                        Mono.Unix.Native.Syscall.chmod(actualPath, Mono.Unix.Native.FilePermissions.ALLPERMS);
                        Mono.Unix.Native.Syscall.chmod(actualPath + "/tmp", Mono.Unix.Native.FilePermissions.ALLPERMS);
                    }
                    else
                    {
                        Console.WriteLine("OS detected: Windows");
                    }

                    Directory.SetCurrentDirectory(actualPath);
                    Directory.CreateDirectory(actualPath + "/tmp");
                    if (File.Exists("config.cfg"))
                    {
                        File.Copy("config.cfg", Directory.GetCurrentDirectory() + "/tmp/config.cfg.tmp");
                    }

                    downloadFinished = false;

                    client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
                    client.DownloadFileCompleted += new AsyncCompletedEventHandler(client_DownloadFileCompleted);
                    client.DownloadFileAsync(new Uri(downloadURL), "ASteambot_" + lastVersion + ".zip");
                    
                    while(!downloadFinished)
                    {
                    }

                    ZipFile.ExtractToDirectory("ASteambot_" + lastVersion + ".zip", "tmp");
                    File.Delete("ASteambot_" + lastVersion + ".zip");

                    Directory.SetCurrentDirectory(actualPath + "/tmp");

                    rewriteConfigFile("config.cfg");

                    Directory.SetCurrentDirectory(actualPath);
                    
                    foreach (string dirPath in Directory.GetDirectories("tmp", "*", SearchOption.AllDirectories))
                        Directory.CreateDirectory(dirPath.Replace("tmp", ""));
                    
                    foreach (string newPath in Directory.GetFiles("tmp", "*.*", SearchOption.AllDirectories))
                        File.Copy(newPath, newPath.Replace("tmp", "./"), true);

                    DeleteDirectory("tmp");
                }
            }
            catch (Exception e)
            {
                PrintErrorMessage(e.Message);

                return false;
            }
            finally
            {
                PrintSucessMessage();
            }

            return true;
        }

        private void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            /*if (lastBytes == 0)
            {
                lastUpdate = DateTime.Now;
                lastBytes = e.BytesReceived;
                return;
            }

            DateTime now = DateTime.Now;
            TimeSpan timeSpan = now - lastUpdate;
            double bytesChange = e.BytesReceived - lastBytes;
            double bytesPerSecond = bytesChange / timeSpan.Seconds;

            lastBytes = e.BytesReceived;
            lastUpdate = now;*/

            if (Console.CursorLeft != 0)
                return;

            int mb = (int)Math.Pow(10, 6);
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            Console.Write("Downloaded " + (e.BytesReceived / mb) + "mb of " + (e.TotalBytesToReceive / mb) + " mb");// @" + bytesPerSecond + "byte/s");
            Console.CursorLeft = 0;
        }
        
        private void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            downloadFinished = true;
            Console.WriteLine();
        }

        private FileStream WaitForFile(string fullPath, System.IO.FileMode mode, FileAccess access, FileShare share)
        {
            for (int numTries = 0; numTries < 10; numTries++)
            {
                FileStream fs = null;
                try
                {
                    fs = new FileStream(fullPath, mode, access, share);
                    return fs;
                }
                catch (IOException)
                {
                    if (fs != null)
                    {
                        fs.Dispose();
                    }
                    Thread.Sleep(50);
                }
            }

            return null;
        }

        public bool IsLastVersion(string currentVersions, out string lastVersion)
        {
            try
            {
                CQ element = null;
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                WebRequest req = HttpWebRequest.Create(ASTEAMBOT_LATEST_BINARIES);
                req.Method = "GET";

                string source;
                using (StreamReader reader = new StreamReader(req.GetResponse().GetResponseStream()))
                    source = reader.ReadToEnd();

                element = CQ.Create(source);

                lastVersion = element.Select(".octicon-tag").Parent().Attr("title");
                
                Console.WriteLine("Current version : " + currentVersions + "\tLast version : " + lastVersion);

                Console.WriteLine("'V " + currentVersions+"'-'"+ lastVersion + "'");
                return ("V " + currentVersions).Equals(lastVersion);
            }
            catch (Exception e)
            {
                //TODO : Handle that correctly.
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error while fetching updates : ");
                Console.WriteLine(e);
                Console.ForegroundColor = ConsoleColor.White;

                lastVersion = "";

                return true;
            }
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
                File.Copy(path + ".tmp", path + ".OLD", true);
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

        private static void PrintInfoMessage(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void PrintWarningMessage(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNING : " + msg);
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static bool IsLinux()
        {
            int p = (int)Environment.OSVersion.Platform;
            return (p == 4) || (p == 6) || (p == 128);
        }
    }
}
