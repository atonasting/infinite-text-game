using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfiniteTextGame.Lib
{
    public class StringHelper
    {
        public static string UTCDateTimeToLocaleString(DateTime UTCDateTime)
        {
            return UTCDateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
