using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace KhpdSynchroService.Tools
{
    /// <summary>
    /// Класс сериализации файла настроек
    /// </summary>
    public static class Serializator
    {
        /// <summary>
        /// Сохранение файла контрольных сумм
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="filePath">путь к файлу</param>
        /// <returns></returns>
        public static bool SaveXml<T>(object obj, string filePath)
        {
            try
            {
                var xml = new XmlSerializer(typeof(T));
                using (var str = new StreamWriter(filePath))
                {
                    xml.Serialize(str, obj);
                    str.Close();
                }
                return true;

            }
            catch (Exception e)
            {
                Diagnostics.WriteEvent($"Ошибка при сериализации!: {e.Message} {typeof(T).ToString()}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);
                return false;
            }
        }
        /// <summary>
        /// Чтение файла настроек
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filepath">путь к файлу</param>
        /// <returns></returns>
        public static T LoadXml<T>(string filepath)
        {
            if (!File.Exists(filepath))
            {
                Diagnostics.WriteEvent($"Файла не существует: {filepath} для {typeof(T).ToString()}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);
                return default(T);
            }

            T obj;

            try
            {
                var xml = new XmlSerializer(typeof(T));
                using (var str = new StreamReader(filepath))
                {
                    obj = (T)xml.Deserialize(str);
                    str.Close();
                }
                return obj;
            }
            catch (Exception e)
            {
                Diagnostics.WriteEvent($"Ошибка при десериализации!: {e.Message} для {typeof(T).ToString()}", EventLogEntryType.Error, Diagnostics.EventID.Cycle);
            }

            return default(T);
        }

    }
}
