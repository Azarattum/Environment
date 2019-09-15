using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Environment
{
    static class Utils
    {
        public static void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            DateTime now = DateTime.Now;
            string prefix = $"[{now.ToLongTimeString()}]: ";

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(prefix);

            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
