using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ASteambot.Translation
{
    public static class CountryCode
    {
        public static string GetCountryCode(string country)
        {
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

        private static string RemoveBetween(string s, char begin, char end)
        {
            Regex regex = new Regex(string.Format("\\{0}.*?\\{1}", begin, end));
            return regex.Replace(s, string.Empty);
        }
    }
}
