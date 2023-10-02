using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService.IO
{
    /// <summary>
    /// Класс метрик мониторинга
    /// </summary>
    public class MetricsConfiguration
    {
        /// <summary>
        /// доступность входящего файлового ресурса 
        /// </summary>
        public string ConnectionStateSource { get; set; }
        /// <summary>
        ///  доступность исходящего файлового ресурса 
        /// </summary>
        public string ConnectionStateDest { get; set; }
        /// <summary>
        /// длительность обработки файлов
        /// </summary>
        public string ProcessingTime { get; set; }
        /// <summary>
        /// метка времени последнего файла в источнике
        /// </summary>
        public string TimeStampFileSource { get; set; }
        /// <summary>
        /// метка времени последнего файла в приемнике
        /// </summary>
        public string TimeStampFileDest { get; set; }
        /// <summary>
        /// количество файлов в источнике
        /// </summary>
        public string CntFileSource { get; set; }
        /// <summary>
        /// количество обработанных файлов
        /// </summary>
        public string CntFileProcessed { get; set; }
        /// <summary>
        /// сигнал жизни службы
        /// </summary>
        public string LiveCounter { get; set; }
        /// <summary>
        /// размер папки источника
        /// </summary>
        public string SourceDirectorySize { get; set; }
        /// <summary>
        /// размер папки приемника
        /// </summary>
        public string DestDirectorySize { get; set; }

    }
}
