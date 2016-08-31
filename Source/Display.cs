using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VersionDB
{
    public enum DisplayType
    {
        General,
        Error,
        Warning,
        Success,
        Info
    }

    public static class Display
    {
        public static void InitializeDisplay()
        {
            try
            {
                Console.SetWindowSize(Console.LargestWindowWidth - 10, Console.LargestWindowHeight - 10);
                Console.Title = "Database Versioning Console - " + Environment.CurrentDirectory;
            }
            catch { }

            ResetColor();
            Console.Clear();
        }

        public static void ResetColor()
        {
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
        }

        public static void SetColor(DisplayType displayType)
        {
            switch (displayType)
            {
                case DisplayType.Error:
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case DisplayType.Success:
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    break;
                case DisplayType.Warning:
                    Console.BackgroundColor = ConsoleColor.Yellow;
                    break;
                case DisplayType.Info:
                    Console.BackgroundColor = ConsoleColor.Gray;
                    break;
                default:
                    break;
            }
        }

        public static void DisplayMessage(DisplayType displayType, string message, params object[] parameters)
        {
            SetColor(displayType);

            if (parameters != null && parameters.Length > 0)
            {
                message = string.Format(message, parameters);
            }

            string displayMessage = message;

            if (displayType == DisplayType.Error && message.Length > 1000)
            {
                displayMessage = message.Substring(0, 1000) + "...";
                displayMessage = displayMessage + Environment.NewLine + "For full log see " + Logger.LogfileName;
            }

            Console.WriteLine(displayMessage);
            ResetColor();

            if (displayType == DisplayType.Error)
            {
                Logger.Log(message);
            }
        }
    }

    public class VersioningException : Exception
    {
        public VersioningException(string message) : base(message) { }
    }
}
