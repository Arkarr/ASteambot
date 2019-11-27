using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASteambot.AutoUpdater
{
    public class Updater
    {
        private bool disableUpdater;
        private string actualVersion;
        private int displayTick;

        public Updater(bool disableUpdater, string version)
        {
            this.disableUpdater = disableUpdater;
            this.actualVersion = version;
            this.displayTick = 0;
        }

        public async Task Update()
        {
            if (disableUpdater)
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
                using (var client = new System.Net.WebClient())
                {
                    client.Headers.Add("Content-Type", "application/json");
                    client.Headers.Add("User-Agent", "Super-Secret-Agent");

                    try
                    {
                        json = JObject.Parse(client.DownloadString("https://api.github.com/repos/arkarr/asteambot/releases/latest"));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Couldn't download the last release. Too many request ? Wait about 15 minutes and try again.");
                        Console.WriteLine(e);
                        Console.Write("-----------------------------------------");
                    }
                }

                if (json != null)
                {

                    string version = json.SelectToken("tag_name").ToString();

                    Console.WriteLine("Installed version " + actualVersion + " Most up-to-date version " + version);

                    if (actualVersion.Equals(version))
                    {
                        Console.WriteLine("Already up to date !");
                    }
                    else if(Environment.OSVersion.Platform != PlatformID.MacOSX && Environment.OSVersion.Platform != PlatformID.Unix)
                    {
                        Console.WriteLine("Update found ! Updating...");
                        if (Directory.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update")))
                            Directory.Delete(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update"), true);

                        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update"));


                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Downloading update...");
                        Console.ForegroundColor = ConsoleColor.White;

                        JToken lastRealse = json.SelectToken("assets[0].browser_download_url");

                        await DownloadUpdate((string)lastRealse);
                    }
                    else
                    {
                        var info = new ProcessStartInfo();
                        info.FileName = "LINUX_auto_start.sh";

                        info.UseShellExecute = true;
                        info.CreateNoWindow = false;

                        var p = Process.Start(info);

                        Environment.Exit(0);
                    }
                }
            }
        }

        private async Task DownloadUpdate(string url)
        {
            using (System.Net.WebClient client = new System.Net.WebClient())
            {

                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(Client_DownloadProgressChanged);
                await client.DownloadFileTaskAsync(new Uri(url), Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update.zip"));

                InstallUpdate();
            }
        }

        private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.CursorLeft = 0;
            int mb = (int)Math.Pow(10, 6);
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            displayTick++;

            if (displayTick == 5)
            {
                Console.Write("Downloaded " + (e.BytesReceived) + " bytes of " + (e.TotalBytesToReceive) + " bytes");
                displayTick = 0;
            }
        }

        private void InstallUpdate()
        {
            var self = Assembly.GetExecutingAssembly().Location;
            string selfFileName = Path.GetFileName(self);
            string directory = Path.GetDirectoryName(self) + "/";

            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                /*Console.WriteLine("Extracting the update...");
                Console.WriteLine(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/update.zip"));

                if(Directory.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)+Path.DirectorySeparatorChar+"update")))
                    Directory.Delete(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar + "update"), true);

                ZipFile.ExtractToDirectory("update.zip", Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar + "update");

                List<String> files = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar + "update", "*.*", SearchOption.AllDirectories).ToList();

                foreach (string file in files)
                {
                    FileInfo mFile = new FileInfo(file);
                    if (mFile.Name.Contains("config.cfg"))
                    {
                        RewriteConfigFile(directory + "config.cfg", mFile.FullName);
                        break;
                    }
                    
                    string updateFile = Directory.GetParent(mFile.FullName).ToString().Replace(Path.DirectorySeparatorChar+"update", "") + Path.DirectorySeparatorChar + mFile.Name;

                    Console.WriteLine(updateFile + " is being replaced by " + file);

                    if (File.Exists(@updateFile))
                        File.Delete(@updateFile);

                    File.Move(@file, @updateFile);
                }

                File.Delete("update.zip");
                Directory.Delete(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar + "update"), true);

                Program.PrintErrorMessage("UPDATE DONE ! RESTART ASteambot.exe !!");

                Thread.Sleep(500);
                Environment.Exit(0);*/
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
                        if (mFile.Name.Contains("config.cfg"))
                        {
                            RewriteConfigFile(directory + "config.cfg", mFile.FullName);
                            break;
                        }
                    }

                    foreach (string file in files)
                    {
                        FileInfo mFile = new FileInfo(file);

                        if (mFile.Name.EndsWith("config.cfg"))
                            continue;

                        string dstFolder = Directory.GetParent(mFile.FullName).ToString().Replace("\\update", "") + "\\" + mFile.Name;
                        batFile.WriteLine("MOVE \"{0}\" \"{1}\"", mFile.FullName, dstFolder);
                    }

                    batFile.WriteLine("DEL \"%~f0\" & START /d \"{0}\" ASteambot.exe", directory);
                    batFile.WriteLine("del / q {0}", directory.Substring(0, directory.Length-1)+ "\\update");
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

        private static void RewriteConfigFile(string oldConfigFile, string newConfigFile)
        {
            List<Option> options = new List<Option>();

            if (!File.Exists(oldConfigFile))
            {
                oldConfigFile = Path.GetDirectoryName(oldConfigFile) + "\\configs\\config.cfg";
            }

            options = LoadOptions(@newConfigFile);

            if (File.Exists(oldConfigFile))
            {
                File.Copy(@oldConfigFile, @oldConfigFile + ".OLD", true);
                List<Option> oldOptions = new List<Option>();
                oldOptions = LoadOptions(@oldConfigFile + ".OLD");

                foreach (Option opt in oldOptions)
                    options.Where(w => w.Name == opt.Name).ToList().ForEach(s => s.Value = opt.Value);
            }

            File.Delete(oldConfigFile);
            using (StreamWriter w = File.AppendText(oldConfigFile))
            {
                foreach (Option opt in options)
                {
                    w.WriteLine(opt.Commentary);
                    w.WriteLine(AskInput(opt.Name, opt.Commentary, opt.Value));
                }
            }
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
                
                if (configLine == null || !configLine.Contains('='))
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
    }
}
