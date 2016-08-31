using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VersionDB
{
    public class CommandManager
    {
        private List<Command> Commands;
        private List<string> CommandHistory = new List<string>();
        private string CurrentCommand = string.Empty;

        public enum CommandType
        {
            SystemCommand,
            DatabaseGroup,
            ChangeVersion,
            ChangeNumber
        }

        public CommandManager()
        {
            PrepareCommandHierarchy();
        }

        public string GetCommand()
        {
            int HistoryPosition = -1;
            int TabItemPosition = -1;
            bool TabHit = false;
            bool ConsecutiveTabHit = false;
            List<string> eligibleCommands = new List<string>();
            string autoCompletePrefix = string.Empty;

            CurrentCommand = string.Empty;

            while (true)
            {
                var key = Console.ReadKey(true);

                ConsecutiveTabHit = (key.Key == ConsoleKey.Tab && TabHit);
                TabHit = (key.Key == ConsoleKey.Tab);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    if (!string.IsNullOrEmpty(CurrentCommand) && !CommandHistory.Contains(CurrentCommand))
                    {
                        CommandHistory.Add(CurrentCommand);
                    }
                    return CurrentCommand;
                }
                else if (key.Key == ConsoleKey.Tab)
                {
                    if (ConsecutiveTabHit)
                    {
                        if (eligibleCommands.Count > 0)
                        {
                            if (TabItemPosition == -1
                                || (key.Modifiers != ConsoleModifiers.Shift && TabItemPosition >= eligibleCommands.Count - 1)
                                || (key.Modifiers == ConsoleModifiers.Shift && TabItemPosition == 0))
                            {
                                TabItemPosition = key.Modifiers == ConsoleModifiers.Shift ? eligibleCommands.Count - 1 : 0;
                            }
                            else
                            {
                                TabItemPosition = key.Modifiers == ConsoleModifiers.Shift ? TabItemPosition - 1 : TabItemPosition + 1;
                            }
                        }
                    }
                    else
                    {
                        eligibleCommands = new List<string>();
                        string[] commandParts = CurrentCommand.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                        bool filterSuggestion = CurrentCommand != string.Empty && CurrentCommand[CurrentCommand.Length - 1] != ' ';
                        string filterText = filterSuggestion ? commandParts[commandParts.Length - 1] : string.Empty;
                        int position = commandParts.Length + (filterSuggestion ? 0 : 1);

                        if (position == 1)
                        {
                            eligibleCommands = Commands.Where(x => x.Level == 1 && x.Text.StartsWith(filterText)).Select(x => x.Text).ToList();
                        }
                        else if (position == 2)
                        {
                            Command level1Command = Commands.FirstOrDefault(x => x.Level == 1 && x.Text == commandParts[0]);
                            if (level1Command != null)
                            {
                                eligibleCommands = level1Command.SubCommands.Where(x => x.Text.StartsWith(filterText)).Select(x => x.Text).ToList();
                            }
                        }
                        else if (position == 3)
                        {
                            Command level1Command = Commands.FirstOrDefault(x => x.Level == 1 && x.Text == commandParts[0]);
                            if (level1Command != null)
                            {
                                Command level2Command = level1Command.SubCommands.FirstOrDefault(x => x.Text == commandParts[1]);

                                if (level2Command != null)
                                {
                                    eligibleCommands = level2Command.SubCommands.Where(x => x.Text.StartsWith(filterText)).Select(x => x.Text).ToList();
                                }
                            }
                        }

                        TabItemPosition = (key.Modifiers != ConsoleModifiers.Shift || eligibleCommands.Count < 2) ? 0 : eligibleCommands.Count - 1;
                        autoCompletePrefix = string.IsNullOrEmpty(CurrentCommand) ? string.Empty : CurrentCommand.Substring(0, CurrentCommand.LastIndexOf(' ') + 1);
                    }

                    if (eligibleCommands.Count > 0)
                    {
                        CurrentCommand = autoCompletePrefix + eligibleCommands[TabItemPosition];
                        ResetCommand();
                    }
                }
                else if (key.Modifiers == ConsoleModifiers.Control)
                {
                    continue;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    BackSpace();
                }
                else if (key.Key == ConsoleKey.UpArrow && CommandHistory.Count > 0)
                {
                    if (HistoryPosition == -1 || HistoryPosition == 0)
                    {
                        HistoryPosition = CommandHistory.Count - 1;
                    }
                    else
                    {
                        HistoryPosition = HistoryPosition - 1;
                    }

                    CurrentCommand = CommandHistory[HistoryPosition];

                    ResetCommand();
                }
                else if (key.Key == ConsoleKey.DownArrow && CommandHistory.Count > 0)
                {
                    if (HistoryPosition == -1 || HistoryPosition == CommandHistory.Count - 1)
                    {
                        HistoryPosition = 0;
                    }
                    else
                    {
                        HistoryPosition = HistoryPosition + 1;
                    }

                    CurrentCommand = CommandHistory[HistoryPosition];

                    ResetCommand();
                }
                else if ((key.KeyChar >= ' ' && key.KeyChar <= '~'))
                {
                    if (key.KeyChar != ' ' || (CurrentCommand != string.Empty && CurrentCommand[CurrentCommand.Length - 1] != ' '))
                    {
                        Console.Write(key.KeyChar);
                        CurrentCommand += key.KeyChar;
                    }
                }
            }
        }

        private void BackSpace()
        {
            if (Console.CursorLeft > 0)
            {
                Console.Write("\b \b");
                CurrentCommand = CurrentCommand.Substring(0, CurrentCommand.Length - 1);
            }
        }

        private void ResetCommand()
        {
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
            Console.Write(CurrentCommand);
        }

        private void PrepareCommandHierarchy()
        {
            Commands = new List<Command>();

            // Create Commands for each database group
            Command statusCommand = new Command { Level = 1, Text = "-status" };
            Command initCommand = new Command { Level = 1, Text = "-init" };
            Command scriptGenerateCommand = new Command { Level = 1, Text = "-generatescript" };
            Command logCommand = new Command { Level = 1, Text = "-log" };

            foreach (var dbGroup in ConfigReader.Config.DatabaseGroups)
            {
                Command dbCommand = new Command { Level = 1, Text = dbGroup.Name };

                foreach (ReleaseChanges releaseChange in ChangeReader.AllReleaseChanges.OrderByDescending(x => x.Sequence))
                {
                    Command releaseCommand = new Command { Level = 2, Text = releaseChange.Name };

                    foreach (Change change in releaseChange.Changes.OrderByDescending(x => x.Version))
                    {
                        Command changeCommand = new Command { Level = 3, Text = change.Version.ToString() };
                        releaseCommand.SubCommands.Add(changeCommand);
                    }

                    dbCommand.SubCommands.Add(releaseCommand);
                }

                Commands.Add(dbCommand);

                Command subCommand = new Command { Level = 2, Text = dbGroup.Name };
                statusCommand.SubCommands.Add(subCommand);
                initCommand.SubCommands.Add(subCommand);
                scriptGenerateCommand.SubCommands.Add(subCommand);
                logCommand.SubCommands.Add(subCommand);
            }

            Commands.Add(statusCommand);
            Commands.Add(initCommand);
            Commands.Add(scriptGenerateCommand);
            Commands.Add(logCommand);
            Commands.Add(new Command { Level = 1, Text = "-clear" });
            Commands.Add(new Command { Level = 1, Text = "-reload" });
            Commands.Add(new Command { Level = 1, Text = "-help" });
        }
    }

    public class Command
    {
        public int Level { get; set; }
        public string Text { get; set; }
        public List<Command> SubCommands { get; set; }

        public Command()
        {
            SubCommands = new List<Command>();
        }
    }
}
