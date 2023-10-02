using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService.IO
{
    /// <summary>
    /// Класс структуры конфига
    /// </summary>
    internal class Configuration
    {
        /// <summary>
        /// ID
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// Путь до источника
        /// </summary>
        public string Source { get; set; }
        /// <summary>
        /// Имя пользователя
        /// </summary>
        public string RemUser { get; set; }
        /// <summary>
        /// Пароль
        /// </summary>
        public string RemPass { get; set; }
        /// <summary>
        /// Путь до приемника
        /// </summary>
        public string Dest { get; set; }
        /// <summary>
        /// Имя пользователя
        /// </summary>
        public string DestRemUser { get; set; }
        /// <summary>
        /// Пароль
        /// </summary>
        public string DestRemPass { get; set; }
        /// <summary>
        /// Маска фильтра
        /// </summary>
        public string FilterMask { get; set; }
        /// <summary>
        /// Период опроса
        /// </summary>
        public int TimerTick { get; set; }
        /// <summary>
        /// Репликаци подкаталогов
        /// </summary>
        public bool ReplicateSubdirectories { get; set; }
        /// <summary>
        /// Очистка источника
        /// </summary>
        public int? DeleteFilesInSource { get; set; }
        /// <summary>
        /// Архивировать файл на стороне источника
        /// </summary>
        public int? ArchFileInSource { get; set; }


        /// <summary>
        /// Конструктор класса
        /// </summary>
        /// <param name="row">Строка</param>
        internal Configuration(DataRow row)
        {
            ID = row.ItemArray[0].ToString();
            Source = row.ItemArray[1].ToString();
            RemUser = row.ItemArray[2].ToString();
            RemPass = row.ItemArray[3].ToString();

            Dest = row.ItemArray[4].ToString();
            DestRemUser = row.ItemArray[5].ToString();
            DestRemPass = row.ItemArray[6].ToString();
            FilterMask = row.ItemArray[7].ToString();

            TimerTick = (int)row.ItemArray[8];
            ReplicateSubdirectories = (bool)row.ItemArray[9];
            DeleteFilesInSource = string.IsNullOrWhiteSpace(row.ItemArray[10].ToString()) ? null : (int?)Convert.ToInt32(row.ItemArray[10].ToString());
            ArchFileInSource = string.IsNullOrWhiteSpace(row.ItemArray[11].ToString()) ? null : (int?)Convert.ToInt32(row.ItemArray[11].ToString());
        }

        /// <summary>
        /// Инициалиация
        /// </summary>
        void InitializeMainTable() 
        {
        
        }
    }
}
