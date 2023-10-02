using KhpdSynchroService.Tools;
using System;


namespace KhpdSynchroService.Conf
{
    /// <summary>
    /// Отвечает за загрузку и хранение настроек программы
    /// </summary>
    public static class Configuration
    {
        static Settings settings;
        public static string BaseDirectory;

        static Configuration()
        {
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;           
        }

        /// <summary>
        /// Настройки программы
        /// </summary>
        public static Settings Settings
        {
            get
            {
                if (settings == null)
                {
                    if (string.IsNullOrEmpty(BaseDirectory))
                        BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    settings = Serializator.LoadXml<Settings>(BaseDirectory + "\\Settings.xml");
                }

                return settings;
            }
            set
            {
                if (value == null)
                    return;

                settings = value;

                if (string.IsNullOrEmpty(BaseDirectory))
                    BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

                Serializator.SaveXml<Settings>(settings, BaseDirectory + "\\Settings\\Settings.xml");
            }
        }        
    }    
}
