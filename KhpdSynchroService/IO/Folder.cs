using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService
{
    internal class Folder
    {
        /// <summary>
        /// Локальный или сетевой путь
        /// </summary>
        internal readonly string Path;
        /// <summary>
        /// Имя пользователя (для сетевого пути)
        /// </summary>
        internal readonly string User;
        /// <summary>
        /// Пароль (для сетевого пути)
        /// </summary>
        internal readonly string Password;
        /// <summary>
        /// Unified Resource Identifier
        /// </summary>
        internal readonly Uri URI;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="path">Локальный или сетевой путь</param>
        /// <param name="user">Имя пользователя (для сетевого пути)</param>
        /// <param name="password">Пароль (для сетевого пути)</param>
        internal Folder(string path, string user, string password)
        {
            Path = path;
            User = user;
            Password = password;
            URI = string.IsNullOrEmpty(Path) ? null : new Uri(Path);
        }
    }
}
