using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambotInterfaces.ASteambotInterfaces
{
    public interface IItem
    {
        long contextid { get; }
        ulong assetid { get; }
        int appid { get; }
        int amount { get; }
        string descriptionid { get; }
    }
}
