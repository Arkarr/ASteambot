using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ASteambot.SteamTrade
{
    public class CommmunityInventory
    {
        /*public static CommmunityInventory FetchInventory(ulong steamId, int gameID, SteamWebCustom steamWeb, long contextID = 2)
        {
            int attempts = 1;
            CommmunityInventoryResponse result = null;
            while ((result == null || result.result == null || result.result.items == null) && attempts <= 3)
            {
                bool isEmpty = true;
                while (isEmpty)
                {
                    var url = "https://steamcommunity.com/profiles/" + steamId + "/inventory/json/" + gameID + "/" + contextID + "/";
                    string response = steamWeb.Fetch(url, "GET", null, true, "", true);

                    isEmpty = false;
                    if (response.Length <= 4 && attempts <= 3)
                    {
                        Thread.Sleep(10 * 1000);
                        isEmpty = true;
                        attempts++;
                    }
                    else
                    {
                        result = JsonConvert.DeserializeObject<CommmunityInventoryResponse>(response);
                    }
                }
                attempts++;
            }
            return new CommmunityInventory(result.result);

        }*/
    }
}
