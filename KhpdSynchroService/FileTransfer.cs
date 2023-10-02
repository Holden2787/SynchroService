using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService
{
    internal static class FileTransfer
    {
        static DataTable _mainDataTable;

        public static string CurrentHostName = "";

        public static string TransferLog = "";

        internal static void Execute()
        {
            if (_mainDataTable == null)
            {   // единичная иницализация
                InitializeMainTable();
                FillMainTable();
            }
            TransferFiles();
        }

        static void InitializeMainTable()
        {
            _mainDataTable = new DataTable("MainTable");
            _mainDataTable.Columns.Add("Source", typeof(string));            // 0  Source
            _mainDataTable.Columns.Add("RemUser", typeof(string));           // 6  RemUser
            _mainDataTable.Columns.Add("RemPassword", typeof(string));       // 7  RemPassword
            _mainDataTable.Columns.Add("Dest", typeof(string));              // 2  Dest
            _mainDataTable.Columns.Add("DestRemUser", typeof(string));       // 3  DestRemUser
            _mainDataTable.Columns.Add("DestRemPassword", typeof(string));   // 4  DestRemPassword
            _mainDataTable.Columns.Add("FilterMask", typeof(string));        // 5  FilterMask
            _mainDataTable.Columns.Add("Move", typeof(string));              // 10 Move
            _mainDataTable.Columns.Add("ClearArchSource", typeof(string));   // 11 ClearArchSource
            _mainDataTable.Columns.Add("ClearArchDest", typeof(string));     // 12 ClearArchDest
            _mainDataTable.Columns.Add("StructCopy", typeof(string));        // 13 StructCopy
            _mainDataTable.Columns.Add("FileTypesToProcess", typeof(string));// 14 FileTypesToProcess    (0 - All, 1 - A Only, 2 - N Only)
            _mainDataTable.Columns.Add("RecordDepth", typeof(string));       // 15 RecordDepth           (days count)
            _mainDataTable.Columns.Add("CommandAfter", typeof(string));      // 16 CommandAfter          (cmd command)
        }

        static void FillMainTable()
        {
            try
            {
                using (StreamReader sr = new StreamReader(Properties.Settings.Default.ConfFile, Encoding.GetEncoding("windows-1251")))
                {
                    int CurRow = 0;
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (CurRow > 0)
                        {
                            InsertCSVRowAsDTRow(line);
                        }
                        CurRow++;
                    }
                    Diagnostics.WriteEvent(string.Format("Конфигурационный файл '{0}' успешно загружен. Число обрабатываемых объектов: {1}.", Properties.Settings.Default.ConfFile, _mainDataTable.Rows.Count), EventLogEntryType.Information, Diagnostics.EventID.ConfigurationLoaded);
                }
            }
            catch (Exception ex)
            {
                Diagnostics.WriteEvent("Невозможно прочитать файл конфигурации.\r\nОшибка: " + ex.Message + "\r\nСтек: " + ex.StackTrace, EventLogEntryType.Error, Diagnostics.EventID.ExceptionError);
            }
        }

        static void InsertCSVRowAsDTRow(string CSVRow)
        {
            DataRow DTRow;
            DTRow = _mainDataTable.NewRow();

            string[] split = CSVRow.Split(new char[] { ';' });
            object[] rowArray = new object[split.Length];

            for (int i = 0; i < split.Length; i++)
            {
                rowArray[i] = split[i];
            }

            DTRow.ItemArray = rowArray;
            _mainDataTable.Rows.Add(DTRow);
        }

        static void TransferFiles()
        {
            bool ConnectedTOD = false; string ConnectedDPath = "";
            bool ConnectedTOS = false; string ConnectedSPath = "";

            // Приемник является FTP-сервером
            bool DestinationIsFTP;

            // Количество обработанных файлов
            int NewFiles = 0;
            try
            {
                // Main cycle - conf. objects enumeration to process
                for (int i = 0; i < _mainDataTable.Rows.Count; i++)
                {
                    Configuration cfg = new Configuration(_mainDataTable.Rows[i]);

                    // Адрес удаленного приемника
                    Uri destinationUri = null;
                    // FileTypes
                    int FileTypesToProcess = 0;
                    bool parsed = int.TryParse(cfg.FileTypesToProcess, out FileTypesToProcess);

                    // RecordDepth (num4)
                    int RecordDepth = 0;
                    parsed = int.TryParse(cfg.RecordDepth, out RecordDepth);

                    // Анализ каталога приемника
                    DestinationIsFTP = false;
                    if (cfg.Destination.Path != "")
                    {
                        destinationUri = new Uri(cfg.Destination.Path);
                        // Если приемник - удаленная сетевая папка (не FTP-сервер), то установим соединение
                        if ((destinationUri.Host != "") && (destinationUri.Scheme != "ftp"))
                        {
                            ConnectedDPath = cfg.Destination.Path;
                            ConnectedTOD = ConnectToShareAsUser(ConnectedDPath, cfg.Destination.User, cfg.Destination.Password);
                        }
                        else if (destinationUri.Scheme == "ftp") // Проверка на FTP-сервер
                        {
                            // Приемник является удаленным FTP-сервером
                            DestinationIsFTP = true;
                        }
                    }

                    // check remote host type and availibility Then Connect to it
                    if (!string.IsNullOrEmpty(cfg.Source.Path))
                    {
                        if ((cfg.Source.URI.Host != "") && (cfg.Source.URI.Scheme != "ftp"))
                        {
                            // Удаленное соединение через сетевую папку

                            // if current remote connection is not connected to host - connect it
                            if (CurrentHostName != cfg.Source.URI.Host)
                            {
                                ConnectedSPath = cfg.Source.Path;
                                if (ConnectToShareAsUser(ConnectedSPath, cfg.Source.User, cfg.Source.Password) && Directory.Exists(cfg.Source.Path))
                                {
                                    ConnectedTOS = true;
                                    NewFiles += CopyFolder(cfg.Source.Path,
                                                            cfg.Destination.Path,
                                                            cfg.FilterMask,
                                                            cfg.ClearArchiveSource,
                                                            cfg.ClearArchiveDestination,
                                                            cfg.Move,
                                                            cfg.StructCopy,
                                                            FileTypesToProcess,
                                                            RecordDepth
                                                            );
                                }
                                else
                                {
                                    Diagnostics.WriteEvent("Не удалось подключиться к источнику для объекта " + (i + 1).ToString(), EventLogEntryType.Warning);
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(cfg.Source.URI.Host) && (cfg.Source.URI.Scheme == "ftp")) // Обработка FTP
                        {
                            throw new NotSupportedException("FTP-соединения не поддерживаются");
                        }
                        else // Источник - локальный каталог
                        {
                            if (DestinationIsFTP)
                            {
                                throw new NotSupportedException("FTP-соединения не поддерживаются");
                            }
                            else
                            {
                                // Приемник - локальный или удаленый каталог
                                NewFiles += CopyFolder(cfg.Source.Path,
                                                     cfg.Destination.Path,
                                                     cfg.FilterMask,
                                                     cfg.ClearArchiveSource,
                                                     cfg.ClearArchiveDestination,
                                                     cfg.Move,
                                                     cfg.StructCopy,
                                                     FileTypesToProcess,
                                                     RecordDepth
                                                     );
                            }
                        }
                    }

                    // Выполнение команды (CommandAfter) после завершения обработки очередного направления
                    if (!string.IsNullOrEmpty(cfg.CommandAfter))
                    {
                        // Проверка на сущестоввание файла
                        if (!File.Exists(cfg.CommandAfter))
                        {
                            Diagnostics.WriteEvent(string.Format("Файл команды '{0}' не найден", cfg.CommandAfter), EventLogEntryType.Warning, Diagnostics.EventID.CommandAfterNotFound);
                        }
                        else
                        {
                            try
                            {
                                string filename = cfg.CommandAfter;
                                StreamWriter sw = new StreamWriter(filename + "tmp.cmd", true, Encoding.GetEncoding(866));

                                using (var sr = new StreamReader(filename, Encoding.GetEncoding(1251)))
                                {
                                    string read = null;
                                    while ((read = sr.ReadLine()) != null)
                                    {
                                        sw.WriteLine(read);
                                    }
                                }
                                sw.Flush();
                                sw.Close();

                                ProcessStartInfo PInfo;
                                Process P;

                                PInfo = new ProcessStartInfo(filename + "tmp.cmd")
                                {
                                    CreateNoWindow = true,
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true
                                };
                                P = Process.Start(PInfo);
                                P.WaitForExit(5000);

                                string result = Oem2String(P.StandardOutput.ReadToEnd());
                                string err = Oem2String(P.StandardError.ReadToEnd());

                                P.Close();

                                File.Delete(filename + "tmp.cmd");

                                // Диагностическое сообщение
                                if (Properties.Settings.Default.CommandAfterOutput)
                                {
                                    Diagnostics.WriteEvent(string.Format("Команда '{0}' вернула: {1}{2}", cfg.CommandAfter, result, err), EventLogEntryType.Information, Diagnostics.EventID.CommandAfterOutput);
                                }
                            }
                            catch (Exception ex)
                            {
                                Diagnostics.WriteEvent("Ошибка при запуске команды: " + ex.Message + "\r\nСтек: " + ex.StackTrace, EventLogEntryType.Error, Diagnostics.EventID.ExceptionError);
                            }
                        }
                    }

                    // Отсоединение от приемника, если соединение было установлено
                    if (ConnectedTOD)
                    {
                        DisconnectFromShare(ConnectedDPath);
                        ConnectedTOD = false;
                    }

                    // Отсоединение от источника, если соединение было установлено
                    if (ConnectedTOS)
                    {
                        DisconnectFromShare(ConnectedSPath);
                        ConnectedTOS = false;
                    }
                }

                // Log after full transfer complete
                if (!string.IsNullOrEmpty(TransferLog))
                {
                    Diagnostics.WriteEvent(TransferLog, EventLogEntryType.Information, Diagnostics.EventID.TransferLog);
                    TransferLog = string.Empty;
                }

                // Протоколирование
                if ((NewFiles > 0) || Properties.Settings.Default.Verbose)
                {
                    Diagnostics.WriteEvent($"Обработка завершена. Обработано {NewFiles} новых файлов. Следующий цикл через " + Properties.Settings.Default.FileTransferIntervalMin.ToString() + " минут.", EventLogEntryType.Information, Diagnostics.EventID.Cycle);
                }
            }
            catch (Exception ex)
            {
                Diagnostics.WriteException(ex);
            }
        }

        /// <summary>
        /// Установление соединения с удаленным сервером
        /// </summary>
        /// <param name="resource">Имя удаленного сервера</param>
        /// <param name="user">Имя пользователя</param>
        /// <param name="password">Пароль</param>
        /// <returns></returns>
        static bool ConnectToShareAsUser(string resource, string user, string password)
        {
            ProcessStartInfo PInfo;
            Process P;
            var successMessages = new[] { "The command completed successfully", "Команда выполнена успешно" };

            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(password))
            {
                // явная аутентификация
                PInfo = new ProcessStartInfo("cmd", string.Format(@"/c net use {0} /user:{1} {2}", resource, user, password));
            }
            else
            {
                // интегрированная аутентификация
                // NOTE : если ресурс расшаренный (напр. df.ak.tn.corp), то команда net use может потребовать ввести пароль в консоли,
                // что означает, что процесс повиснет навсегда в ожидании ввода. Избежать этого можно явно указать пользователя под которым вызвается команда
                PInfo = new ProcessStartInfo("cmd", string.Format(@"/c net use {0} /user:{1}", resource, Environment.UserDomainName + "\\" + Environment.UserName));
            }
            PInfo.CreateNoWindow = true;
            PInfo.UseShellExecute = false;

            PInfo.RedirectStandardOutput = true;
            PInfo.RedirectStandardError = true;
            PInfo.Verb = "runas";
            P = Process.Start(PInfo);
            string result = "";
            string error = "";
            if (P.WaitForExit(300000))
            {
                result = P.StandardOutput.ReadToEnd();
                error = P.StandardError.ReadToEnd();
                if (!successMessages.Any(sm => result.Contains(sm)))
                {
                    result = Oem2String(P.StandardOutput.ReadToEnd());
                    error = Oem2String(P.StandardError.ReadToEnd());
                }
            }
            else
            {
                error = "Could not get response from process to establish remote connection for timeout";
            }

            P.Close();

            // Проверка установления соединения
            if (successMessages.Any(sm => result.Contains(sm)))
            {
                return true;
            }
            else
            {
                Diagnostics.WriteEvent("Сервер " + resource + " недоступен. Проверьте параметры доступа. Результат выполнения команды: " + result + error, EventLogEntryType.Warning);
                return false;
            }
        }

        /// <summary>
        /// перекодировка из OEM (866) в строку Unicode
        /// </summary>
        /// <param name="oem">Строка в кодировке OEM</param>
        /// <returns></returns>
        private static string Oem2String(string oem)
        {
            // Перекодировка из OEM (866) -- поддержка русскоязычной операционной системы
            byte[] b = Encoding.Default.GetBytes(oem);
            return Encoding.GetEncoding(866).GetString(b);
        }

        /// <summary>
        /// Копирование каталога
        /// </summary>
        /// <param name="sourcef"></param>
        /// <param name="destf"></param>
        /// <param name="filters"></param>
        /// <param name="ClearArchtoSource"></param>
        /// <param name="ClearArchToDest"></param>
        /// <param name="Move"></param>
        /// <param name="StructCopy"></param>
        /// <param name="FileTypesToProcess"></param>
        /// <param name="RecordDepth"></param>
        /// <returns></returns>
        static int CopyFolder(string sourcef, string destf, string filters, bool ClearArchtoSource, bool ClearArchToDest, bool Move, Configuration.DirectoryMode StructCopy, int FileTypesToProcess, int RecordDepth)
        {
            int TotalFilesCopied = 0;
            try
            {
                // Список файлов для обработки
                List<string> FP = SearchFiles(sourcef, filters, StructCopy, FileTypesToProcess, RecordDepth);
                // Группировка файлов
                List<FileGroup> groups = FileGroup.CreateList(FP);
                // Список обработанных файлов для отката в случае ошибки
                List<string> list = new List<string>();

                // Обработка групп файлов
                foreach (FileGroup group in groups)
                {
                    list.Clear();
                    try
                    {
                        // Лист для протокола
                        List<string> messages = new List<string>();
                        // Обработка файлов в группе
                        foreach (string FullPath in group.files)
                        {

                            string s1 = Path.GetDirectoryName(FullPath);
                            string s2;
                            // s2 добавляется только при полной репликации подкаталогов
                            if ((s1.Length > sourcef.Length) && (StructCopy == Configuration.DirectoryMode.ReplicateSubdirectories))
                                s2 = s1.Substring(sourcef.Length);
                            else
                                s2 = "";
                            string FileName = Path.GetFileName(FullPath);

                            // Имя файла-приемника
                            string destName = destf + s2 + "\\" + FileName;

                            if (NeedToCopy(FullPath, destName))
                            {
                                // Фиксируем имена файлов для отката в случае ошибки
                                list.Add(destName);
                                list.Add(destName + ".tmp");

                                // Каталоги создаются только при полной репликации подкаталогов
                                if ((StructCopy == Configuration.DirectoryMode.ReplicateSubdirectories) && !Directory.Exists(destf + s2 + "\\"))
                                {
                                    Directory.CreateDirectory(destf + s2 + "\\");
                                }

                                // copy or move file with phantom extension to exclude file access
                                if (!Move)
                                {
                                    File.Copy(FullPath, destName + ".tmp", false);
                                    messages.Add(FullPath + " скопирован в " + destf + s2 + "\\" + FileName);
                                }
                                else
                                {
                                    File.Move(FullPath, destName + ".tmp");
                                    messages.Add(FullPath + " перемещен в " + destf + s2 + "\\" + FileName);
                                }

                                // Если целевой файл уже существует
                                if (File.Exists(destName))
                                {
                                    // Удалим его перед переименованием скопированного файла
                                    File.Delete(destName);
                                }
                                // rename to normal name to grant access
                                File.Move(destName + ".tmp", destName);

                                // setting attributes
                                if (ClearArchToDest)
                                {
                                    File.SetAttributes(destName, FileAttributes.Normal);
                                }
                                if (ClearArchtoSource)
                                {
                                    File.SetAttributes(FullPath, FileAttributes.Normal);
                                }
                            }
                        }
                        // Протоколирование после успешного завершения
                        foreach (string m in messages)
                        {
                            SendToTransferLog(m);
                        }
                        TotalFilesCopied += messages.Count;
                    }
                    catch (Exception ex)
                    {
                        Diagnostics.WriteException(ex);
                        // Удаление уже скопированных файлов
                        foreach (string name in list)
                        {
                            // Если файл уже был скопирован, то его следует удалить
                            if (File.Exists(name))
                            {
                                try
                                {
                                    // Попытка удаления файла
                                    File.Delete(name);
                                }
                                catch (Exception ex1)
                                {
                                    // Ошибка при удалении файла не должна блокировать удаление других файлов
                                    Diagnostics.WriteEvent(string.Format("Во время отката копирования группы файлов при удалении файла '{0}' возникла ошибка: {1}", name, ex1.Message), EventLogEntryType.Warning, Diagnostics.EventID.RollbackFailed);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Diagnostics.WriteException(ex);
            }
            return TotalFilesCopied;
        }

        /// <summary>
        /// Построение списка исходных файлов по заданному локальному или сетевому каталогу
        /// </summary>
        /// <param name="sourcef">Исходный каталог</param>
        /// <param name="filters">Строка фильтров</param>
        /// <param name="StructCopy">Режим копирования каталогов</param>
        /// <param name="FileTypesToProcess">Обрабатываемые типы файлов</param>
        /// <param name="RecordDepth">Глубина копирования файлов в днях</param>
        /// <returns></returns>
        static List<string> SearchFiles(string sourcef, string filters, Configuration.DirectoryMode StructCopy, int FileTypesToProcess, int RecordDepth)
        {
            DirectoryInfo Dir = new DirectoryInfo(sourcef);

            // Массив масок файлов
            string[] split = filters.Split(new char[] { '|' });

            // Массив исходных имен файлов
            List<string> FP = new List<string>();

            // Режим поиска подкаталогов
            SearchOption so = SearchOptionByDirectoryMode(StructCopy);

            // fill filepaths array
            foreach (string filter in split)
            {
                FileInfo[] FI = Dir.GetFiles(filter, so);

                foreach (FileInfo info in FI)
                {
                    bool CanFT = false;
                    bool CanRD = false;
                    // FileTypes checking
                    if (FileTypesToProcess > 0)
                    {
                        FileAttributes FA = File.GetAttributes(info.FullName);
                        if ((FA & FileAttributes.Archive) == FileAttributes.Archive && FileTypesToProcess == 1)
                            CanFT = true;
                        if ((FA & FileAttributes.Normal) == FileAttributes.Normal && FileTypesToProcess == 2)
                            CanFT = true;
                    }
                    else
                        CanFT = true;

                    // RecordDepth
                    if (RecordDepth > 0)
                    {
                        DateTime startTime = DateTime.Now;
                        DateTime endTime = File.GetLastWriteTime(info.FullName);
                        TimeSpan span = endTime.Subtract(startTime);
                        int rangeDays = Math.Abs(span.Days);
                        if (rangeDays < RecordDepth)
                            CanRD = true;
                    }
                    else CanRD = true;

                    // CanAdd to file to main CycleTimer
                    if (CanFT && CanRD)
                        FP.Add(info.FullName);
                }
            }

            // sorting by name incl. extensions
            FP.Sort();

            return FP;
        }

        /// <summary>
        /// Режим поиска в соответствии с параметром конфигурации
        /// </summary>
        /// <param name="mode">Параметр конфигурации StructCopy</param>
        /// <returns></returns>
        static SearchOption SearchOptionByDirectoryMode(Configuration.DirectoryMode mode)
        {
            SearchOption so;
            switch (mode)
            {
                case Configuration.DirectoryMode.OnlyCurrent:
                    so = SearchOption.TopDirectoryOnly;
                    break;

                case Configuration.DirectoryMode.FlatSubdirectories:
                case Configuration.DirectoryMode.ReplicateSubdirectories:
                    so = SearchOption.AllDirectories;
                    break;

                default: // Теоретически такого быть не должно
                    so = SearchOption.TopDirectoryOnly;
                    break;
            }
            return so;
        }

        /// <summary>
        /// Проверка необходимости копирования файла
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        static bool NeedToCopy(string source, string dest)
        {
            if (!File.Exists(dest))
            {
                // Если файл-приемник отсутствует, его надо копировать
                return true;
            }
            if (File.GetLastWriteTime(source) > File.GetLastWriteTime(dest))
            {
                // Если источник изменен позже приемника, его надо копировать
                return true;
            }
            // Иначе файл можно смело пропустить
            return false;
        }

        /// <summary>
        /// Формирование списка обработанных файлов
        /// </summary>
        /// <param name="sMessage"></param>
        static void SendToTransferLog(string sMessage)
        {
            // Проверка на необходимость записать событие (максимальный размер - 16000; тут перестраховка)
            if ((TransferLog.Length + sMessage.Length) > 15000)
            {
                Diagnostics.WriteEvent(TransferLog, EventLogEntryType.Information, Diagnostics.EventID.TransferLog);
                TransferLog = string.Empty;
            }
            TransferLog += sMessage + "\r\n";
        }

        /// <summary>
        /// Разрыв соединения с удаленным сервером
        /// </summary>
        /// <param name="RemPath">Путь к сетевому ресурсу</param>
        /// <returns>true - соединение было разорвано (в том числе если оно не было установлено в момент разрыва)
        /// false - ошибка при разрыве соединения</returns>
        static bool DisconnectFromShare(string RemPath)
        {
            ProcessStartInfo PInfo;
            Process P;
            var successMessages = new[] { "The command completed successfully", "успешно удален", "was deleted successfully", "Не удалось найти сетевое подключение" };

            // Отключение с автоматическим подтверждением, если остались (почему-то) открытые подсоединения
            PInfo = new ProcessStartInfo("cmd", string.Format(@"/c net use " + RemPath + " /delete /y"))
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Запуск процесса
            P = Process.Start(PInfo);
            P.WaitForExit(300000);
            string result = P.StandardOutput.ReadToEnd();
            string error = P.StandardError.ReadToEnd();
            if (!successMessages.Any(sm => result.Contains(sm)))
            {
                result = Oem2String(result);
                error = Oem2String(error);
            }
            P.Close();

            // Проверка разрыва соединения
            if (successMessages.Any(sm => result.Contains(sm)))
            {
                return true;
            }
            else
            {
                // Протоколирование, если в файле конфигурации разрешено это
                if (Properties.Settings.Default.DisconnectError)
                {
                    Diagnostics.WriteEvent(string.Format("Ошибка при отсоединении от ресурса '{0}'. Результат выполнения команды '{1} {2}': {3}{4}", RemPath, PInfo.FileName, PInfo.Arguments, result, error), EventLogEntryType.Warning, Diagnostics.EventID.DisconnectError);
                }
                return false;
            }
        }
    }
}
