using KhpdSynchroService.ZabbixIntegration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KhpdSynchroService.Tools
{
    /// <summary>
    /// Класс отправки уведомлений в САУ СИБ (безопасники)
    /// </summary>
    public static class SsSender
    {
        public static NumberFormatInfo Nfi = new NumberFormatInfo();
        static Timer sendTimer;
        static bool sending;
        //static Dictionary<string, SsValue> queue;
        static List<KeyValuePair<string, SsValue>> queueList;
        public static DateTime D1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        static SsSender()
        {
            Nfi.NumberGroupSeparator = ".";
            queueList = new List<KeyValuePair<string, SsValue>>();
            sendTimer = new Timer(sendTimerTick, null, 1000, 1000);
        }

        public static void SendAsync(string key, int value, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, additionalInfo)));
            }
        }

        public static void SendAsync(string key, bool value, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, additionalInfo)));
            }
        }

        public static void SendAsync(string key, double value, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, additionalInfo)));
            }
        }

        public static void SendAsync(string key, long value, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, additionalInfo)));
            }
        }

        public static void SendAsync(string key, decimal value, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, additionalInfo)));
            }
        }

        public static void SendAsync(string key, string value, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, additionalInfo)));
            }
        }

        public static void SendAsync(string key, int value, DateTime timestamp, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, timestamp, additionalInfo)));
            }
        }

        public static void SendAsync(string key, long value, DateTime timestamp, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, timestamp, additionalInfo)));
            }
        }

        public static void SendAsync(string key, bool value, DateTime timestamp, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, timestamp, additionalInfo)));
            }
        }

        public static void SendAsync(string key, double value, DateTime timestamp, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, timestamp, additionalInfo)));
            }
        }

        public static void SendAsync(string key, decimal value, DateTime timestamp, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, timestamp, additionalInfo)));
            }
        }

        public static void SendAsync(string key, string value, DateTime timestamp, string additionalInfo = "")
        {
            lock (queueList)
            {
                queueList.Add(new KeyValuePair<string, SsValue>(key, new SsValue(value, timestamp, additionalInfo)));
            }
        }

        public static void SendAsync(Dictionary<string, SsValue> values)
        {
            lock (queueList)
            {
                foreach (var v in values)
                {
                    queueList.Add(new KeyValuePair<string, SsValue>(v.Key, v.Value));
                }
            }
        }

        /// <summary>
        /// Отправка событий по таймеру
        /// </summary>
        /// <param name="state"></param>
        static void sendTimerTick(object state)
        {
            if (sending)
                return;

            sending = true;

            List<KeyValuePair<string, SsValue>> buf;

            lock (queueList)
            {
                if (!queueList.Any())
                {
                    sending = false;
                    return;
                }
                else
                {
                    buf = new List<KeyValuePair<string, SsValue>>(queueList);
                    //queue = new Dictionary<string, SsValue>();
                    queueList.Clear();
                }
            }

            send(Properties.Settings.Default.ZabbixServer, buf); // host

            sending = false;
        }

        static void send(string host, List<KeyValuePair<string, SsValue>> values)
        {
            var vals = new List<SsJsonValue>();
            try
            {
                foreach (var v in values)
                {
                    if (v.Value == null || v.Value.Value == null)
                        continue;

                    vals.Add(new SsJsonValue(host, v.Key, v.Value.Value, v.Value.Timestamp, v.Value.AdditionalInfo));
                }
            }
            catch (Exception e)
            {
                Diagnostics.WriteEvent($"SSSender send(): {e.Message} {e.InnerException?.Message}", EventLogEntryType.Warning);
            }

            string json;

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
                Diagnostics.WriteEvent($"SSSender send() ошибка создания JSON: {e.Message} {e.InnerException?.Message}", EventLogEntryType.Warning);
                return;
            }

            byte[] header = Encoding.ASCII.GetBytes("SSD\x01");
            byte[] data = Encoding.UTF8.GetBytes(json);
            byte[] length = BitConverter.GetBytes((long)data.Length); // long для 4байт длинны и 4 пустых байта

            byte[] all = new byte[header.Length + length.Length + data.Length];

            // https://www.zabbix.com/documentation/current/manual/appendix/protocols/header_datalen
            Buffer.BlockCopy(header, 0, all, 0, header.Length);
            Buffer.BlockCopy(length, 0, all, header.Length, length.Length);
            Buffer.BlockCopy(data, 0, all, header.Length + length.Length, data.Length);

            try
            {
                using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    client.Connect(Properties.Settings.Default.SSServer, Properties.Settings.Default.SsPort);
                    client.Send(all);

                    // Заголовок

                    byte[] buffer = new byte[4];
                    receive(client, buffer, 0, buffer.Length, 10000);

                    if ("SSD\x01" != Encoding.ASCII.GetString(buffer, 0, buffer.Length))
                    {
                        Diagnostics.WriteEvent("Invalid response header", EventLogEntryType.Warning);
                        client.Close();
                        return;
                    }

                    // Длинна сообщения

                    buffer = new byte[8];
                    receive(client, buffer, 0, buffer.Length, 10000);
                    int dataLength = BitConverter.ToInt32(buffer, 0);

                    if (dataLength == 0)
                    {
                        Diagnostics.WriteEvent("Invalid response length", EventLogEntryType.Warning);
                        client.Close();
                    }

                    // Сообщение

                    buffer = new byte[dataLength];
                    receive(client, buffer, 0, buffer.Length, 10000);
                    client.Close();

                }
            }
            catch (Exception e)
            {
                Diagnostics.WriteEvent($"SSSender send(): {e.Message} {e.InnerException?.Message}", EventLogEntryType.Warning);
            }
        }

        static void receive(Socket socket, byte[] buffer, int offset, int size, int timeout)
        {
            int startTickCount = Environment.TickCount;
            int received = 0;
            do
            {
                if (Environment.TickCount > startTickCount + timeout)
                    throw new Exception("Recieve timeout");

                try
                {
                    received += socket.Receive(buffer, offset + received, size - received, SocketFlags.None);
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
                        Diagnostics.WriteEvent($"SSSender receive(): {ex.Message} {ex.InnerException?.Message}", EventLogEntryType.Warning);
                    }
                }
            } while (received < size);
        }
    }

    public class SsValue
    {
        public string Value;
        public DateTime Timestamp;
        public string AdditionalInfo;

        public SsValue(int value, string additionalInfo = "")
        {
            Value = value.ToString();
            Timestamp = DateTime.Now;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(bool value, string additionalInfo = "")
        {
            Value = value ? "1" : "0";
            Timestamp = DateTime.Now;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(long value, string additionalInfo = "")
        {
            Value = value.ToString();
            Timestamp = DateTime.Now;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(double value, string additionalInfo = "")
        {
            Value = value.ToString(SsSender.Nfi);
            Timestamp = DateTime.Now;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(float value, string additionalInfo = "")
        {
            Value = value.ToString(SsSender.Nfi);
            Timestamp = DateTime.Now;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(decimal value, string additionalInfo = "")
        {
            Value = value.ToString(SsSender.Nfi);
            Timestamp = DateTime.Now;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(string value, string additionalInfo = "")
        {
            Value = value;
            Timestamp = DateTime.Now;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(int value, DateTime timestamp, string additionalInfo = "")
        {
            Value = value.ToString();
            Timestamp = timestamp;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(bool value, DateTime timestamp, string additionalInfo = "")
        {
            Value = value ? "1" : "0";
            Timestamp = timestamp;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(long value, DateTime timestamp, string additionalInfo = "")
        {
            Value = value.ToString();
            Timestamp = timestamp;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(double value, DateTime timestamp, string additionalInfo = "")
        {
            Value = value.ToString(SsSender.Nfi);
            Timestamp = timestamp;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(float value, DateTime timestamp, string additionalInfo = "")
        {
            Value = value.ToString(SsSender.Nfi);
            Timestamp = timestamp;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(decimal value, DateTime timestamp, string additionalInfo = "")
        {
            Value = value.ToString(SsSender.Nfi);
            Timestamp = timestamp;
            AdditionalInfo = additionalInfo;
        }

        public SsValue(string value, DateTime timestamp, string additionalInfo = "")
        {
            Value = value;
            Timestamp = timestamp;
            AdditionalInfo = additionalInfo;
        }
    }

    [Serializable]
    public class SsJsonValue
    {
        public string host;
        public string key;
        public string value;
        public string additionalInfo;

        /// <summary>
        /// Время с 1 янв. 1970 UTC
        /// </summary>
        public long clock;
        /// <summary>
        /// Наносекунды
        /// </summary>
        public long ns;

        internal SsJsonValue(string host, string key, string value)
        {
            this.host = host;
            this.key = key;
            this.value = value;

            var now = DateTime.Now;
            clock = Convert.ToInt64((now.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            ns = Convert.ToInt64(now.Millisecond) * 1000000;
        }

        internal SsJsonValue(string host, string key, string value, DateTime timestamp, string additionalInfo)
        {
            this.host = host;
            this.key = key;
            this.value = value;
            this.additionalInfo = additionalInfo;

            clock = Convert.ToInt64((timestamp.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            ns = Convert.ToInt64(timestamp.Millisecond) * 1000000;
        }

    }
}
