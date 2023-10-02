using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using KhpdSynchroService.ZabbixIntegration;

namespace KhpdSynchroService
{
    /// <summary>
    /// Формирование диагностических сообщений
    /// </summary>
    internal class Diagnostics
    {
        /// <summary>
        /// логгер
        /// </summary>
        public static readonly ILogger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Блокиратор
        /// </summary>
        public static object isLock = new object();
        /// <summary>
        /// Идентификаторы событий в диагностическом журнале сообщений 
        /// </summary>
        internal enum EventID
        {
            None = 0,
            /// <summary>
            /// Запуск сервиса
            /// </summary>
            OnStart = 1000,
            /// <summary>
            /// Останов сервиса
            /// </summary>
            OnStop,
            /// <summary>
            /// Приостанов сервиса
            /// </summary>
            OnPause,
            /// <summary>
            /// Возобновление сервиса
            /// </summary>
            OnContinue,
            /// <summary>
            /// Конфигурация успешно загружена
            /// </summary>
            ConfigurationLoaded,
            /// <summary>
            /// Завершение цикла обработки
            /// </summary>
            Cycle,
            /// <summary>
            /// Протоколирование списка обработанных файлов
            /// </summary>
            TransferLog,
            /// <summary>
            /// Протоколирование вывода команды CommandAfter
            /// </summary>
            CommandAfterOutput,
            /// <summary>
            /// Ошибка при удалении файла во время отката копирования группы файлов
            /// </summary>
            RollbackFailed,
            /// <summary>
            /// Соединение с основным FTP-сервером не установлено
            /// </summary>
            PrimaryFtpConnectFailed,
            /// <summary>
            /// Параметры резервного источника данных не заданы
            /// </summary>
            NoBackup,
            /// <summary>
            /// Соединение с резервным FTP-сервером не установлено
            /// </summary>
            BackupFtpConnectFailed,
            /// <summary>
            /// Некорректное имя осциллограммы в формате ЭКРА
            /// </summary>
            InvalidEkraName,
            /// <summary>
            /// Обработка исключения
            /// </summary>
            ExceptionError = 2000,
            /// <summary>
            /// Ошибка при разрыве соединения
            /// </summary>
            DisconnectError,
            /// <summary>
            /// Некорректное значение параметра StructCopy
            /// </summary>
            InvalidStructCopyValue,
            /// <summary>
            /// Файл команды не найден
            /// </summary>
            CommandAfterNotFound
        };
        /// <summary>
        /// Формирование диагностического сообщения в журнале приложений
        /// </summary>
        /// <param name="sEvent">Сообшение</param>
        /// <param name="sType">Тип события. Information используется для информационных сообщений
        /// Warning используется для ошибочных ситуаций, которые обработаны в штатном порядке. 
        /// Error используется для ошибочных ситуаций, связанных с перехватом исключений в общем виде</param>
        /// <param name="eventID">Идентификатор события</param>
        internal static void WriteEvent(string sEvent, EventLogEntryType sType, EventID eventID = EventID.None)
        {
            //sEvent += ";" + eventID.ToString();

            //EventLog.WriteEntry(SynchroService.SourceName, sEvent, sType, (int)eventID);
            if (sType == EventLogEntryType.Information)
            {
                logger.Info(sEvent);
            }
            else if (sType == EventLogEntryType.Warning)
            {
                logger.Warn(sEvent);
            }
            else if (sType == EventLogEntryType.SuccessAudit)
            {
                logger.Info(sEvent);
            }
            else if (sType == EventLogEntryType.Error)
            {
                logger.Error(sEvent);
            }
            else if (sType == EventLogEntryType.FailureAudit)
            {
                logger.Error(sEvent);
            }
        }
        /// <summary>
        /// Формирование сообщения об исключении в журнале приложений
        /// </summary>
        /// <param name="ex">Исключение</param>
        internal static void WriteException(Exception ex)
        {
            if (ex is ApplicationException)
            {
                // Прикладные ошибки являются предупреждениями
                WriteEvent(ex.Message, EventLogEntryType.Warning, Diagnostics.EventID.ExceptionError);
            }
            else
            {
                WriteEvent(string.Format("Ошибка ({0}): {1}{2}Стек: {3}", ex.GetType().Name, ex.Message, Environment.NewLine, ex.StackTrace), EventLogEntryType.Error, Diagnostics.EventID.ExceptionError);
            }
        }
        /// <summary>
        /// Метод формирования сообщения в zabbix
        /// </summary>
        /// <param name="value"></param>
        /// <param name="metric"></param>
        internal static void SendToZabbix(object value, string metric)
        {
            ZabbixSender.SendAsync(metric, new ZabbixValue(value));
        }

    }
}
