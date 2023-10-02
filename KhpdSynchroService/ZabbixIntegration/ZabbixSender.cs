using KhpdSynchroService.Conf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KhpdSynchroService.ZabbixIntegration
{
    /// <summary>
    /// Отправляет метрики в заббикс
    /// </summary>
    class ZabbixSender
    {
        /// <summary>
        /// адресс
        /// </summary>
        public static string Ip;
        /// <summary>
        /// порт
        /// </summary>
        public static int Port;
        /// <summary>
        /// Хост
        /// </summary>
        public static string Host;
        /// <summary>
        /// Класс формата сообщения
        /// </summary>
        static NumberFormatInfo nfi = new NumberFormatInfo();
        /// <summary>
        /// Таймер
        /// </summary>
        static Timer sendTimer;
        /// <summary>
        /// флаг отправки
        /// </summary>
        static bool sending;
        /// <summary>
        /// Лист сообщений
        /// </summary>
        static List<KeyValuePair<string, ZabbixValue>> queueList;
        /// <summary>
        /// ошибка соединения
        /// </summary>
        static bool commError;
        /// <summary>
        /// Конструктор класса
        /// </summary>
        static ZabbixSender()
        {
            nfi.NumberGroupSeparator = ".";
            queueList = new List<KeyValuePair<string, ZabbixValue>>();
            sendTimer = new Timer(sendTimerTick, null, 10000, 10000);
            Ip = Configuration.Settings.ZabbixServer; //"10.7.142.202";
            Port = Configuration.Settings.ZabbixPort; // 10051;
            Host = Configuration.Settings.ZabbixHost; // "vdc01-peintktn3";
            //Ip = "172.24.5.65";
            //Port = 10051;
            //Host = "CDC01-PEINTKT01";
        }
        /// <summary>
        /// Добавляет значение метрики в очередь на отправку
        /// </summary>
        /// <param name="key">Ключ</param>
        /// <param name="value">Значение</param>
        public static void SendAsync(string key, ZabbixValue value)
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, ZabbixValue>(key, value));
            }
        }

        /// <summary>
        /// Добавляет значение метрики в очередь на отправку
        /// </summary>
        /// <param name="values">Словарь значений</param>
        public static void SendAsync(Dictionary<string, ZabbixValue> values)
        {
            lock (queueList)
            {
                foreach (var v in values)
                {
                    queueList.Add(new KeyValuePair<string, ZabbixValue>(v.Key, v.Value));
                }
            }
        }
        /// <summary>
        /// Отправка событий по таймеру, пробуем снизить нагрузку на заббикс
        /// </summary>
        /// <param name="state"></param>
        static void sendTimerTick(object state)
        {
            if (sending)
                return;

            sending = true;

            List<KeyValuePair<string, ZabbixValue>> buf;

            lock (queueList)
            {
                if (!queueList.Any())
                {
                    sending = false;
                    return;
                }
                else
                {
                    buf = new List<KeyValuePair<string, ZabbixValue>>(queueList);
                    queueList.Clear();
                }
            }

            send(Host, buf);

            sending = false;
        }
        /// <summary>
        /// Сериализует и отправляет значения в сокет
        /// </summary>
        /// <param name="host">Имя хоста</param>
        /// <param name="values">Значения</param>
        static void send(string host, List<KeyValuePair<string, ZabbixValue>> values)
        {
            var vals = new List<ZabbixJsonValue>();
            try
            {
                foreach (var v in values)
                {
                    if (v.Value == null || v.Value.Value == null)
                        continue;

                    vals.Add(new ZabbixJsonValue(host, v.Key, v.Value.GetStringValue(nfi), v.Value.Timestamp));
                }
            }
            catch (Exception e)
            {
                //Utils.Trace(TraceMasks.Warning, $"ZabbixSender send(): {e.Message} {e.InnerException?.Message}");
                Diagnostics.WriteEvent($"ZabbixSender send(): {e.Message} {e.InnerException?.Message}", System.Diagnostics.EventLogEntryType.Error);
            }

            string json = string.Empty;

            // https://www.zabbix.com/documentation/current/manual/appendix/items/trapper
            try
            {
                json = JsonConvert.SerializeObject(new
                {
                    request = "sender data",
                    data = vals.ToArray()
                });
            }
            catch (Exception e)
            {
                //Utils.Trace(TraceMasks.Warning, $"ZabbixSender send() ошибка создания JSON: {e.Message} {e.InnerException?.Message}");
                Diagnostics.WriteEvent($"ZabbixSender send(): {e.Message} {e.InnerException?.Message}", System.Diagnostics.EventLogEntryType.Error);
                return;
            }

            byte[] header = Encoding.ASCII.GetBytes("ZBXD\x01");
            byte[] length = BitConverter.GetBytes((long)json.Length); // long для 4байт длинны и 4 пустых байта
            byte[] data = Encoding.ASCII.GetBytes(json);

            byte[] all = new byte[header.Length + length.Length + data.Length];

            // https://www.zabbix.com/documentation/current/manual/appendix/protocols/header_datalen
            Buffer.BlockCopy(header, 0, all, 0, header.Length);
            Buffer.BlockCopy(length, 0, all, header.Length, length.Length);
            Buffer.BlockCopy(data, 0, all, header.Length + length.Length, data.Length);

            try
            {
                using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    client.Connect(Ip, Port);
                    client.Send(all);

                    // Заголовок

                    byte[] buffer = new byte[5];
                    receive(client, buffer, 0, buffer.Length, 10000);

                    if ("ZBXD\x01" != Encoding.ASCII.GetString(buffer, 0, buffer.Length))
                    {
                        //Utils.Trace(TraceMasks.Warning, "Zabbix - Invalid response header");
                        client.Close();
                        return;
                    }

                    // Длинна сообщения

                    buffer = new byte[8];
                    receive(client, buffer, 0, buffer.Length, 10000);
                    int dataLength = BitConverter.ToInt32(buffer, 0);

                    if (dataLength == 0)
                    {
                        //Utils.Trace(TraceMasks.Warning, "Zabbix - Invalid response data length");
                        client.Close();
                        return;
                    }

                    // Сообщение

                    buffer = new byte[dataLength];
                    receive(client, buffer, 0, buffer.Length, 10000);
                    client.Close();

                    //Console.WriteLine(Encoding.ASCII.GetString(buffer, 0, buffer.Length));
                }
                commError = false;
            }
            catch (Exception e)
            {
                if (!commError)
                    //Utils.Trace(TraceMasks.Warning, $"ZabbixSender send(): {e.Message} {e.InnerException?.Message}");
                commError = true;
                Diagnostics.WriteEvent($"ZabbixSender send(): {e.Message} {e.InnerException?.Message}", System.Diagnostics.EventLogEntryType.Error);
            }
        }
        /// <summary>
        /// Принимает ответ из сокета
        /// </summary>
        /// <param name="socket">Сокет</param>
        /// <param name="buffer">Буфер</param>
        /// <param name="offset">Смещение</param>
        /// <param name="size">Размер</param>
        /// <param name="timeout">Таймаут</param>
        static void receive(Socket socket, byte[] buffer, int offset, int size, int timeout)
        {
            int startTickCount = Environment.TickCount;
            int received = 0;
            int retries = 0;
            do
            {
                if (Environment.TickCount > startTickCount + timeout)
                    throw new Exception("Recieve timeout");

                try
                {
                    received += socket.Receive(buffer, offset + received, size - received, SocketFlags.None);
                    commError = false;
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.WouldBlock || ex.SocketErrorCode == SocketError.IOPending || ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                    {
                        // socket buffer is probably empty, wait and try again
                        Thread.Sleep(30);
                    }
                    else
                    {
                        //if (!commError)
                            //Utils.Trace(TraceMasks.Warning, $"ZabbixSender receive(): {ex.Message} {ex.InnerException?.Message}");
                    }
                    commError = true;
                    retries++;
                }
            } while ((received < size) && (retries < 10));
        }
    }
    /// <summary>
    /// Класс сообщения для Zabbix
    /// </summary>
    public class ZabbixValue
    {
        /// <summary>
        /// Значние
        /// </summary>
        public object Value;
        /// <summary>
        /// Метка времени
        /// </summary>
        public DateTime Timestamp;
        /// <summary>
        /// Конструктор класса
        /// </summary>
        /// <param name="value">значние</param>
        public ZabbixValue(object value)
        {
            Value = value;
            Timestamp = DateTime.Now;
        }
        /// <summary>
        /// Конструктор класса
        /// </summary>
        /// <param name="value">значние</param>
        /// <param name="timestamp">метка времени</param>
        public ZabbixValue(object value, DateTime timestamp)
        {
            Value = value;
            Timestamp = timestamp;
        }
        /// <summary>
        /// Преобразователь типа
        /// </summary>
        /// <param name="nfi"></param>
        /// <returns></returns>
        public string GetStringValue(NumberFormatInfo nfi)
        {
            if (Value is bool)
                return ((bool)Value) ? "1" : "0";
            if (Value is int)
                return ((int)Value).ToString();
            if (Value is long)
                return ((long)Value).ToString();
            if (Value is double)
                return ((double)Value).ToString(nfi);
            if (Value is float)
                return ((float)Value).ToString(nfi);
            if (Value is decimal)
                return ((decimal)Value).ToString(nfi);
            else
                return Value.ToString();
        }
    }
    /// <summary>
    /// Класс сериализации сообщения json   
    /// </summary>
    [Serializable]
    public class ZabbixJsonValue
    {
        /// <summary>
        /// хост
        /// </summary>
        public string host;
        /// <summary>
        /// ключ
        /// </summary>
        public string key;
        /// <summary>
        /// значнеие
        /// </summary>
        public string value;
        /// <summary>
        /// Время с 1 янв. 1970 UTC
        /// </summary>
        public long clock;
        /// <summary>
        /// Наносекунды
        /// </summary>
        public long ns;
        /// <summary>
        /// Конструктор класса
        /// </summary>
        /// <param name="host">хост</param>
        /// <param name="key">ключ</param>
        /// <param name="value">значение</param>
        internal ZabbixJsonValue(string host, string key, string value)
        {
            this.host = host;
            this.key = key;
            this.value = value;

            var now = DateTime.Now;
            clock = Convert.ToInt64((now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            ns = Convert.ToInt64(now.Millisecond) * 1000000;
        }
        /// <summary>
        /// Конструктор класса
        /// </summary>
        /// <param name="host">хост</param>
        /// <param name="key">ключ</param>
        /// <param name="value">значение</param>
        /// <param name="timestamp">метка времени</param>
        internal ZabbixJsonValue(string host, string key, string value, DateTime timestamp)
        {
            this.host = host;
            this.key = key;
            this.value = value;

            clock = Convert.ToInt64((timestamp.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            ns = Convert.ToInt64(timestamp.Millisecond) * 1000000;
        }

    }
}
