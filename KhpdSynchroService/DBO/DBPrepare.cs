using KhpdSynchroService.Conf;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService.DBO
{
    /// <summary>
    /// Класс подготовик талицы с фиксацие данных о скопированных файлах
    /// </summary>
    public class DBPrepare : DBBase
    {
        /// <summary>
        /// Проверяем существует ли необходимая таблица. Создаем если её нет.
        /// </summary>
        /// <returns></returns>
        public static bool CheckDB()
        {
            DBPrepare dbo = new DBPrepare();
            
            return dbo.CheckTableAndType();
        }
        /// <summary>
        /// Проверка таблицы хранения данных о копировании (dbo.SSFR_Hist)
        /// </summary>
        /// <returns></returns>
        private bool CheckTableAndType()
        {
            string SqlTableToInsert = Configuration.Settings.SqlTableToInsert;
            string SqlTypeTableCreate = Configuration.Settings.SqlTypeTableCreate;

            using (SqlCommand cmd = new SqlCommand())
            {
                if(OpenConnection())
                    return false;                  

                cmd.Connection = Conn;
                cmd.Transaction = Transaction;
                cmd.CommandType = CommandType.Text;

                cmd.CommandText = @"USE [Khpd] 
                                        IF NOT EXISTS
                                        (
                                               SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                                               WHERE TABLE_NAME = @TableName 
                                        ) 
                                        BEGIN                                       
                                            CREATE TABLE [dbo].[" + SqlTableToInsert + @"]( 
                                                [Path] NVARCHAR(1000)
                                                ,[Time] datetime
                                                ,[HistTime] datetime
                                            ) ON[PRIMARY];     
                                        END                                      

                                        IF NOT EXISTS 
                                        (
                                                SELECT *
                                                FROM sys.objects
                                                WHERE name LIKE ('TT_" + SqlTypeTableCreate + @"%')
                                        ) 
                                        BEGIN 
                                        CREATE TYPE dbo." + SqlTypeTableCreate + @" AS TABLE(
                                                            [Path] NVARCHAR(1000)
                                                            ,[Time] datetime
                                                            ) END ";

                cmd.Parameters.Add("@TableName", SqlDbType.NVarChar).Value = SqlTableToInsert;


                //оставлю тут скрипт создания  триггера, вдруг у когото получться его иниализировать
                //cmd.CommandText = @"CREATE OR ALTER TRIGGER TimeTrigger 
                //                               ON khpd.dbo." + SqlTableToInsert + @" 
                //                               AFTER Insert 
                //                            AS 
                //                            BEGIN 

                //                                UPDATE SSFR_Hist 
                //                                SET HistTime = GETDATE() 
                //                                WHERE HistTime IS NULL 

                //                                DELETE SSFR_Hist 
                //                                WHERE HistTime<GETDATE() -MONTH(3) 
                //                            END ";
                try
                {
                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch (Exception e)
                {
                    Diagnostics.WriteEvent("SQL Execute error " + e.Message, System.Diagnostics.EventLogEntryType.Error);
                }
                finally
                {
                    CloseConnection();
                }

                return false;
            }
        }
    }
}
