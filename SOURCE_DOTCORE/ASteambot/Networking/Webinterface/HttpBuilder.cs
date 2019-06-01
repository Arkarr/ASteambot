﻿using ASteambot.Networking.Webinterface.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking.Webinterface
{
    class HttpBuilder
    {
        public static HttpResponse InternalServerError()
        {
            string content = File.ReadAllText("website/500.html");

            return new HttpResponse()
            {
                ReasonPhrase = "InternalServerError",
                StatusCode = "500",
                ContentAsUTF8 = content
            };
        }

        public static HttpResponse NotFound()
        {
            string content = File.ReadAllText("website/404.html");

            return new HttpResponse()
            {
                ReasonPhrase = "NotFound",
                StatusCode = "404",
                ContentAsUTF8 = content
            };
        }
    }
}
