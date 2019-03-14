using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static ASteambot.SteamProfile;

namespace ASteambot.Translation
{
    public static class CountryCode
    {
        public static string GetCountryCode(SteamProfileInfos sp)
        {
            try
            {
                if (sp == null)
                {
                    return "en";
                }
                else
                {
                    string country = sp.Location.Substring(sp.Location.LastIndexOf(" ") + 1);
                    country = RemoveBetween(country.Split(' ')[0], '(', ')').Trim();

                    var regions = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(x => new RegionInfo(x.LCID));
                    RegionInfo englishRegion = regions.FirstOrDefault(region => region.EnglishName.Contains(country));

                    if (englishRegion == null)
                    {
                        Console.WriteLine("Country \"" + country + "\" not found !");
                        return "en";
                    }
                    else
                    {
                        return englishRegion.TwoLetterISORegionName.ToLower();
                    }
                }
            }
            catch(Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error while fetching country code. Assumed 'en'. Details here : ");
                if (sp.Location == null)
                    Console.WriteLine("Location null !");
                else
                    Console.WriteLine("Faulty country >>> " + sp.Location.Substring(sp.Location.LastIndexOf(" ") + 1));
                Console.WriteLine("Send to Arkarr please!");
                Console.ForegroundColor = ConsoleColor.White;

                return "en";
            }
        }

        private static string RemoveBetween(string s, char begin, char end)
        {
            Regex regex = new Regex(string.Format("\\{0}.*?\\{1}", begin, end));
            return regex.Replace(s, string.Empty);
        }
    }
}
