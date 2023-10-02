using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService
{
    public class StringComparer : IEqualityComparer<string>
    {
        /// <summary>
        /// Сравнение двух строк без учета регистра
        /// </summary>
        /// <param name="x">Первая строка</param>
        /// <param name="y">Втоаря строка</param>
        /// <returns></returns>
        public bool Equals(string x, string y)
        {
            return x.ToLower() == y.ToLower();
        }

        /// <summary>
        /// Генерация хэш-кода строки без учета регистра
        /// </summary>
        /// <param name="obj">Строка</param>
        /// <returns></returns>
        public int GetHashCode(string obj)
        {
            return obj.ToLower().GetHashCode();
        }
    }
}
