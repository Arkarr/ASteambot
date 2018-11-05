using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambotUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            string v = "";
            if (args.Length > 0)
                v = args[0];

            Updater upd = new Updater();
            upd.Update(v);
        }
    }
}
