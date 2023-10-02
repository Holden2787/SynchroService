using KhpdSynchroService.Conf;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService.DBO
{
    /// <summary>
    /// Базовый класс БД
    /// </summary>
    public abstract class DBBase
    {
        /// <summary>
        /// Соединение
        /// </summary>
        public SqlConnection Conn;
        /// <summary>
        /// транзакция
        /// </summary>
        public SqlTransaction Transaction;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="overloadedConnStr">строка соединения</param>
        public DBBase(string overloadedConnStr = "")  // Constructor function
        {
            string strconn;

            if (overloadedConnStr == "")
            {
                strconn = Configuration.Settings.SQLConnString; //Enter your SQL server instance name
            }
            else
            {
                strconn = overloadedConnStr;
            }

            Conn = new SqlConnection(strconn);
        }
        /// <summary>
        /// Открытие соединения с БД
        /// </summary>
        /// <returns></returns>
        public bool OpenConnection() // Open database Connection
        {
            var ErrorConnectionSQL = false;
            try
            {
                Conn.Close();
                Conn.Open();
            }
            catch (SqlException ex)
            {
                ErrorConnectionSQL = true;
                Diagnostics.WriteEvent($"SQL connection error {ex.Message} server:{Conn.DataSource}", System.Diagnostics.EventLogEntryType.Error);
                return ErrorConnectionSQL;
            }

            try
            {
                Transaction = Conn.BeginTransaction();
            }
            catch (SqlException ex)
            {
                ErrorConnectionSQL = true;
                Diagnostics.WriteEvent($"SQL Transaction error {ex.Message} server:{Conn.DataSource}", System.Diagnostics.EventLogEntryType.Error);
                return ErrorConnectionSQL;               
            }

            return ErrorConnectionSQL;
        }
        /// <summary>
        /// Закрытие соединения с БД
        /// </summary>
        /// <returns></returns>
        public void CloseConnection() // database connection close
        {
            Transaction.Commit();
            Conn.Close();
        }
        /// <summary>
        /// Ошибка соединения
        /// </summary>
        public void ErrorTransaction()
        {
            Transaction.Rollback();
            Conn.Close();
        }
    }
}
