using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Utils
{
    public static class TimestampedConsole
    {
        public static void Log(string message)
        {
            Console.WriteLine($"{message} - {DateTime.Now}");
        }
    }
}
