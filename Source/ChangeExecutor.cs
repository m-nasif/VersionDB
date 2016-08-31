using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Common;
using System.Data;
using System.Data.SqlClient;

namespace VersionDB
{
    public static class ChangeExecutor
    {
        public static void ShowLastExecutedChanges(DatabaseGroup databaseGroup)
        {
            foreach (Database database in databaseGroup.Databases)
            {
                DatabaseVersion lastVersion = GetLastExecutedVersion(database);
                Display.DisplayMessage(DisplayType.Info, "DATABASE: {0}, LATEST RELEASE: {1}, LATEST CHANGE: {2}", database.Name, lastVersion.ReleaseVersion, lastVersion.ChangeVersion);
            }
        }

        public static void ExecuteChanges(DatabaseGroup databaseGroup)
        {
            ReleaseChanges latestReleaseChanges = ChangeReader.AllReleaseChanges.FirstOrDefault(x => x.IsLatestRelease);
            ExecuteChanges(databaseGroup, latestReleaseChanges.Name, latestReleaseChanges.LastChangeVersion, false);
        }

        public static void ExecuteChanges(DatabaseGroup databaseGroup, string releaseVersion)
        {
            ReleaseChanges releaseChanges = AsserValidReleaseVersion(releaseVersion);
            ExecuteChanges(databaseGroup, releaseVersion, releaseChanges.LastChangeVersion, false);
        }

        public static void ExecuteChanges(DatabaseGroup databaseGroup, string releaseVersion, int changeVersion, bool force)
        {
            ReleaseChanges releaseChanges = AsserValidReleaseVersion(releaseVersion);

            if (ChangeReader.AllReleaseChanges.First(x => x.Name == releaseVersion).Changes.Count(y => y.Version == changeVersion) == 0)
            {
                throw new VersioningException("Change version - \"" + changeVersion + "\" does not exist in the change xml file for Release version - \"" + releaseVersion + "\" .");
            }

            foreach (Database database in databaseGroup.Databases)
            {
                int lastExecutedCurrentChangeVersion = GetLastExecutedChangeVersion(database, releaseVersion);

                if (force)
                {
                    lastExecutedCurrentChangeVersion = changeVersion - 1;
                }
                else if (!IsVersionChainMaintained(database, releaseChanges, changeVersion, lastExecutedCurrentChangeVersion))
                {
                    continue;
                }

                SqlDatabaseManager databaseManager = new SqlDatabaseManager(database, true);
                SqlTransaction tx = databaseManager.Connection.BeginTransaction();

                Change executingChange = null;
                string executingSql = null;

                try
                {
                    List<int> executedVersions = new List<int>();

                    for (int version = lastExecutedCurrentChangeVersion + 1; version <= changeVersion; version++)
                    {
                        executingChange = releaseChanges.Changes.FirstOrDefault(x => x.Version == version);

                        foreach (var sql in executingChange.ChangeSqls)
                        {
                            executingSql = string.Empty;

                            if (!string.IsNullOrEmpty(sql.Path))
                            {
                                executingSql = System.IO.File.ReadAllText(System.IO.Path.Combine(Constants.CHANGE_SCRIPT_DIRECTORY, sql.Path));
                            }
                            else
                            {
                                executingSql = sql.Sql;
                            }

                            if (database.Replacements != null)
                            {
                                foreach (var replacement in database.Replacements)
                                {
                                    executingSql = executingSql.Replace(replacement.Text, replacement.ReplacementText);
                                }
                            }

                            databaseManager.ExecuteNonQuery(executingSql, tx);
                        }

                        if (!force)
                        {
                            databaseManager.ExecuteNonQuery(@"
			                INSERT INTO " + Constants.CHANGE_LOG_TABLE + @" (RELEASE_VERSION, CHANGE_VERSION, EXECUTION_TIME, EXECUTOR_NAME, EXECUTOR_IP, DESCRIPTION)
			                VALUES ('" + releaseVersion + "'," + version + " ,CURRENT_TIMESTAMP, SUSER_NAME(), CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(255)), '" + (releaseChanges.Changes.FirstOrDefault(x => x.Version == version).Description ?? "") + "');", tx);
                        }

                        executedVersions.Add(version);
                    }

                    tx.Commit();

                    Display.DisplayMessage(DisplayType.Success, "Successfulle executed change versions {0} in release {1} in database {2}", string.Join(", ", executedVersions), releaseVersion, database.Name);
                }
                catch (Exception ex)
                {
                    string additionaMessage = string.Empty;
                    string executingSqlText = string.Empty;

                    if (executingChange != null)
                    {
                        additionaMessage += "\nException in Change \"" + executingChange.Version + "\" (" + (executingChange.Description ?? "") + ").\n";
                    }

                    if (!string.IsNullOrEmpty(executingSql))
                    {
                        executingSqlText = "\nException while executing SQL:\n" + executingSql;
                    }

                    Display.DisplayMessage(DisplayType.Error, "Exception occured while executing changes in release \"{0}\" in database \"{1}\".{2}\nException {3}{4}", releaseVersion, database.Name, additionaMessage, ex, executingSqlText);
                    tx.Rollback();
                }
                finally
                {
                    databaseManager.CloseConnection();
                }
            }
        }

