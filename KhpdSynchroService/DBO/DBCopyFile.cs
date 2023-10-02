using KhpdSynchroService.Conf;
using KhpdSynchroService.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService.DBO
{
    /// <summary>
    /// Класс копирования файлов
    /// </summary>
    public class DBCopyFile : DBBase
    {
        /// <summary>
        /// Таблица источник
        /// </summary>
        string SqlTableToInsert;
        /// <summary>
        /// Тип таблицы источника
        /// </summary>
        string SqlTypeTableCreate;
        /// <summary>
        /// таймаут SQL запроса
        /// </summary>
        int TimeoutQuery;
        /// <summary>
        /// Ошибка
        /// </summary>
        bool Err;
        /// <summary>
        /// Конструктор класса
        /// </summary>
        public DBCopyFile()
        {
            //Проверяем существует ли необходимая таблица. Создаем если её нет.
            if(!Configuration.Settings.WithoutBD)
                Err = !DBPrepare.CheckDB();
            TimeoutQuery = Configuration.Settings.TimeoutQuery;
            SqlTableToInsert = Configuration.Settings.SqlTableToInsert;
            SqlTypeTableCreate = Configuration.Settings.SqlTypeTableCreate;
        }
        /// <summary>
        /// Ошибка
        /// </summary>
        /// <returns></returns>
        public bool isErrConnect()
        {
            return Err;
        }
        /// <summary>
        /// Поиск файлов
        /// </summary>
        /// <param name="copyFiles">список файлов</param>
        /// <returns>статус</returns>
        public bool SelectFiles(List<CopyFileInfo> copyFiles)
        {
            var err = false;

            using (SqlCommand cmd = new SqlCommand())
            {              
                if (OpenConnection())
                    return true;

                SqlDataReader conReader = null;

                cmd.Connection = Conn;
                cmd.Transaction = Transaction;
                cmd.CommandType = CommandType.Text;
                /*
                 сохряню и разверну запрос
                cmd.CommandText = @"select 
                                        [Path]
                                        ,[Time]
                                    FROM [KHPD].[dbo]." + SqlTypeTableCreate +
                                    @"where 
                                       WHERE EXISTS (SELECT [Path]
                                                            ,[Time]
                                                    FROM
                                                            @raramTable)";
                */

                cmd.CommandText = @"SELECT [Path]
                                            ,[Time] 
                                    FROM
                                            @paramTable as parTab
                                    WHERE EXISTS (  select 
                                                        [Path]
                                                        ,[Time]
                                                    FROM 
                                                        [KHPD].[dbo]." + SqlTableToInsert + @" as extTab 
                                                    WHERE 
                                                        extTab.[Time] > @extTime AND
                                                        parTab.[Path] = extTab.[Path] and DATEDIFF(millisecond, extTab.[Time], parTab.[Time]) < 10)";


                var minTimeFile = copyFiles.Min(t => t.DateTime).AddDays(-1);

                DataTable table = ToDataTable(copyFiles);

                SqlParameter tvpParam = cmd.Parameters.AddWithValue("@paramTable", table);
                tvpParam.SqlDbType = SqlDbType.Structured;
                tvpParam.TypeName = "dbo." + SqlTypeTableCreate;
                cmd.Parameters.AddWithValue("@extTime", minTimeFile);
                //cmd.Parameters.Add("@EmptyRow", SqlDbType.NVarChar).Value = "";
                cmd.CommandTimeout = TimeoutQuery;
                try
                {
                    conReader = cmd.ExecuteReader();
                    //int i = 1;

                    while (conReader.Read())
                    {
                        IEnumerable<CopyFileInfo> selector = copyFiles.Where(c => c.FilePath == conReader["Path"].ToString() && (c.DateTime - (DateTime)conReader["Time"]).TotalMilliseconds < 10);
                        if (selector.Count() > 0)
                        {
                            selector.First().NeedCopy = false;                          
                        }
                        //Diagnostics.WriteEvent("Файл попал в выборку" + i + " " + conReader["Path"].ToString(), System.Diagnostics.EventLogEntryType.Information);
                        //i++;
                    }
                }
                catch (Exception ex)
                {
                    Diagnostics.WriteEvent($"SQL select error.{ ex.Message}. {ex.StackTrace}", System.Diagnostics.EventLogEntryType.Error);
                    err = true;
                }
                finally
                {
                    if (conReader != null)
                    {
                        conReader.Close();
                    }
                    CloseConnection();                    
                }
            }

            return err;
        }
        /// <summary>
        /// выборка файла
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data">элемент данных</param>
        /// <returns></returns>
        private DataTable ToDataTable<T>(IList<T> data)
        {
            PropertyDescriptorCollection properties =
                TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            foreach (PropertyDescriptor prop in properties)
            {
                if (AtributeCondition(prop))
                {
                    continue;
                }
                table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            }
            foreach (T item in data)
            {
                DataRow row = table.NewRow();
                foreach (PropertyDescriptor prop in properties)
                {
                    if (AtributeCondition(prop))
                    {
                        continue;
                    }
                    row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                }
                table.Rows.Add(row);
            }
            return table;
        }
        /// <summary>
        /// Проверка атрибута
        /// </summary>
        /// <param name="prop">настройка</param>
        /// <returns>результат проверки</returns>
        private bool AtributeCondition(PropertyDescriptor prop)
        {
            return prop.Attributes.Contains(new AtributeNotMappedInType());
        }
        /// <summary>
        /// Записываем в таблицу
        /// </summary>
        /// <param name="copyFiles"></param>
        /// <returns></returns>
        public bool InsertData(List<CopyFileInfo> copyFiles)
        {
            if(copyFiles.Count == 0)
                return true;

            using (SqlCommand cmd = new SqlCommand())
            {
                if (OpenConnection())
                    return false;
                cmd.Connection = Conn;
                cmd.Transaction = Transaction;
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = @"INSERT INTO " + SqlTableToInsert + @" ([Path] 
                                                                                ,[Time]) 
                                                   SELECT [Path] 
                                                            ,[Time] 
                                                   FROM @TableData AS td";

                DataTable table = ToDataTable(copyFiles);

                SqlParameter tvpParam = cmd.Parameters.AddWithValue("@TableData", table);
                tvpParam.SqlDbType = SqlDbType.Structured;
                tvpParam.TypeName = "dbo." + SqlTypeTableCreate;
                //cmd.Parameters.Add("@EmptyRow", SqlDbType.NVarChar).Value = "";

                try
                {
                    cmd.ExecuteNonQuery();

                    foreach (var file in copyFiles)
                        file.NeedCopy = false;
                }
                catch (Exception ex)
                {
                    Diagnostics.WriteEvent("SQL insert error" + ex.Message, System.Diagnostics.EventLogEntryType.Error);
                    return false;
                }
                finally
                {
                    //conReader.Close();
                    CloseConnection();
                }
            }

            return true;

        }
    }
}
