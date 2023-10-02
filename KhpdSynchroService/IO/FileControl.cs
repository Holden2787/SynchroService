using KhpdSynchroService.Conf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService.IO
{
    internal class FileControl
    {
        /// <summary>
        /// Конфиг
        /// </summary>
        TransferDirection cfg;


        public FileControl(TransferDirection config)
        {
            cfg = config;
        }


        /// <summary>
        /// Вычисление условий по удалению ( превышение хранение по дате или по количеству файлов в директорию)
        /// </summary>
        /// <param name="directoryInfo">директория расположения файлов</param>
        /// <param name="maxArchiveFiles">максимальное количество файлов в директории</param>
        /// <param name="maxLifeTimeFilesInSec">максимальное времы в секунда</param>
        public void Calculations()
        {          

            var now = DateTime.Now;
            DirectoryInfo di = new DirectoryInfo($"{cfg.Source}\\ARH");

            if (!di.Exists)
                return;

            var maxSizeFilesInArchive = Configuration.Settings.MaxSizeFilesInArchive;
            var maxLifeTimeFilesInDay = Configuration.Settings.MaxLifeTimeFilesInDay;

            var directories = di.GetDirectories().OrderBy(t => t.CreationTime).ToArray();            

            var files = di.GetFiles("*", SearchOption.TopDirectoryOnly);
            try
            {
                if (files.Any())
                {
                    foreach (var file in files)
                        file.Delete();
                }
            }
            catch (Exception ex)
            {
                Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка очистки файлов находящихся вне директорий с датами {ex.Message}. {ex.StackTrace}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);
                return;
            }

            //var size = di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);

            while (di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length) >  maxSizeFilesInArchive * 1024 * 1024 * 1024)
            {                
                if (!directories.Any())
                    break;
                
                if(directories.Count() == 1) 
                {
                    try
                    {
                        files = directories.First().GetFiles("*", SearchOption.TopDirectoryOnly).OrderBy(x => x.CreationTime).ToArray();
                        long ls = 0;
                        foreach (var file in files)
                        {
                            if (ls > 100 * 1024 * 1024)
                                break;

                            ls = ls + file.Length;
                            file.Delete();
                        }
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка очистки файлов в единственно оставшейся папке {ex.Message}. {ex.StackTrace}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);
                        break;
                    }
                }

                try
                {
                    directories.First().Delete(true);

                    directories = di.GetDirectories().OrderBy(t => t.CreationTime).ToArray();
                }
                catch (Exception ex)
                {
                    Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка очистки архива методом контроля размера {ex.Message}. {ex.StackTrace}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);

                    break;
                }               
            }            

            while (directories.Any(d => d.CreationTime.Date < now.AddDays(-maxLifeTimeFilesInDay).Date))
            {
                if (!directories.Any())
                    break;
                try
                { 
                    directories.First().Delete(true);

                    directories = di.GetDirectories().OrderBy(t => t.CreationTime).ToArray();
                }
                catch (Exception ex)
                {
                    Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка очистки архива методом контроля даты создания {ex.Message}. {ex.StackTrace}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);

                    break; 
                }              
            }            
        }
    }
}
