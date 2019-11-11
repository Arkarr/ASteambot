using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambotInterfaces.ASteambotInterfaces
{
    public interface IItemDescription
    {
         string name { get; set; }
         string type { get; set; }
         bool tradable { get; set; }
         bool marketable { get; set; }
         string url { get; set; }
         long classid { get; set; }
         long market_fee_app_id { get; set; }
         string market_hash_name { get; set; }
    }
}
