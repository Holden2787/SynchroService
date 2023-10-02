using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService
{
    internal class FileGroup
    {
        /// <summary>
        /// Список файлов, входящих в группу
        /// </summary>
        internal readonly List<string> files;

        /// <summary>
        /// Конструктор по умолчанию
        /// </summary>
        internal FileGroup()
        {
            files = new List<string>();
        }

        /// <summary>
        /// Конструктор с добавлением двух элементов списка
        /// </summary>
        /// <param name="s1">Первый элемент группы</param>
        /// <param name="s2">Второй элемент группы</param>
        internal FileGroup(string s1, string s2)
        {
            files = new List<string>();
            files.Add(s1);
            files.Add(s2);
        }

        /// <summary>
        /// Группировка списка файлов по группам в соответствии с расширениями
        /// </summary>
        /// <param name="files">Список файлов</param>
        /// <returns></returns>
        static internal List<FileGroup> CreateList(List<string> files)
        {
            
            List<FileGroup> list = new List<FileGroup>();
            /*
            // Сравнение строк без учета регистра
            .StringComparer comparer = new StringComparer();

            // Цикл обработки файлов
            foreach (string path in files)
            {
                switch (System.IO.Path.GetExtension(path).ToUpper())
                {
                    case ".CFG": // Конфигурация COMTRADE 
                        // Проверка на наличие файла DAT
                        string dat = System.IO.Path.ChangeExtension(path, "DAT");
                        if (files.Contains<string>(dat, comparer))
                        {
                            // Файл DAT имеется; уже можно формировать группу
                            FileGroup group = new FileGroup(path, dat);
                            // Проверка на наличие файла HDR (необязательный файл)
                            string hdr = System.IO.Path.ChangeExtension(path, "HDR");
                            if (files.Contains<string>(hdr, comparer))
                            {
                                // Файл HDR имеется; он становится частью группы
                                group.files.Add(hdr);
                            }
                            list.Add(group);
                        }
                        break;

                    case ".DAT": // Данные COMTRADE
                    case ".HDR": // Заголовок COMTRADE 
                        // Файл пропускаем, так как он обрабатывается вместе с CFG
                        break;

                    case ".DFR": // ЭКРА
                        // Проверка на наличие файла SFR
                        // Структура имени DFR-файла: nnnDnnnn.DFR
                        // Структура имени SFR-файла: nnnSnnnn.SFR
                        string name = System.IO.Path.GetFileNameWithoutExtension(path);
                        string sfr;
                        if (name.Length == 8)
                        {
                            // Проверка на наличие каталога (для FTP-передачи каталог отсутствует, для остальных - присутствует)
                            string dir = System.IO.Path.GetDirectoryName(path);
                            if (string.IsNullOrEmpty(dir))
                            {
                                sfr = string.Format("{0}s{1}.sfr", name.Substring(0, 3), name.Substring(4, 4));
                            }
                            else
                            {
                                sfr = string.Format("{0}{1}{2}s{3}.sfr", dir, System.IO.Path.DirectorySeparatorChar, name.Substring(0, 3), name.Substring(4, 4));
                            }
                        }
                        else
                        {
                            Diagnostics.WriteEvent(string.Format("Некорректное имя файла осциллограммы в формате ЭКРА: '{0}'. Имя должно содержать 8 символов", name), System.Diagnostics.EventLogEntryType.Warning, Diagnostics.EventID.InvalidEkraName);
                            sfr = System.IO.Path.ChangeExtension(path, "SFR");
                        }
                        if (files.Contains<string>(sfr, comparer))
                        {
                            // Файл SFR имеется; уже можно формировать группу
                            FileGroup group = new FileGroup(path, sfr);
                            list.Add(group);
                        }
                        break;

                    case ".SFR": // ЭКРА
                        // Файл пропускаем, так как он обрабатывается вместе с DFR
                        break;

                    case ".REH": // ABB
                        // Проверка на наличие файла REV
                        string rev = System.IO.Path.ChangeExtension(path, "REV");
                        if (files.Contains<string>(rev, comparer))
                        {
                            // Файл REV имеется; уже можно формировать группу
                            FileGroup group = new FileGroup(path, rev);
                            list.Add(group);
                        }
                        break;

                    case ".REV": // ABB
                        // Файл пропускаем, так как он обрабатывается вместе с REH
                        break;

                    default: // Все остальные файлы формируют собственную группу
                        FileGroup gr = new FileGroup();
                        gr.files.Add(path);
                        list.Add(gr);
                        break;
                }
            }
            */

            return list;
        }
    }
}
