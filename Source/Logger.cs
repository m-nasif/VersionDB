using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace VersionDB
{
    public class Logger
    {
        private static string logFileName;

        public static string LogfileName
        {
            get { return logFileName; }
        }

        public static void Initialize(string logFilePath)
        {
            if (!Directory.Exists(logFilePath))
                Directory.CreateDirectory(logFilePath);

            logFileName = Path.Combine(logFilePath, "Log_" + DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".txt");
        }

        public static void Log(Exception ex)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(logFileName, true))
                {
                    sw.WriteLine("====================================== Time: " + DateTime.Now + " ===========================================");
                    sw.WriteLine(ex);
                    sw.WriteLine();
                }
            }
            catch (Exception ex2) { }
        }

        public static void Log(string message)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(logFileName, true))
                {
                    sw.WriteLine("====================================== Time: " + DateTime.Now + " ===========================================");
                    sw.WriteLine(message);
                    sw.WriteLine();
                }
            }
            catch (Exception ex2) { }
        }
    }
}
