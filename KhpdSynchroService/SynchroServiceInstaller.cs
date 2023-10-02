using KhpdSynchroService.Conf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace KhpdSynchroService
{
    /// <summary>
    /// Класс устоновщина
    /// </summary>
    [RunInstaller(true)]
    public class KhpdSynchroServiceInstaller : Installer
    {
        /// <summary>
        /// Конструктор класса
        /// </summary>
        public KhpdSynchroServiceInstaller()
        {
            ServiceInstaller installer = new ServiceInstaller();
            ServiceProcessInstaller installer2 = new ServiceProcessInstaller();
            installer.ServiceName = Configuration.Settings.ServiceName;
            installer.DisplayName = Configuration.Settings.DisplayName;
            installer.Description = Configuration.Settings.ServiceDescription;

            base.Installers.Add(installer);

            installer2.Account = ServiceAccount.LocalSystem;
            installer2.Password = null;
            installer2.Username = null;
            base.Installers.Add(installer2);
        }
    }
}
