using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KhpdSynchroService.IO;
using Newtonsoft.Json;
using KhpdSynchroService.DBO;
using System.Threading;
using KhpdSynchroService.Conf;

namespace KhpdSynchroService
{
    /// <summary>
    /// Класс трансфера фала
    /// </summary>
    internal class FileTransfer
    {
        /// <summary>
        /// Конфиг
        /// </summary>
        TransferDirection cfg;
        /// <summary>
        /// метрики
        /// </summary>
        MonitoringSettings metric;
        /// <summary>
        /// Список скопированных файлов
        /// </summary>
        List<CopyFileInfo> LastCopyFile;
        /// <summary>
        /// Блокиратор
        /// </summary>
        private static object isLock = new object();
        /// <summary>
        /// Конструктор класса
        /// </summary>
        /// <param name="checkPath">Конфиг</param>
        /// <param name="mtrc">метрики</param>
        public FileTransfer(TransferDirection config)
        {
            cfg = config;
            metric = Configuration.Settings.MonitoringSettings;
        }
        /// <summary>
        /// Главный выполняемый метод 
        /// </summary>
        internal void Execute()
        {
            var isConnectTrap = true;
            //соединение с источником, если он удаленный. Если нет, то провереяем наличие папки. Наличие папки удаленного источника проверяется внутри ConnectToSource.
            if (SynchroService.SMB.TryGetValue(cfg.Source.ToLower(), out var foundSource))
            {
                lock (foundSource.IsLock)
                {
                    if (!foundSource.IsConnection)
                    {

                        var connection = ConnectToSource();
                        foundSource.IsConnection = connection;
                        isConnectTrap = isConnectTrap & connection;

                        Diagnostics.SendToZabbix(connection ? 1 : 0, metric.ConnectionStateSource + $"_{cfg.ID}");
                    }
                    else
                    {
                        try
                        {
                            //если соединение уже установлено то проверям доступность директории
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Проверяем доступность каталога источника - {cfg.Source}.......", EventLogEntryType.Information, Diagnostics.EventID.Cycle);

                            CancellationTokenSource source = new CancellationTokenSource();
                            CancellationToken token = source.Token;

                            //фиксируем допустимое время ожидания
                            int cancelAfter = Configuration.Settings.ConnectionTime * 1000;
                            //флаг доступности каталога
                            var exists = false;

                            var existsTask = Task.Run(() =>
                            {
                                exists = Exists(cfg.Source);
                                if (token.IsCancellationRequested)
                                    return;

                            }, token);

                            source.CancelAfter(cancelAfter);
                            existsTask.Wait(token);
                            existsTask.Dispose();

                            if (!exists)
                                throw new Exception();
                            //если хотим каждый цикл подтверждать доступность источника то 
                            Diagnostics.SendToZabbix(1, metric.ConnectionStateSource + $"_{cfg.ID}");
                            Diagnostics.SendToZabbix(GetFilesSize(cfg.Source, "*", SearchOption.AllDirectories), metric.SourceDirectorySize + $"_{cfg.ID}");

                        }
                        catch (OperationCanceledException)
                        {
                            foundSource.IsConnection = false;
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка проверки доступности каталога {cfg.Source} по timeout", EventLogEntryType.Warning, Diagnostics.EventID.Cycle);
                            DisconnectFromShare(cfg.Source);                            
                            Diagnostics.SendToZabbix(0, metric.ConnectionStateSource + $"_{cfg.ID}");
                            return;
                        }
                        catch
                        {
                            foundSource.IsConnection = false;
                            Diagnostics.SendToZabbix(0, metric.ConnectionStateSource + $"_{cfg.ID}");
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Недоступен сетевой ресурс {cfg.Source}", EventLogEntryType.Warning, Diagnostics.EventID.Cycle);
                            return;
                        }                        
                    }
                }                    
            }
            else
            {
                Diagnostics.WriteEvent($"ID - #{cfg.ID}: Проверяем наличие локального каталога источника - {cfg.Source}.......", EventLogEntryType.Information, Diagnostics.EventID.Cycle);

                if (!Directory.Exists(cfg.Source))
                {
                    Diagnostics.WriteEvent($"ID - #{cfg.ID}: Отсутствует локальный каталог источника - {cfg.Source}", EventLogEntryType.Warning, Diagnostics.EventID.Cycle);
                    Diagnostics.SendToZabbix(0, metric.ConnectionStateSource + $"_{cfg.ID}");

                    isConnectTrap = isConnectTrap & false;
                }
                else
                {
                    Diagnostics.SendToZabbix(1, metric.ConnectionStateSource + $"_{cfg.ID}");
                    Diagnostics.SendToZabbix(GetFilesSize(cfg.Source, "*", SearchOption.AllDirectories), metric.SourceDirectorySize + $"_{cfg.ID}");
                }

            }
            //соединение с приемником
            if (SynchroService.SMB.TryGetValue(cfg.Dest.ToLower(), out var foundDest))
            {
                lock (foundDest.IsLock)
                {
                    if (!foundDest.IsConnection)
                    {
                        //DisconnectFromShare(cfg.Source);

                        var connection = ConnectToDest();
                        foundDest.IsConnection = connection;
                        isConnectTrap = isConnectTrap & connection;
                        Diagnostics.SendToZabbix(connection ? 1 : 0, metric.ConnectionStateDest + $"_{cfg.ID}");
                    }
                    else
                    {
                        try
                        {
                            //если соединение уже установлено то проверям доступность директории
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Проверяем доступность каталога приемника - {cfg.Dest}.......", EventLogEntryType.Information, Diagnostics.EventID.Cycle);

                            CancellationTokenSource source = new CancellationTokenSource();
                            CancellationToken token = source.Token;

                            //фиксируем допустимое время ожидания
                            int cancelAfter = Configuration.Settings.ConnectionTime * 1000;
                            //флаг доступности каталога
                            var exists = false;

                            var existsTask = Task.Run(() =>
                            {
                                exists = Exists(cfg.Dest);
                                if (token.IsCancellationRequested)
                                    return;

                            }, token);

                            source.CancelAfter(cancelAfter);
                            existsTask.Wait(token);
                            existsTask.Dispose();
                            if (!exists)
                                throw new Exception();
                            //если хотим каждый цикл подтверждать доступность источника то 
                            Diagnostics.SendToZabbix(1, metric.ConnectionStateDest + $"_{cfg.ID}");
                            Diagnostics.SendToZabbix(GetFilesSize(cfg.Dest, "*", SearchOption.AllDirectories), metric.DestDirectorySize + $"_{cfg.ID}");
                        }
                        catch (OperationCanceledException)
                        {
                            foundDest.IsConnection = false;
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка проверки доступности каталога {cfg.Dest} по timeout", EventLogEntryType.Warning, Diagnostics.EventID.Cycle);
                            DisconnectFromShare(cfg.Dest);
                            Diagnostics.SendToZabbix(0, metric.ConnectionStateDest + $"_{cfg.ID}");
                            return;
                        }
                        catch
                        {
                            foundDest.IsConnection = false;
                            Diagnostics.SendToZabbix(0, metric.ConnectionStateDest + $"_{cfg.ID}");
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Недоступен сетевой ресурс {cfg.Dest}", EventLogEntryType.Warning, Diagnostics.EventID.Cycle);
                            return;
                        }
                    }
                }                    
            }
            else
            {
                Diagnostics.SendToZabbix(1, metric.ConnectionStateDest + $"_{cfg.ID}");

                Diagnostics.SendToZabbix(GetFilesSize(cfg.Dest, "*", SearchOption.AllDirectories), metric.DestDirectorySize + $"_{cfg.ID}");
            }

            //если соединение доступно то переходим к копированию
            if (isConnectTrap)
            {
                Diagnostics.WriteEvent($"ID - #{cfg.ID}: Выполняется операция проверки наличия новых файлов.", EventLogEntryType.Information, Diagnostics.EventID.Cycle);

                // Количество обработанных файлов
                int NewFiles = 0;
                // Создаем новый объект Stopwatch для фиксации времени выполнения метода копирования
                Stopwatch stopwatch = new Stopwatch();
                double processTime = 0;
                try
                {
                    // Запускаем внутренний таймер объекта Stopwatch
                    stopwatch.Start();
                    NewFiles += CopyFolder();
                    // Останавливаем внутренний таймер объекта Stopwatch
                    stopwatch.Stop();
                    processTime = new TimeSpan(stopwatch.ElapsedTicks).TotalSeconds;                   
                }
                catch
                {
                    NewFiles = -99;
                }

                Diagnostics.SendToZabbix(processTime, metric.ProcessingTime + $"_{cfg.ID}");

                // Протоколирование
                if (NewFiles > 0)
                {
                    var infoFile = new StringBuilder();
                    foreach (var file in LastCopyFile)
                        infoFile.AppendLine($"{file.DateTime} - {file.FilePath}");

                    var txt = $"ID - #{cfg.ID}: Копирование завершено. Обработано {NewFiles} новых файлов. Потрачено на выполнение:{processTime} сек.\r\n{infoFile}";
                    txt = txt.Trim('\n', '\r');
                    Diagnostics.WriteEvent(txt, EventLogEntryType.Information);
                }
                else if(NewFiles == -99)
                    Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка процесса копирования.", EventLogEntryType.Warning, Diagnostics.EventID.Cycle);
                else
                    Diagnostics.WriteEvent($"ID - #{cfg.ID}: Обработка завершена. Отсутствуют новые файлы для передачи. Потрачено на выполнение:{processTime} сек.", EventLogEntryType.Information, Diagnostics.EventID.Cycle);

            }
        }
        /// <summary>
        /// Вернуть размер файлов в байтах
        /// </summary>
        private static double GetFilesSize(string dirToLocal, string ext, SearchOption searchOption)
        {
            var directoryInfo = new DirectoryInfo(dirToLocal);
            double totalSize = directoryInfo.GetFiles(ext, searchOption).Sum(file => file.Length);
            return totalSize/1024;
        }
        /// <summary>
        /// Соединение с источником
        /// </summary>
        /// <returns>статус соединения</returns>
        bool ConnectToSource()
        {
            bool Connected = false;

            string CurrentHostName = System.Net.Dns.GetHostName();

            Uri sourceUri = new Uri(cfg.Source);

            // Проверка присутсвия пути ситочника
            if (!string.IsNullOrEmpty(cfg.Source))
            {
                //если удаленная папка
                if ((sourceUri.Host != "") && (sourceUri.Scheme != "ftp"))
                {
                    // если пусть источника записан через домен и он же является доменом локальным  то нам не нужно подключать соединение
                    if (!string.Equals(CurrentHostName.ToLower(), sourceUri.Host.ToLower()))
                    {
                        if (ConnectToShareAsUser(cfg.Source, cfg.RemUser, cfg.RemPass))
                        { 
                            if(Directory.Exists(cfg.Source))
                                Connected = true;
                            else
                                Diagnostics.WriteEvent($"ID - #{cfg.ID}: Отсутствует удаленный каталог источника - {cfg.Source}", EventLogEntryType.Warning);
                        }
                        else
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Не удалось подключиться к источнику", EventLogEntryType.Warning);
                    }
                    else
                    {
                        Connected = true;
                        // Приемник - локальный или удаленый каталог
                        //Diagnostics.SendToZabbix(1, cfg.Metric);
                    }
                }
                else if (!string.IsNullOrEmpty(sourceUri.Host) && (sourceUri.Scheme == "ftp")) // Обработка FTP
                {
                    throw new NotSupportedException($"ID - #{cfg.ID}: FTP-соединения не поддерживаются");
                }
                else // Источник - локальный каталог
                {
                    Connected = true;
                    // Приемник - локальный или удаленый каталог
                    //Diagnostics.SendToZabbix(1, cfg.Metric);
                }
            }

            return Connected;

        }
        /// <summary>
        /// Соединение с приемником
        /// </summary>
        /// <returns>Статус соединения</returns>
        bool ConnectToDest()
        {
            bool Connected = false;

            string CurrentHostName = System.Net.Dns.GetHostName();

            Uri destinationUri = new Uri(cfg.Dest);

            if (cfg.Dest != "")
            {
                // Если приемник - удаленная сетевая папка (не FTP-сервер), то установим соединение
                if ((destinationUri.Host != "") && (destinationUri.Scheme != "ftp"))
                {
                    if (!string.Equals(CurrentHostName.ToLower(), destinationUri.Host.ToLower()))
                    {
                        if (ConnectToShareAsUser(cfg.Dest, cfg.DestRemUser, cfg.DestRemPass))
                            Connected = true;
                        else
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Не удалось подключиться к приемнику", EventLogEntryType.Warning);
                    }
                    else
                        Connected = true;
                }
                else if (destinationUri.Scheme == "ftp") // Проверка на FTP-сервер
                {
                    // Приемник является удаленным FTP-сервером
                    Connected = false;
                }
                else
                {
                    Connected = true;
                }
            }

            return Connected;            
        }
        /// <summary>
        /// Установление соединения с удаленным сервером
        /// </summary>
        /// <param name="RemComp">Имя удаленного сервера</param>
        /// <param name="user">Имя пользователя</param>
        /// <param name="password">Пароль</param>
        /// <returns>Статус соединения</returns>
        bool ConnectToShareAsUser(string resource, string user, string password)
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
                // NOTE: для расшаренных сетевых ресурсов (напр. df.ak.tn.corp) команда net use может потребовать ввести пароль пользователя в консоли,
                // что не позволит процессу вернуть результат. Для решения такой проблемы надо в параметры комманды указать пользователя, под которым выполняется 
                PInfo = new ProcessStartInfo("cmd", string.Format(@"/c net use {0} /user:{1}", resource, Environment.UserDomainName + "\\" + Environment.UserName));
            }

            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Выполняется подключение к " + resource, EventLogEntryType.Information);

