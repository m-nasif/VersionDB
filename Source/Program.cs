using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VersionDB
{
    class Program
    {
        static int LoadConfigurations()
        {
            try
            {
                ConfigReader.ReadConfig();
            }
            catch (Exception ex)
            {
                Display.DisplayMessage(DisplayType.Error, "Error reading configuration file. Exception:\n{0}", ex);
                Console.ReadLine();
                return -1;
            }

            Constants.CHANGE_SCRIPT_DIRECTORY = System.IO.Path.Combine(Constants.WORKING_DIR_ROOT, ConfigReader.Config.ChangeScriptDirectory.Path);
            Constants.CHANGE_LOG_TABLE = ConfigReader.Config.LogTable.SchemaName + "." + ConfigReader.Config.LogTable.TableName;
            Constants.CHANGE_LOG_TABLE_WITHOUT_SCHEMA = ConfigReader.Config.LogTable.TableName;

            Logger.Initialize(System.IO.Path.Combine(Constants.WORKING_DIR_ROOT, ConfigReader.Config.LogDirectory.Path));

            try
            {
                ChangeReader.ReadAllChanges();
            }
            catch (Exception ex)
            {
                Display.DisplayMessage(DisplayType.Error, "Error reading change xml scripts. Exception:\n{0}", ex);
                Console.ReadLine();
                return -1;
            }

            return 0;
        }

        static void Main(string[] args)
        {
            Display.InitializeDisplay();

            if (LoadConfigurations() == -1)
            {
                return;
            }

            bool isDefaultDatabaseSpecified = ConfigReader.Config.DatabaseGroups.Count(x => x.Name == "DEFAULT") == 1;

            if (isDefaultDatabaseSpecified && ChangeReader.AllReleaseChanges.Count > 0)
            {
                Display.DisplayMessage(DisplayType.General, "Press ENTER to execute latest changes (up to version \"{0}\") in the latest release script (\"{1}\") to \"DEFAULT\" database group.\nElse enter specific command. To exit type \"Q\". Type \"-help\" for help.",
                    ChangeReader.AllReleaseChanges.FirstOrDefault(x => x.IsLatestRelease).LastChangeVersion,
                    ChangeReader.AllReleaseChanges.FirstOrDefault(x => x.IsLatestRelease).Name);
            }
            else
            {
                Display.DisplayMessage(DisplayType.General, "Enter command. To exit type \"Q\". Type \"-help\" for help.");
            }

            CommandManager commandManager = new CommandManager();
            string command = commandManager.GetCommand();

            while (command != "Q")
            {
                try
                {
                    if (string.IsNullOrEmpty(command))
                    {
                        if (isDefaultDatabaseSpecified && ChangeReader.AllReleaseChanges.Count > 0)
                        {
                            ChangeExecutor.ExecuteChanges(ConfigReader.Config.DatabaseGroups.FirstOrDefault(x => x.Name == "DEFAULT"));
                        }
                        else
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Incorrect Command.");
                        }
                    }
                    else if (command.StartsWith("-init"))
                    {
                        string[] options = command.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        if (options.Length != 2)
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Incorrect command. Correct format is -init<space>{database_group}");
                        }
                        else if (ConfigReader.Config.DatabaseGroups.Count(x => x.Name == options[1]) == 0)
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Specified database group does not exist in the config file.");
                        }
                        else
                        {
                            Initializer.Initialize(ConfigReader.Config.DatabaseGroups.Where(x => x.Name == options[1]).FirstOrDefault());
                        }
                    }
                    else if (command.StartsWith("-generatescript"))
                    {
                        string[] options = command.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        if (options.Length != 2)
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Incorrect command. Correct format is -generatescript<space>{database_group}");
                        }
                        else if (ConfigReader.Config.DatabaseGroups.Count(x => x.Name == options[1]) == 0)
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Specified database group does not exist in the config file.");
                        }
                        else
                        {
                            DatabaseScriptGenerator.ScriptGenerator generator = new DatabaseScriptGenerator.ScriptGenerator(ConfigReader.Config.DatabaseGroups.FirstOrDefault(x => x.Name == options[1]).Databases[0].ConnectionString,
                                Constants.CHANGE_SCRIPT_DIRECTORY);
                            generator.GenerateScriptAndWriteXML();
                        }
                    }
                    else if (command.StartsWith("-status"))
                    {
                        string[] options = command.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        if (options.Length != 2)
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Incorrect command. Correct format is -status<space>{database_group}");
                        }
                        else if (ConfigReader.Config.DatabaseGroups.Count(x => x.Name == options[1]) == 0)
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Specified database group does not exist in the config file.");
                        }
                        else
                        {
                            ChangeExecutor.ShowLastExecutedChanges(ConfigReader.Config.DatabaseGroups.Where(x => x.Name == options[1]).FirstOrDefault());
                        }
                    }
                    else if (command.StartsWith("-log"))
                    {
                        string[] options = command.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        int changeVersion = -1;

                        if (options.Length != 4 || !Int32.TryParse(options[3], out changeVersion))
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Incorrect command. Correct format is -log<space>{database_group}<space>{release_version}<space>{change_version}");
                        }
                        else if (ConfigReader.Config.DatabaseGroups.Count(x => x.Name == options[1]) == 0)
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Specified database group does not exist in the config file.");
                        }
                        else
                        {
                            Initializer.LogChanges(ConfigReader.Config.DatabaseGroups.Where(x => x.Name == options[1]).FirstOrDefault(), options[2], changeVersion);
                        }
                    }
                    else if (command.StartsWith("-clear"))
                    {
                        Console.Clear();
                    }
                    else if (command.StartsWith("-reload"))
                    {
                        if (LoadConfigurations() == -1)
                        {
                            return;
                        }

                        isDefaultDatabaseSpecified = ConfigReader.Config.DatabaseGroups.Count(x => x.Name == "DEFAULT") == 1;

                        commandManager = new CommandManager();

                        Display.DisplayMessage(DisplayType.Info, "All Configurations have been reloaded.");
                    }
                    else if (command.StartsWith("-help"))
                    {
                        Display.DisplayMessage(DisplayType.Info, @"
>> {database_group}<space>{release_version}<space>{change_version}
Executes the changes up to {change_version} in {release_version} in the databases in {database_group}.
>> {database_group}<space>{release_version}
Executes all the latest changes in {release_version} in the databases in {database_group}.
>> {database_group}
Executes all the latest changes in the latest release version in the databases in {database_group}.
>> ENTER (Key press)
Executes all the latest changes in the latest release version in the databases in ""DEFAULT"" database_group.
>> {database_group}<space>{release_version}<space>{change_version}<space>-force
Forcibly executes (even if version chain is not maintained) the changes in {change_version} in the {release_version} in the databases in {database_group} without inserting the log.
>> -status<space>{database_group}    
Shows the current status of the databases in the specified {database_group}.
>> -init<space>{database_group}
Creates the log table in the databases in {database_group}.
>> -generatescript<space>{database_group}
Creates the exisitng table/SP/function scripts from the first database in the specified {database_group}. Should be used if the tool is intended to use from an existing database and to automatically generate the scripts for the first time.
>> -log<space>{database_group}<space>{release_version}<space>{change_version}
Inserts all the logs up to given {change_version} and {release_version} in the databases in {database_group}. It's helpful to insert logs in the existing databases for the first time without actually executing the changes.
>> -reload   
Reloads all the configurations (Config XML and Change XMLs).
>> -clear
Clears the screen
");
                    }
                    else
                    {
                        string[] parameters = command.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        DatabaseGroup databaseGroup = ConfigReader.Config.DatabaseGroups.FirstOrDefault(x => x.Name == parameters[0]);
                        int changeVersion = -1;

                        if (databaseGroup == null)
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Database Group - {0} does not exist.", parameters[0]);
                        }
                        else if (parameters.Length == 1)
                        {
                            ChangeExecutor.ExecuteChanges(databaseGroup);
                        }
                        else if (parameters.Length == 2)
                        {
                            ChangeExecutor.ExecuteChanges(databaseGroup, parameters[1]);
                        }
                        else if ((parameters.Length == 3 || (parameters.Length == 4 && parameters[3] == "-force")) && Int32.TryParse(parameters[2], out changeVersion))
                        {
                            ChangeExecutor.ExecuteChanges(databaseGroup, parameters[1], changeVersion, (parameters.Length == 4 && parameters[3] == "-force"));
                        }
                        else
                        {
                            Display.DisplayMessage(DisplayType.Warning, "Incorrect Command.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is VersioningException)
                    {
                        Display.DisplayMessage(DisplayType.Warning, ex.Message);
                    }
                    else
                    {
                        Display.DisplayMessage(DisplayType.Error, "The command was aborted due to exception - {0}", ex);
                    }
                }

                Display.DisplayMessage(DisplayType.General, "\n\nEnter command. To exit type \"Q\". Type \"-help\" for help.");
                command = commandManager.GetCommand();
            }
        }
    }

    public static class Constants
    {
        public static string WORKING_DIR_ROOT = Environment.CurrentDirectory;
        public static string CHANGE_LOG_TABLE = "dbo._DB_VERSIONING_CHANGE_LOG";
        public static string CHANGE_LOG_TABLE_WITHOUT_SCHEMA = "_DB_VERSIONING_CHANGE_LOG";
        public static string CHANGE_SCRIPT_DIRECTORY;
    }
}
