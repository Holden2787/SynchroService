using KhpdSynchroService.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
//using System.Timers;
using Newtonsoft.Json;
using System.Threading;
using System.IO;
using KhpdSynchroService.Conf;

namespace KhpdSynchroService
{
    /// <summary>
    /// Виндовая служба "Служба синхронизации файловых ресурсов" 
    /// </summary>
    partial class SynchroService : ServiceBase
    {
        /// <summary>
        /// таймер
        /// </summary>
        static System.Threading.Timer LiveTimer;
        /// <summary>
        /// Словарь соединений с источником/приемником
        /// </summary>
        public static Dictionary<string, Connection> SMB;
        /// <summary>
        /// Конструктор класса
        /// </summary>
        public SynchroService()
        {
            InitializeComponent();
        }
        /// <summary>
        /// При запуске службы
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
           
            Diagnostics.WriteEvent($"Сервис ССФР запущен. Версия {fvi.FileVersion}", EventLogEntryType.Information);

            if (Configuration.Settings is null || Configuration.Settings.TransferDirections is null)
            {
                Diagnostics.WriteEvent($"Ошибка загрузки конфигурационного файла", EventLogEntryType.Error);
                return;
            }

            if (!Configuration.Settings.TransferDirections.Any(x => x.IsOn) || Configuration.Settings.TransferDirections.Any(x => x.IsOn && string.IsNullOrWhiteSpace(x.ID)))
            {
                Diagnostics.WriteEvent($"Не задан ID для одной из включенных конфигураций", EventLogEntryType.Error);
                return;
            }

            if (Configuration.Settings.TransferDirections.Where(x => x.IsOn).GroupBy(x => x.ID).Where(x => x.Count() > 1).Any())
            {
                Diagnostics.WriteEvent($"Во включенных конфигурациях присутствую одинаковые ID", EventLogEntryType.Error);
                return;
            }

            if (Configuration.Settings.TransferDirections.Any(x => x.IsOn && string.IsNullOrWhiteSpace(x.Source)))
            {
                Diagnostics.WriteEvent($"Не задана директория источника для одной из включенных конфигураций", EventLogEntryType.Error);
                return;
            }
            if (Configuration.Settings.TransferDirections.Any(x => x.IsOn && string.IsNullOrWhiteSpace(x.Dest)))
            {
                Diagnostics.WriteEvent($"Не задана директория приёмника для одной из включенных конфигураций", EventLogEntryType.Error);
                return;
            }



            Diagnostics.WriteEvent($"Конфигурационный файл успешно загружен. Число конфигураций: {Configuration.Settings.TransferDirections.Count(x =>x.IsOn)}", EventLogEntryType.Information);

            SMB = new Dictionary<string, Connection>();	

            string CurrentHostName = System.Net.Dns.GetHostName();

            //запустим счетчик жизни службы
            LiveTimer = new System.Threading.Timer(LiveCounter, null, 10000, 20000);

            foreach (var config in Configuration.Settings.TransferDirections)
            {
                if (!config.IsOn)
                    continue;
                var logReplicatDir = config.ReplicateSubdirectories == true ? "УСТАНОВЛЕНА" : "ОТКЛЮЧЕНА";
                var logArh = config.ArchFileInSource.IsOn == false ? "- ОТКЛЮЧЕНО" : config.ArchFileInSource.AfterDay == 0 ? "- СРАЗУ" : $"через {config.ArchFileInSource.AfterDay}дней.";
                var logDel = config.ArchFileInSource.IsOn == false ? "- ОТКЛЮЧЕНО" : config.DeleteFilesInSource.AfterDay == 0 ? "- СРАЗУ" : $"через {config.DeleteFilesInSource.AfterDay}дней.";

                Diagnostics.WriteEvent($"ID - #{config.ID}: Директория источника - {config.Source}; Директория приемника - {config.Dest}; Период опроса {config.TimerTick}мин; Репликация директорий - {logReplicatDir}; Архивирование файлов источника после копирования {logArh}; Удаление файлов источника после копирования {logDel}", EventLogEntryType.Information, Diagnostics.EventID.Cycle);

                Uri sourceUri = new Uri(config.Source);
                Uri destinationUri = new Uri(config.Dest);

                //Заполняем словарь только удаленными каталогами источника и приемника к которым будет организовано подключение как сетевой диск
                //условие заполнени словаря: в пути присутствует имя хоста и это имя не соответвует текущей машине
                if (!string.IsNullOrEmpty(sourceUri.Host) && !string.Equals(CurrentHostName.ToLower(), sourceUri.Host.ToLower()) && !SMB.ContainsKey(config.Source.ToLower()))
                    SMB.Add(config.Source.ToLower(), new Connection());

                if (!string.IsNullOrEmpty(destinationUri.Host) && !string.Equals(CurrentHostName.ToLower(), destinationUri.Host.ToLower()) && !SMB.ContainsKey(config.Dest.ToLower()))
                    SMB.Add(config.Dest.ToLower(), new Connection());

                //основной поток обработки файлов
                FileTransferTimer_Elapsed(config);
                //запускаем поток контроля размера архива
                ArchiveControl(config);
            }
        }
        /// <summary>
        /// Циклический метод обработки конфига
        /// </summary>
        /// <param name="metric">Конфиг</param>
        /// <returns></returns>
        public Task FileTransferTimer_Elapsed(TransferDirection config)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    FileTransfer ft = new FileTransfer(config);

                    while (true)
                    {
                        Diagnostics.WriteEvent($"ID - #{config.ID}: Запущена обработка конфигурации.....", EventLogEntryType.Information);

                        ft.Execute();

                        Thread.Sleep(config.TimerTick * 60000);
                    }
                }
                catch (Exception ex)
                {
                    Diagnostics.WriteEvent($"ID - #{config.ID}: Ошибка обработки конфигурации {ex.Message}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);
                }
            });
        }

        /// <summary>
        /// Циклический метод обработки конфига
        /// </summary>
        /// <param name="metric">Конфиг</param>
        /// <returns></returns>
        public Task ArchiveControl(TransferDirection config)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    FileControl fc = new FileControl(config);

                    while (true)
                    {
                        if (!config.ArchFileInSource.IsOn)
                        {
                            Diagnostics.WriteEvent($"ID - #{config.ID}: Контроль над архивом не осуществляется, так как функция архивирования отключена", EventLogEntryType.Information);
                            break;
                        }

                        Diagnostics.WriteEvent($"ID - #{config.ID}: Запущен модуль контроля размера архива", EventLogEntryType.Information);

                        fc.Calculations();

                        Thread.Sleep(120 * 60000);
                    }
                }
                catch (Exception ex)
                {
                    Diagnostics.WriteEvent($"ID - #{config.ID}: Ошибка модуля контроля размера архива {ex.Message}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);
                }
            });
        }

        /// <summary>
        /// Запуск в консольном режиме
        /// </summary>
        public void StartService()
        {
            OnStart(null);
        }
        /// <summary>
        /// Остановка консольного приложения
        /// </summary>
        public void StopService()
        {
            OnStop();
        }
        /// <summary>
        /// Счетчик жизни
        /// </summary>
        /// <param name="state"></param>
        void LiveCounter(object state)
        {
            Diagnostics.SendToZabbix(1, Configuration.Settings.MonitoringSettings.LiveCounter);
        }		
    }

    /// <summary>
    /// Класс соединения с источником/приемником
    /// </summary>
    class Connection
    {
        public object IsLock;
        public bool IsConnection;
        /// <summary>
        /// Конструктор класса
        /// </summary>
        public Connection()
        {
            IsLock = new object();
            IsConnection = false;
        }
    }
}