            PInfo.CreateNoWindow = false;
            PInfo.UseShellExecute = false;

            PInfo.RedirectStandardOutput = true;
            PInfo.RedirectStandardError = true;
            PInfo.Verb = "runas";
            P = Process.Start(PInfo);
            string result = "";
            string error = "";
            if (P.WaitForExit(300000)) //300 000 установить
            {
                result = P.StandardOutput.ReadToEnd();
                error = P.StandardError.ReadToEnd();
                if (string.IsNullOrWhiteSpace(error))
                    error = "no";
                Diagnostics.WriteEvent($"ID - #{cfg.ID}: result: {result.Replace("\r\n", " ")} error: {error.Replace("\r\n", " ")}", EventLogEntryType.Information);

                if (!successMessages.Any(sm => result.Contains(sm)))
                {
                    result = Oem2String(P.StandardOutput.ReadToEnd());
                    error = Oem2String(P.StandardError.ReadToEnd());
                }
            }
            else
            {
                error = $"Could not get response from process to establish remote connection for timeout";
            }

            P.Close();

            // Проверка установления соединения
            if (successMessages.Any(sm => result.Contains(sm)))
            {
                //Diagnostics.SendToZabbix(1, resource);
                return true;
            }
            else
            {
                //Diagnostics.SendToZabbix(0, resource);
                Diagnostics.WriteEvent($"ID - #{cfg.ID}: Сервер " + resource + $" недоступен. Проверьте параметры доступа. Результат выполнения команды: result: {result.Replace("\r\n", " ")} error: {error.Replace("\r\n", " ")}", EventLogEntryType.Warning);
                return false;
            }
        }
        /// <summary>
        /// перекодировка из OEM (866) в строку Unicode
        /// </summary>
        /// <param name="oem">Строка в кодировке OEM</param>
        /// <returns>результат кодировки</returns>
        private string Oem2String(string oem)
        {
            // Перекодировка из OEM (866) -- поддержка русскоязычной операционной системы
            byte[] b = Encoding.Default.GetBytes(oem);
            return Encoding.GetEncoding(866).GetString(b);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="srcForder"></param>
        /// <returns></returns>
        int CopyFolder(string srcForder = "")
        {
            if (srcForder == "")
            {
                srcForder = cfg.Source;
            }

            int TotalFilesCopied = 0;

            var newFileInSource = new List<CopyFileInfo>();
            var timeStampFileSource = DateTime.MinValue;

            LastCopyFile = new List<CopyFileInfo>();
            var timeStampFileDest = DateTime.MinValue;

            var timeNow = DateTime.Now;

            // Список файлов для обработки
            List<CopyFileInfo> files = SearchFiles(srcForder);

            if (files.Count == 0) //отменяем если ничего не нашли
            {
                Diagnostics.SendToZabbix(0, metric.CntFileSource + $"_{cfg.ID}");
                Diagnostics.SendToZabbix(0, metric.CntFileProcessed + $"_{cfg.ID}");
                return 0;
            }
            // Группировка файлов
            //List<FileGroup> groups = FileGroup.CreateList(FP); //Не понятно что это

            //Проверяем какие фалы необходимо скопировать.
            DBCopyFile dbchecker = new DBCopyFile();
            //проверяем устанолено ли соединение с БД

            if (!Configuration.Settings.WithoutBD)
            {
                if (dbchecker.isErrConnect())
                    return -99;

                var err = dbchecker.SelectFiles(files);

                if (err)
                    return -99;
            }

            files.OrderBy(f => f.SubDirs.Count);

            
            // Обработка файлов
            foreach (CopyFileInfo copyFileInfo in files)
            {
                if (copyFileInfo.NeedCopy)
                {
                    newFileInSource.Add(copyFileInfo);

                    if (copyFileInfo.DateTime > timeStampFileSource)
                        timeStampFileSource = copyFileInfo.DateTime;

                    string sourceDir = Path.GetDirectoryName(copyFileInfo.FilePath);
                    string FileName = Path.GetFileName(copyFileInfo.FilePath);

                    // Имя файла-приемника
                    string destDir = cfg.Dest + copyFileInfo.ResultedSubDirs;
                    string destName = destDir + "\\" + FileName;

                    // Каталоги создаются только при полной репликации подкаталогов
                    // лучше потом вынести в отдельную процедуру
                    if ((cfg.ReplicateSubdirectories) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    var brakeflag = 0;
                    // Если целевой файл уже существует
                    while (File.Exists(destName))
                    {
                        brakeflag++;
                        // Удалим его перед переименованием скопированного файла
                        File.Delete(destName);
                        System.Threading.Thread.Sleep(100);
                        if (brakeflag == 200)
                            break;
                    }

                    try
                    {
                        File.Copy(copyFileInfo.FilePath, destName, false);
                    }
                    catch(Exception ex)
                    {
                        Diagnostics.WriteEvent(string.Format($"ID - #{cfg.ID}: Ошибка копирования файла {destName}. {ex.Message}. {ex.StackTrace}"), EventLogEntryType.Warning);
                    }

                    //проверяем
                    if (File.Exists(destName))
                    {
                        TotalFilesCopied++;

                        //copyFileInfo.CopySuccess = true;
                        LastCopyFile.Add(copyFileInfo);

                        if (copyFileInfo.DateTime > timeStampFileDest)
                            timeStampFileDest = copyFileInfo.DateTime;
                    }
                    else
                    {
                        Diagnostics.WriteEvent(string.Format($"ID - #{cfg.ID}: Ошибка копирования файла {destName}"), EventLogEntryType.Warning);
                    }
                }
            }

            var cntNewFile = newFileInSource.Count();

            if (cntNewFile != 0)
                Diagnostics.SendToZabbix(ConvertToUnixTimestamp(timeStampFileSource), metric.TimeStampFileSource + $"_{cfg.ID}");

            Diagnostics.SendToZabbix(cntNewFile, metric.CntFileSource + $"_{cfg.ID}");


            var cntCopiedFile = LastCopyFile.Count();
            //Metric: кол-во обработанных файлов
            Diagnostics.SendToZabbix(cntCopiedFile, metric.CntFileProcessed + $"_{cfg.ID}");
            
            if (cntCopiedFile != 0)
                Diagnostics.SendToZabbix(ConvertToUnixTimestamp(timeStampFileDest), metric.TimeStampFileDest + $"_{cfg.ID}");


            //записываем в бд
            if (!dbchecker.InsertData(LastCopyFile))
            {
                Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка записи информации о скопированных файлах в БД", System.Diagnostics.EventLogEntryType.Warning);
                TotalFilesCopied = - 99;
            }

            //если транзакция завершена, перемещаем файл в папку архива
            if (cfg.ArchFileInSource.IsOn)
            {
                foreach (CopyFileInfo copyFileInfo in files)
                {
                    if (!copyFileInfo.NeedCopy)
                    {
                        if ((timeNow - copyFileInfo.DateTime).TotalHours < cfg.ArchFileInSource.AfterDay * 24)
                            continue;

                        try
                        {
                            var arhDir = $"{cfg.Source}\\ARH{copyFileInfo.ResultedSubDirs}\\{copyFileInfo.DateTime.ToString("dd-MM-yyy")}";
                            if (!Directory.Exists(arhDir))
                            {
                                Directory.CreateDirectory(arhDir);
                            }

                            var arhFile = $"{arhDir}\\{Path.GetFileName(copyFileInfo.FilePath)}";

                            if (File.Exists(arhFile))
                                File.Delete(arhFile);

                            File.Copy(copyFileInfo.FilePath, arhFile);
                        }
                        catch (Exception ex)
                        {
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка перемещения файла в архив {ex.Message}", System.Diagnostics.EventLogEntryType.Error);
                        }
                    }
                }
            }
            //Проверяем папку архива на устарелось даннных
            if (cfg.DeleteFilesInSource.IsOn)
            {
                //если транзакция завершена, удаляем файлы
                foreach (CopyFileInfo copyFileInfo in files)
                {
                    if (!copyFileInfo.NeedCopy)
                    {
                        if ((timeNow - copyFileInfo.DateTime).TotalHours < cfg.DeleteFilesInSource.AfterDay * 24)
                            continue;

                        try
                        {
                            File.Delete(copyFileInfo.FilePath);
                        }
                        catch (Exception ex)
                        {
                            Diagnostics.WriteEvent($"ID - #{cfg.ID}: Ошибка удаления файла из папки источника {ex.Message}", System.Diagnostics.EventLogEntryType.Error);
                        }
                    }
                }
            }

            //



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
        List<CopyFileInfo> SearchFiles(string srcFolder)
        {
            List<KeyValuePair<string, SearchOption>> directories = new List<KeyValuePair<string, SearchOption>> { 
                new KeyValuePair<string, SearchOption> (cfg.Source, SearchOption.TopDirectoryOnly) 
            };

            // Массив масок файлов
            string[] split = cfg.FilterMask.Split(new char[] { '|' });

            // Массив исходных имен файлов
            List<CopyFileInfo> FP = new List<CopyFileInfo>();

            // Режим поиска подкаталогов
            //SearchOption so = SearchOptionByDirectoryMode(StructCopy);
            SearchOption searchOption;
            if (cfg.ReplicateSubdirectories)
            {
                searchOption = SearchOption.AllDirectories;

                directories.AddRange(Directory.GetDirectories(cfg.Source).Where(p => p != $"{cfg.Source}\\ARH").Select(x => new KeyValuePair<string, SearchOption>(x, searchOption)));
            }
            else
            {
                searchOption = SearchOption.TopDirectoryOnly;

            }          

            //var timeStampFileSource = DateTime.MinValue;
            // fill filepaths array
            foreach (string filter in split)
            {
                foreach (var dir in directories)
                {
                    var dirInfo = new DirectoryInfo(dir.Key);

                    FileInfo[] FI = dirInfo.GetFiles(filter, dir.Value);

                    foreach (FileInfo info in FI)
                    {
                        FP.Add(new CopyFileInfo(info, cfg.Source));

                        //if (info.LastWriteTime > timeStampFileSource)
                        //    timeStampFileSource = info.LastWriteTime;
                    }

                }
               
            }

            //var cnt = FP.Count();

            //if (cnt != 0)
            //    Diagnostics.SendToZabbix(ConvertToUnixTimestamp(timeStampFileSource), metric.TimeStampFileSource + $"_{cfg.ID}");
                
            //Diagnostics.SendToZabbix(cnt, metric.CntFileSource + $"_{cfg.ID}");

            return FP;
        }
        /// <summary>
        /// Проверка необходимости копирования файла
        /// </summary>
        /// <param name="source">источник</param>
        /// <param name="dest">приемник</param>
        /// <returns>Результат поиска</returns>
        bool NeedToCopy(string source, string dest) //Переписать
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
        /// Разрыв соединения с удаленным сервером
        /// </summary>
        /// <param name="RemPath"></param>
        /// <returns>ошибка при разрыве соединения</returns>
        bool DisconnectFromShare(string RemPath)
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
            P.WaitForExit(60000);
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
                //if (Properties.Settings.Default.DisconnectError)
                //{
                    Diagnostics.WriteEvent(string.Format($"ID - #{cfg.ID}: Ошибка при отсоединении от ресурса '{0}'. Результат выполнения команды '{1} {2}': {3}{4}", RemPath, PInfo.FileName, PInfo.Arguments, result, error), EventLogEntryType.Warning, Diagnostics.EventID.DisconnectError);
                //}
                return false;
            }
        }
        /// <summary>
        /// конвертор типа DateTime в int
        /// </summary>
        /// <param name="date">метка времени</param>
        /// <returns>результат конвертировнаия</returns>
        int ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0).ToLocalTime();
            TimeSpan diff = date - origin;
            return (int)Math.Floor(diff.TotalSeconds);
        }
        /// <summary>
        /// Проверка директории
        /// </summary>
        /// <param name="source">путь</param>
        /// <returns>результат проверки</returns>
        bool Exists (string source)
        {
            var di = new DirectoryInfo(source);
            return di.Exists;
        }
    }
}