        public static bool IsVersionChainMaintained(Database database, ReleaseChanges releaseChanges, int changeVersion, int lastExecutedCurrentChangeVersion)
        {
            if (releaseChanges.Sequence > 1)
            {
                // Check if last release changes are there
                var previousReleaseVersion = ChangeReader.AllReleaseChanges.FirstOrDefault(x => x.Sequence == releaseChanges.Sequence - 1);
                int lastExecutedPreviousChangeVersion = GetLastExecutedChangeVersion(database, previousReleaseVersion.Name);

                if (lastExecutedPreviousChangeVersion == 0)
                {
                    Display.DisplayMessage(DisplayType.Warning, "Could not execute changes in {0} since previous release ({1}) changes were not executed in that database.", database.Name, previousReleaseVersion.Name);
                    return false;
                }

                if (lastExecutedPreviousChangeVersion < previousReleaseVersion.LastChangeVersion)
                {
                    Display.DisplayMessage(DisplayType.Warning, "Could not execute changes in {0} since latest changes in previous release ({1}) were not executed in that database.", database.Name, previousReleaseVersion.Name);
                    return false;
                }
            }

            if (lastExecutedCurrentChangeVersion >= changeVersion)
            {
                Display.DisplayMessage(DisplayType.Info, "Change {0} in release {1} was already executed in database {2}.", changeVersion, releaseChanges.Name, database.Name);
                return false;
            }

            return true;
        }

        public static ReleaseChanges AsserValidReleaseVersion(string releaseVersion)
        {
            ReleaseChanges releaseChanges = ChangeReader.AllReleaseChanges.FirstOrDefault(x => x.Name == releaseVersion);

            if (releaseChanges == null)
            {
                throw new VersioningException("Release version - \"" + releaseVersion + "\" does not exist in the change xml files.");
            }

            return releaseChanges;
        }

        public static DatabaseVersion GetLastExecutedVersion(Database database)
        {
            DatabaseVersion databaseVersion = new DatabaseVersion { ReleaseVersion = DatabaseVersion.NO_EXECUTED_RELEASE_VERSION };

            List<string> releaseVersions = new List<string>();

            SqlDatabaseManager databaseManager = new SqlDatabaseManager(database, true);
            IDataReader reader = databaseManager.ExecuteReader("SELECT DISTINCT RELEASE_VERSION FROM " + Constants.CHANGE_LOG_TABLE);

            while (reader.Read())
            {
                releaseVersions.Add((string)reader["RELEASE_VERSION"]);
            }

            reader.Close();

            if (releaseVersions.Count == 0)
            {
                databaseManager.CloseConnection();
                return databaseVersion;
            }

            var latestReleaseVerion = ChangeReader.AllReleaseChanges.Where(x => releaseVersions.Contains(x.Name)).OrderByDescending(y => y.Sequence).First();

            databaseVersion.ReleaseVersion = latestReleaseVerion.Name;

            reader = databaseManager.ExecuteReader(@"SELECT MAX(CHANGE_VERSION) AS CHANGE_VERSION FROM " + Constants.CHANGE_LOG_TABLE + " WHERE RELEASE_VERSION = '" + databaseVersion.ReleaseVersion + "';");

            while (reader.Read())
            {
                databaseVersion.ChangeVersion = (int)reader["CHANGE_VERSION"];
            }

            reader.Close();

            databaseManager.CloseConnection();
            return databaseVersion;
        }

        public static int GetLastExecutedChangeVersion(Database database, string releaseVersion)
        {
            int changeVersion = 0;

            SqlDatabaseManager databaseManager = new SqlDatabaseManager(database, true);
            IDataReader reader = databaseManager.ExecuteReader(@"SELECT COALESCE(MAX(CHANGE_VERSION), 0) AS CHANGE_VERSION FROM " + Constants.CHANGE_LOG_TABLE + " WHERE RELEASE_VERSION = '" + releaseVersion + "';");

            while (reader.Read())
            {
                changeVersion = (int)reader["CHANGE_VERSION"];
            }

            reader.Close();

            databaseManager.CloseConnection();
            return changeVersion;
        }
    }

    public class DatabaseVersion
    {
        public string ReleaseVersion { get; set; }
        public int ChangeVersion { get; set; }

        public static string NO_EXECUTED_RELEASE_VERSION = "NO RELEASE WAS EXECUTED";
    }
}
