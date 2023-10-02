using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using KhpdSynchroService.DBO;

namespace KhpdSynchroService.IO
{
 
    /// <summary>
    /// Класс копирования файла
    /// </summary>
    public class CopyFileInfo
    {
        /// <summary>
        /// Путь
        /// </summary>
        public string FilePath { get; set; }
        /// <summary>
        /// метка времени файла
        /// </summary>
        public DateTime DateTime { get; set; }
        /// <summary>
        /// Необходиомсть копирования
        /// </summary>
        [AtributeNotMappedInType]
        public bool NeedCopy { get; set; }
        ///// <summary>
        ///// Флаг удачного копирования
        ///// </summary>
        //[AtributeNotMappedInType]
        //public bool CopySuccess { get; set; }
        /// <summary>
        /// Путь до источника
        /// </summary>
        private string PathSource { get; set; }
        /// <summary>
        /// Путь до приемника
        /// </summary>
        [AtributeNotMappedInType]
        public List<string> SubDirs { get; set; }
        /// <summary>
        /// Результат
        /// </summary>
        [AtributeNotMappedInType]
        public string ResultedSubDirs { get; set; }
        /// <summary>
        /// Конструктор класса
        /// </summary>
        /// <param name="fi">файл</param>
        /// <param name="pathSource">путь</param>
        public CopyFileInfo(FileInfo fi, string pathSource)
        {
            FilePath = fi.FullName;
            DateTime = fi.LastWriteTime;
            NeedCopy = true;
            PathSource = pathSource;
            SubDirs = new List<string>();
            FillSubDirs(Path.GetDirectoryName(FilePath));
            SubDirs.Reverse();
            ResultSubDirs();
        }
        /// <summary>
        /// Возвращает путь
        /// </summary>
        /// <param name="pathSource">путь</param>
        private void FillSubDirs(string pathSource)
        {
            //bool isFolder = Path.GetExtension(pathSource) == "";
            if (pathSource != PathSource)
            {
                SubDirs.Add(Path.GetFileName(pathSource));
                FillSubDirs(Path.GetDirectoryName(pathSource)); //рекурсия
            }
        }
        /// <summary>
        /// Корректирует путь
        /// </summary>
        private void ResultSubDirs()
        {
            ResultedSubDirs = "";

            foreach (string sbir in SubDirs)
            {
                ResultedSubDirs += "\\" + sbir;
            }
        }
    }
}
