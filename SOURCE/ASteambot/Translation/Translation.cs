using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ASteambot.Translation
{
    public class Translation
    {
        public bool Load(string path)
        {
            /*XElement translationsXML = null;
            try
            {
                translationsXML = XElement.Load(path);
                //Console.WriteLine(translationsXML);
            }
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                return false;
            }

            if (!translationsXML.HasElements)
                return false;

            
            */
            return true;
        }
    }
}
