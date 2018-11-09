using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ASteambotUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            Updater upd = new Updater();

            string v = "";
            string lsv = "";

            if (args.Length > 0)
                v = args[0];
            
            if (upd.IsLastVersion(v, out lsv))
            {
                Console.WriteLine("OK");
            }
            else
            {
                upd.Update(v);
                Process p = new Process();
                Console.WriteLine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\..\\ASteambot.exe");
                p.StartInfo.FileName = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\..\\ASteambot.exe";
                p.StartInfo.Arguments = "";
                p.StartInfo.CreateNoWindow = false;
                p.Start();

                Environment.Exit(0);
            }
        }
    }
}
