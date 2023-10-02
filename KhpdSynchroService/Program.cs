using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
namespace KhpdSynchroService
{
    class Program
    {
        /// <summary>
        /// Основная точка входа в приложение
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                // Объект сервиса
                SynchroService svc;

                // Определение аргумента командной строки
                string cmd = (args.Count() > 0) ? args[0] : string.Empty;

                // Имя сервиса (исполняемого файла)
                string name = Assembly.GetExecutingAssembly().Location;

                switch (cmd)
                {
                    case "install":
                        // Добавление сервиса
                        ManagedInstallerClass.InstallHelper(new string[] { name });
                        break;

                    case "delete":
                        // Удаление сервиса
                        ManagedInstallerClass.InstallHelper(new string[] { "/u", name });
                        break;

                    case "console":
                        //CopyFiles();

                        // Консольный вариант запуска
                        svc = new SynchroService();

                        svc.StartService();
                        Console.WriteLine("Служба запущена. Для завершения нажмите Enter");
                        Console.ReadLine();
                        // Останов сервиса
                        svc.StopService();
                        break;

                    default:
                        // Запуск в виде сервиса
                        svc = new SynchroService();

                        // Запуск сервиса
                        ServiceBase.Run(svc);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}






