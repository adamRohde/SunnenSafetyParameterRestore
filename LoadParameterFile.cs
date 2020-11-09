using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace _6112020_SunnenSafetyParameterEALTest
{
    class LoadParameterFile
    {
        public string _path { get; set; }
        public static string _messageUpdate { get; set;}
        public static string _errorUpdate { get; set; }
        public static string _statusUpdate { get; set; }

        public static string MessageUpdate()
        {
            return _messageUpdate;
        }

        public static string ErrorUpdate()
        {
            return _errorUpdate;
        }

        public static string StatusUpdate()
        {
            return _statusUpdate;
        }
    }
}
