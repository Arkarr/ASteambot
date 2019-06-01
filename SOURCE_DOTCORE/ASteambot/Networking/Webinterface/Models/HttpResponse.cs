using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking.Webinterface.Models
{
    public class HttpResponse
    {
        public string StatusCode { get; set; }
        public string ReasonPhrase { get; set; }
        public byte[] Content { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public string ContentAsUTF8
        {
            set
            {
                this.setContent(value, encoding: Encoding.UTF8);
            }
        }
        public void setContent(string content, Encoding encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }
            Content = encoding.GetBytes(content);
        }

        public HttpResponse()
        {
            this.Headers = new Dictionary<string, string>();
        }

        // informational only tostring...
        public override string ToString()
        {
            return string.Format("HTTP status {0} {1}", this.StatusCode, this.ReasonPhrase);
        }
    }
}
