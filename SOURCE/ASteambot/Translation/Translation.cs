using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace ASteambot.Translation
{
    public class Translation
    {
        private Dictionary<string, Dictionary<string, string>> translations = new Dictionary<string, Dictionary<string, string>>();

        public bool Load(string path)
        {
            XElement translationsXML = null;
            try
            {
                translationsXML = XElement.Load(path);
                //Console.WriteLine(translationsXML);
            }
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Translation file ("+ path + ") can't be found or is corrupted ! Bot won't respond to user message.");
                Console.WriteLine("More infos here :");
                Console.WriteLine(e.Message);
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }

            if (!translationsXML.HasElements)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Translation file (" + path + ") is corrupted ! Bot won't respond to user message. Presse a key to continue...");
                Console.ReadKey();
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }

            XmlReader xmlReader = translationsXML.CreateReader();

            string msg = "";
            Dictionary<string, string> languages = new Dictionary<string, string>();

            while (!xmlReader.EOF)
            {
                if (xmlReader.NodeType == XmlNodeType.Element)
                {
                    if (xmlReader.Name.Equals("handler"))
                    {
                        if (msg.Length == 0)
                        {
                            msg = xmlReader.GetAttribute(0);
                            xmlReader.Read();
                        }
                        else
                        {
                            translations.Add(msg, languages);
                            languages = new Dictionary<string, string>();
                            msg = "";
                        }
                    }
                    else if (xmlReader.Name.Equals("translation"))
                    {
                        if (languages.ContainsKey(xmlReader.GetAttribute(0)))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Translation for " + msg + " exist twice in " + xmlReader.GetAttribute(0) + " ! Skipping...");
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        else
                        {
                            languages.Add(xmlReader.GetAttribute(0), xmlReader.ReadInnerXml());
                        }
                    }
                    else
                    {
                        xmlReader.Read();
                    }
                }
                else
                {
                    xmlReader.Read();
                }
            }

            translations.Add(msg, languages);

            return true;
        }

        public string GetSentence(string sentence, string language)
        {
            Dictionary<string, string> s = null;
            //Shitty
            foreach (KeyValuePair<string, Dictionary<string, string>> entry in translations)
            {
                if(entry.Key.Split(';').Contains(sentence))
                {
                    s = entry.Value;
                    break;
                }
            }

            if (s == null)
                return "Sorry, I don't understand your request.";

            if (s.ContainsKey(language))
                return s[language];
            else if (s.ContainsKey("en"))
                return s["en"];
            else
                return "Sorry, I don't understand your request.";
        }
    }
}
