using System;
using System.Xml.Serialization;

namespace KhpdSynchroService.Conf
{
    /// <summary>
    /// Описывает структуру хранения настроек приложения
    /// </summary>
    [Serializable]
    public class Settings
    {        
        /// <summary>
        /// Для тестирования функционала исключаем БД
        /// </summary>
        public bool WithoutBD;
        /// <summary>
        /// Имя службы
        /// </summary>
        public string ServiceName;
        /// <summary>
        /// Отображаемое имя службы
        /// </summary>
        public string DisplayName;
        /// <summary>
        /// Описание службы
        /// </summary>
        public string ServiceDescription;
        /// <summary>
        /// Сервер заббикса
        /// </summary>
        public string ZabbixServer;
        /// <summary>
        /// Порт заббикса
        /// </summary>
        public int ZabbixPort;
        /// <summary>
        /// Хост заббикса
        /// </summary>
        public string ZabbixHost;
        /// <summary>
        /// Строка подключения к MS SQL
        /// </summary>
        public string SQLConnString;
        /// <summary>
        /// Имя таблицы для хранений истории в MS SQL
        /// </summary>
        public string SqlTableToInsert;
        /// <summary>
        /// Имя созаваемого типа для загрузки истории в MS SQL
        /// </summary>
        public string SqlTypeTableCreate;
        /// <summary>
        /// Timeout доступа к файловому ресурсу(сек)
        /// </summary>
        public int ConnectionTime;
        /// <summary>
        /// Timeout SQL запроса(сек)
        /// </summary>
        public int TimeoutQuery;
        /// <summary>
        /// Максимальный размер файлов в архиве хранящиеся на источнике(Гб)
        /// </summary>
        public int MaxSizeFilesInArchive;
        /// <summary>
        /// Максимальный время жизни файлов в архиве хранящиеся на источнике(дней)
        /// </summary>
        public int MaxLifeTimeFilesInDay;
        /// <summary>
        /// Уставки
        /// </summary>
        public TransferDirection[] TransferDirections; 
        /// <summary>
        /// Маппинг метрик мониторинга к объектам
        /// </summary>
        public MonitoringSettings MonitoringSettings;

    }

    /// <summary>
    /// Уставки
    /// </summary>
    [Serializable]
    public class TransferDirection
    {
        /// <summary>
        /// Передача включён
        /// </summary>
        [XmlAttribute("isOn")]
        public bool IsOn;
        /// <summary>
        /// Идентификатор передачи
        /// </summary>
        [XmlAttribute("id")]
        public string ID;
        /// <summary>
        /// Период опроса
        /// </summary>
        [XmlAttribute("intervalMin")]
        public int TimerTick;
        /// <summary>
        /// Директория источника файлового ресурса
        /// </summary>
        public string Source;
        /// <summary>
        /// Имя пользователя уч. записи под которым выполняется подключение к ресурсу
        /// </summary>
        public string RemUser;
        /// <summary>
        /// Пароль от уч. записи под которой выполняется подключение к ресурсу
        /// </summary>
        public string RemPass;
        /// <summary>
        /// Директория приемника файлового ресурса
        /// </summary>
        public string Dest;
        /// <summary>
        /// Имя пользователя уч. записи под которым выполняется подключение к ресурсу
        /// </summary>
        public string DestRemUser;
        /// <summary>
        /// Пароль от уч. записи под которой выполняется подключение к ресурсу
        /// </summary>
        public string DestRemPass;
        /// <summary>
        /// Фильтр обрабатываемых файлов
        /// </summary>
        public string FilterMask;
        /// <summary>
        /// Выполнять репликацию директорий   
        /// </summary>
        public bool ReplicateSubdirectories;
        /// <summary>
        /// Выполнять удаление обработанных файлов из источника
        /// </summary>
        public DeleteFilesInSource DeleteFilesInSource;
        /// <summary>
        /// Выполнять архивацию обработанных файлов
        /// </summary>
        public ArchFileInSource ArchFileInSource;
    }

    /// <summary>
    /// Уставки
    /// </summary>
    [Serializable]
    public class DeleteFilesInSource
    {
        /// <summary>
        /// Передача включён
        /// </summary>
        [XmlAttribute("afterDay")]
        public int AfterDay;
        /// <summary>
        /// Передача включён
        /// </summary>
        [XmlAttribute("isOn")]
        public bool IsOn;
    }
    /// <summary>
    /// Уставки
    /// </summary>
    [Serializable]
    public class ArchFileInSource
    {
        /// <summary>
        /// Передача включён
        /// </summary>
        [XmlAttribute("afterDay")]
        public int AfterDay;
        /// <summary>
        /// Передача включён
        /// </summary>
        [XmlAttribute("isOn")]
        public bool IsOn;
    }

    /// <summary>
    /// Маппинг мониторинга
    /// </summary>
    [Serializable]
    public class MonitoringSettings
    {
        public string ConnectionStateSource;
        public string ConnectionStateDest;
        public string ProcessingTime;
        public string TimeStampFileSource;
        public string TimeStampFileDest;
        public string CntFileSource;
        public string CntFileProcessed;
        public string LiveCounter;
        public string SourceDirectorySize;
        public string DestDirectorySize;
    }
}
