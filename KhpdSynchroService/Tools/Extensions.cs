using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService.Tools
{
    public static class StringExtensions
    {
        /// <summary>
        /// перекодировка из OEM (866) в строку Unicode
        /// </summary>
        /// <param name="s">Строка в кодировке OEM</param>
        /// <returns>Unicode</returns>
        public static string Oem866_2_Unicode(this string s)
        {
            // Перекодировка из OEM (866) -- поддержка русскоязычной операционной системы
            byte[] b = Encoding.Default.GetBytes(s);
            return Encoding.GetEncoding(866).GetString(b);
        }
    }
}
