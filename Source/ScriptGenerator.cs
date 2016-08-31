using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Smo.SqlEnum;
using System.Collections.Specialized;
using System.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;

namespace DatabaseScriptGenerator
{
    public class ScriptGenerator
    {
        private string connectionString = "";
        private string rootPath = @"D:\ScriptGen";
        private string initializationConfigPath = "Initialization.xml";

        private string relativeSchemasScriptPath = @"Scripts\Initialization\Schemas.sql";
        private string relativeTablesScriptPath = @"Scripts\Initialization\Tables.sql";
        private string relativeTablesFKScriptPath = @"Scripts\Initialization\ForeignKeys.sql";
        private string relativeViewsScriptPath = @"Scripts\Initialization\Views.sql";
        private string relativeUDTsScriptPath = @"Scripts\Initialization\UserDefinedTypes.sql";
        private string relativeStoredProceduresLocation = @"Procedures";
        private string relativeFunctionsLocation = @"Functions";
        private string relativeTriggersScriptPath = @"Scripts\Initialization\Triggers.sql";

        public string SchemasScriptPath { get { return System.IO.Path.Combine(rootPath, relativeSchemasScriptPath); } }
        public string TablesScriptPath { get { return System.IO.Path.Combine(rootPath, relativeTablesScriptPath); } }
        public string TablesFKScriptPath { get { return System.IO.Path.Combine(rootPath, relativeTablesFKScriptPath); } }
        public string ViewsScriptPath { get { return System.IO.Path.Combine(rootPath, relativeViewsScriptPath); } }
        public string UDTsScriptPath { get { return System.IO.Path.Combine(rootPath, relativeUDTsScriptPath); } }
        public string StoredProceduresLocation { get { return System.IO.Path.Combine(rootPath, relativeStoredProceduresLocation); } }
        public string FunctionsLocation { get { return System.IO.Path.Combine(rootPath, relativeFunctionsLocation); } }
        public string TriggersScriptPath { get { return System.IO.Path.Combine(rootPath, relativeTriggersScriptPath); } }

        public ScriptGenerator(string connectionString, string scriptRootPath)
        {
            this.connectionString = connectionString;
            this.rootPath = scriptRootPath;
        }

        public void GenerateScriptAndWriteXML()
        {
            GenerateScripts();
            GenerateConfig();
        }

        public void GenerateScripts()
        {
            DatabaseScripter scripter = new DatabaseScripter();
            scripter.CreateOnly = true;
            scripter.ScriptAsCreate = true;

            scripter.SchemasScriptPath = SchemasScriptPath;
            scripter.TablesScriptPath = TablesScriptPath;
            scripter.TablesFKScriptPath = TablesFKScriptPath;
            scripter.ViewsScriptPath = ViewsScriptPath;
            scripter.UDTsScriptPath = UDTsScriptPath;
            scripter.StoredProceduresLocation = StoredProceduresLocation;
            scripter.FunctionsLocation = FunctionsLocation;
            scripter.TriggersScriptPath = TriggersScriptPath;

            scripter.GenerateScripts(this.connectionString, true);
        }

        public void GenerateConfig()
        {
            int changeNumber = 1;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8"" ?>");
            sb.Append(@"<ReleaseChanges releaseName=""Initialization"">");

            if (System.IO.File.Exists(SchemasScriptPath))
            {
                sb.AppendFormat(@"

  <Change version=""{0}"" description=""Creating Schemas."">
    <Sql path=""{1}"" />
  </Change>", changeNumber++, relativeSchemasScriptPath);
            }

            if (System.IO.File.Exists(TablesScriptPath))
            {
                sb.AppendFormat(@"

  <Change version=""{0}"" description=""Creating Tables."">
    <Sql path=""{1}"" />
  </Change>", changeNumber++, relativeTablesScriptPath);
            }

            if (System.IO.File.Exists(TablesFKScriptPath))
            {
                sb.AppendFormat(@"

  <Change version=""{0}"" description=""Creating Foreign Keys."">
    <Sql path=""{1}"" />
  </Change>", changeNumber++, relativeTablesFKScriptPath);
            }

            if (System.IO.File.Exists(ViewsScriptPath))
            {
                sb.AppendFormat(@"

  <Change version=""{0}"" description=""Creating Views."">
    <Sql path=""{1}"" />
  </Change>", changeNumber++, relativeViewsScriptPath);
            }

            if (System.IO.File.Exists(UDTsScriptPath))
            {
                sb.AppendFormat(@"

  <Change version=""{0}"" description=""Creating User Defined Types."">
    <Sql path=""{1}"" />
  </Change>", changeNumber++, relativeUDTsScriptPath);
            }

            if (System.IO.Directory.Exists(StoredProceduresLocation))
            {
                StringBuilder procString = new StringBuilder("");
                System.IO.DirectoryInfo procsDirectory = new System.IO.DirectoryInfo(StoredProceduresLocation);

                foreach (var directory in procsDirectory.GetDirectories().OrderBy(x => x.Name))
                {
                    if (directory.GetFiles("Version 000.sql").Length > 0)
                    {
                        procString.AppendFormat(@"    <Sql path=""{0}\{1}\Version 000.sql"" />{2}", relativeStoredProceduresLocation, directory.Name, Environment.NewLine);
                    }
                }

                if (procString.Length > 0)
                {
                    sb.AppendFormat(@"

  <Change version=""{0}"" description=""Creating Stored Procedures."">
{1}  </Change>", changeNumber++, procString);
                }
            }

            if (System.IO.Directory.Exists(FunctionsLocation))
            {
                StringBuilder procString = new StringBuilder("");
                System.IO.DirectoryInfo procsDirectory = new System.IO.DirectoryInfo(FunctionsLocation);

                foreach (var directory in procsDirectory.GetDirectories().OrderBy(x => x.Name))
                {
                    if (directory.GetFiles("Version 000.sql").Length > 0)
                    {
                        procString.AppendFormat(@"    <Sql path=""{0}\{1}\Version 000.sql"" />{2}", relativeFunctionsLocation, directory.Name, Environment.NewLine);
                    }
                }

                if (procString.Length > 0)
                {
                    sb.AppendFormat(@"

  <Change version=""{0}"" description=""Creating Functions."">
{1}  </Change>", changeNumber++, procString);
                }
            }

            if (System.IO.File.Exists(TriggersScriptPath))
            {
                sb.AppendFormat(@"

  <Change version=""{0}"" description=""Creating Triggers."">
    <Sql path=""{1}"" />
  </Change>", changeNumber++, relativeTriggersScriptPath);
            }

            sb.AppendLine(@"

</ReleaseChanges>");

            System.IO.File.WriteAllText(System.IO.Path.Combine(rootPath, initializationConfigPath), sb.ToString());
        }
    }
}
