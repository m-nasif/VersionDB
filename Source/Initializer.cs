using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace VersionDB
{
    public class Initializer
    {
        public static void Initialize(DatabaseGroup databaseGroup)
        {
            foreach (Database database in databaseGroup.Databases)
            {
                try
                {
                    SqlDatabaseManager databaseManager = new SqlDatabaseManager(database, true);
                    databaseManager.ExecuteNonQuery(GetVersioningTableScript(), transaction: null);
                    databaseManager.CloseConnection();

                    Display.DisplayMessage(DisplayType.Success, "Log table created in database - {0}.", database.Name);
                }
                catch (Exception ex)
                {
                    Display.DisplayMessage(DisplayType.Error, "Could not create change log table in database - {0}. See logs for details.", database.Name);
                    Logger.Log(ex);
                }
            }
        }

        public static void LogChanges(DatabaseGroup databaseGroup, string toReleaseVersion, int toChangeVersion)
        {
            ReleaseChanges toReleaseChanges = ChangeExecutor.AsserValidReleaseVersion(toReleaseVersion);

            if (!toReleaseChanges.Changes.Select(x => x.Version).Contains(toChangeVersion))
            {
                throw new VersioningException("Change Version - " + toChangeVersion + " does not exist in Release version - " + toReleaseVersion + " in the change xml files.");
            }

            foreach (Database database in databaseGroup.Databases)
            {
                DatabaseVersion lastExecutedVersion = ChangeExecutor.GetLastExecutedVersion(database);
                if (lastExecutedVersion.ReleaseVersion != DatabaseVersion.NO_EXECUTED_RELEASE_VERSION)
                {
                    Display.DisplayMessage(DisplayType.Error, "Can not insert logs in database - {0} since some logs were already inserted in this database by the tool.", database.Name);
                    continue;
                }

                SqlDatabaseManager databaseManager = new SqlDatabaseManager(database, true);
                SqlTransaction tx = databaseManager.Connection.BeginTransaction();

                try
                {
                    for (int sequence = 1; sequence <= toReleaseChanges.Sequence; sequence++)
                    {
                        ReleaseChanges releaseChanges = ChangeReader.AllReleaseChanges.FirstOrDefault(x => x.Sequence == sequence);

                        int toChangeVersionToExecute = releaseChanges.LastChangeVersion;

                        if (releaseChanges.Sequence == toReleaseChanges.Sequence)
                        {
                            toChangeVersionToExecute = toChangeVersion;
                        }

                        for (int version = 1; version <= toChangeVersionToExecute; version++)
                        {
                            databaseManager.ExecuteNonQuery(@"
			                INSERT INTO " + Constants.CHANGE_LOG_TABLE + @" (RELEASE_VERSION, CHANGE_VERSION, EXECUTION_TIME, EXECUTOR_NAME, EXECUTOR_IP, DESCRIPTION)
			                VALUES ('" + releaseChanges.Name + "'," + version + " ,CURRENT_TIMESTAMP, SUSER_NAME(), CAST(CONNECTIONPROPERTY('client_net_address') AS VARCHAR(255)), '" + (releaseChanges.Changes.FirstOrDefault(x => x.Version == version).Description ?? "") + "');", tx);
                        }
                    }

                    tx.Commit();

                    Display.DisplayMessage(DisplayType.Success, "Successfully inserted logs in database - {0}.", database.Name);
                }
                catch (Exception ex)
                {
                    Display.DisplayMessage(DisplayType.Error, "Exception occured while inserting logs in database - {0}. Exception - {1}", database.Name, ex);
                    tx.Rollback();
                }
                finally
                {
                    databaseManager.CloseConnection();
                }
            }
        }

        private static string GetVersioningTableScript()
        {
            string tableCreateStatement = @"
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'" + Constants.CHANGE_LOG_TABLE + @"') AND type in (N'U'))
BEGIN
CREATE TABLE " + Constants.CHANGE_LOG_TABLE + @" (
	CHANGE_LOG_ID INT IDENTITY(1,1) NOT NULL,
	RELEASE_VERSION VARCHAR(50) NOT NULL,
    CHANGE_VERSION int NOT NULL,
	EXECUTION_TIME DATETIME NOT NULL,
	EXECUTOR_NAME VARCHAR(255) DEFAULT NULL,
	EXECUTOR_IP VARCHAR(255) DEFAULT NULL,
	DESCRIPTION VARCHAR(2048) DEFAULT NULL,
	CONSTRAINT PK_" + Constants.CHANGE_LOG_TABLE_WITHOUT_SCHEMA + @" PRIMARY KEY CLUSTERED (CHANGE_LOG_ID ASC)
	);
END;";

            return tableCreateStatement;
        }
    }
}
