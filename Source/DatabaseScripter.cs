/*
 * Copyright 2006 Jesse Hersch
 *
 * Permission to use, copy, modify, and distribute this software
 * and its documentation for any purpose is hereby granted without fee,
 * provided that the above copyright notice appears in all copies and that
 * both that copyright notice and this permission notice appear in
 * supporting documentation, and that the name of Jesse Hersch or
 * Elsasoft LLC not be used in advertising or publicity
 * pertaining to distribution of the software without specific, written
 * prior permission.  Jesse Hersch and Elsasoft LLC make no
 * representations about the suitability of this software for any
 * purpose.  It is provided "as is" without express or implied warranty.
 *
 * Jesse Hersch and Elsasoft LLC disclaim all warranties with
 * regard to this software, including all implied warranties of
 * merchantability and fitness, in no event shall Jesse Hersch or
 * Elsasoft LLC be liable for any special, indirect or
 * consequential damages or any damages whatsoever resulting from loss of
 * use, data or profits, whether in an action of contract, negligence or
 * other tortious action, arising out of or in connection with the use or
 * performance of this software.
 *
 * Author:
 *  Jesse Hersch
 *  Elsasoft LLC
 * 
*/

using System;
using System.Collections.Specialized;
using System.Data.SqlClient;
using System.IO;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;

namespace DatabaseScriptGenerator
{
    public class DatabaseScripter
    {
        #region Paths

        public string SchemasScriptPath { get; set; }
        public string TablesScriptPath { get; set; }
        public string TablesFKScriptPath { get; set; }
        public string ViewsScriptPath { get; set; }
        public string UDTsScriptPath { get; set; }
        public string StoredProceduresLocation { get; set; }
        public string FunctionsLocation { get; set; }
        public string TriggersScriptPath { get; set; }

        #endregion

        #region Private Variables

        private bool _ScriptAsCreate = false;
        private bool _Permissions = false;
        private bool _NoCollation = false;
        private bool _IncludeDatabase;
        private bool _CreateOnly = false;
        private string _OutputFileName = null;

        #endregion

        /// <summary>
        /// does all the work.
        /// </summary>
        /// <param name="connStr"></param>
        /// <param name="outputDirectory"></param>
        /// <param name="verbose"></param>
        public void GenerateScripts(string connStr, bool verbose)
        {
            SqlConnection connection = new SqlConnection(connStr);
            ServerConnection sc = new ServerConnection(connection);
            Server s = new Server(sc);

            s.SetDefaultInitFields(typeof(StoredProcedure), "IsSystemObject", "IsEncrypted");
            s.SetDefaultInitFields(typeof(Table), "IsSystemObject");
            s.SetDefaultInitFields(typeof(View), "IsSystemObject", "IsEncrypted");
            s.SetDefaultInitFields(typeof(UserDefinedFunction), "IsSystemObject", "IsEncrypted");
            s.ConnectionContext.SqlExecutionModes = SqlExecutionModes.CaptureSql;

            ScriptingOptions so = new ScriptingOptions();
            so.Default = true;
            so.DriDefaults = true;
            so.DriUniqueKeys = true;
            so.Bindings = true;
            so.Permissions = _Permissions;
            so.NoCollation = _NoCollation;
            so.IncludeDatabaseContext = _IncludeDatabase;

            Database db = s.Databases[connection.Database];

            ScriptTables(verbose, db, so);
            ScriptUddts(verbose, db, so);
            ScriptUdfs(verbose, db, so);
            ScriptViews(verbose, db, so);
            ScriptSprocs(verbose, db, so);
            ScriptUdts(verbose, db, so);
            ScriptUdtts(verbose, db, so);
            ScriptSchemas(verbose, db, so);
            ScriptDdlTriggers(verbose, db, so);
        }

        #region Private Script Functions

        private void ScriptTables(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (Table table in db.Tables)
            {
                if (!table.IsSystemObject)
                {
                    #region Table Definition

                    using (StreamWriter sw = GetStreamWriter(TablesScriptPath, true))
                    {
                        if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, table.Name);
                        if (!_CreateOnly)
                        {
                            so.ScriptDrops = so.IncludeIfNotExists = true;
                            WriteScript(table.Script(so), sw);
                        }
                        so.ScriptDrops = so.IncludeIfNotExists = false;
                        WriteScript(table.Script(so), sw);
                    }

                    #endregion

                    #region Triggers

                    foreach (Trigger smo in table.Triggers)
                    {
                        if (!smo.IsSystemObject && !smo.IsEncrypted)
                        {
                            using (StreamWriter sw = GetStreamWriter(TriggersScriptPath, true))
                            {
                                if (verbose) Console.WriteLine("{0] Scripting {1}.{2}", db.Name, table.Name, smo.Name);
                                if (!_CreateOnly)
                                {
                                    so.ScriptDrops = so.IncludeIfNotExists = true;
                                    WriteScript(smo.Script(so), sw);
                                }
                                so.ScriptDrops = so.IncludeIfNotExists = false;
                                WriteScript(smo.Script(so), sw);
                            }
                        }
                    }

                    #endregion

                    #region Indexes

                    foreach (Index smo in table.Indexes)
                    {
                        if (!smo.IsSystemObject)
                        {
                            using (StreamWriter sw = GetStreamWriter(TablesScriptPath, true))
                            {
                                if (verbose) Console.WriteLine("{0} Scripting {1}.{2}", db.Name, table.Name, smo.Name);
                                if (!_CreateOnly)
                                {
                                    so.ScriptDrops = so.IncludeIfNotExists = true;
                                    WriteScript(smo.Script(so), sw);
                                }
                                so.ScriptDrops = so.IncludeIfNotExists = false;
                                WriteScript(smo.Script(so), sw);
                            }
                        }
                    }

                    #endregion

                    #region Foreign Keys

                    foreach (ForeignKey smo in table.ForeignKeys)
                    {
                        using (StreamWriter sw = GetStreamWriter(TablesFKScriptPath, true))
                        {
                            if (verbose) Console.WriteLine("{0} Scripting {1}.{2}", db.Name, table.Name, smo.Name);
                            if (!_CreateOnly)
                            {
                                so.ScriptDrops = so.IncludeIfNotExists = true;
                            }
                            WriteScript(smo.Script(), sw);
                        }
                    }

                    #endregion
                }
                else
                {
                    if (verbose) Console.WriteLine("skipping system object {0}", table.Name);
                }
            }
        }

        private void ScriptSprocs(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (StoredProcedure smo in db.StoredProcedures)
            {
                if (!smo.IsSystemObject && !smo.IsEncrypted)
                {
                    string folderName = Path.Combine(StoredProceduresLocation, smo.Schema + "_" + FixUpFileName(smo.Name));
                    if (!Directory.Exists(folderName)) Directory.CreateDirectory(folderName);

                    using (StreamWriter sw = GetStreamWriter(Path.Combine(folderName, "Version 000.sql"), false))
                    {
                        if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, smo.Name);
                        if (_ScriptAsCreate)
                        {
                            so.ScriptDrops = so.IncludeIfNotExists = true;
                            WriteScript(smo.Script(so), sw);
                        }
                        so.ScriptDrops = so.IncludeIfNotExists = false;

                        if (_ScriptAsCreate)
                        {
                            WriteScript(smo.Script(so), sw);
                        }
                        else
                        {
                            WriteScript(smo.Script(so), sw, "CREATE PROC", "ALTER PROC");
                        }
                    }
                }
                else
                {
                    if (verbose) Console.WriteLine("skipping system object {0}", smo.Name);
                }
            }
        }

        private void ScriptViews(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (View smo in db.Views)
            {
                if (!smo.IsSystemObject && !smo.IsEncrypted)
                {
                    using (StreamWriter sw = GetStreamWriter(Path.Combine(ViewsScriptPath, FixUpFileName(smo.Name) + ".sql"), true))
                    {
                        if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, smo.Name);
                        if (!_CreateOnly)
                        {
                            so.ScriptDrops = so.IncludeIfNotExists = true;
                            WriteScript(smo.Script(so), sw);
                        }
                        so.ScriptDrops = so.IncludeIfNotExists = false;
                        WriteScript(smo.Script(so), sw);
                    }
                }
                else
                {
                    if (verbose) Console.WriteLine("skipping system object {0}", smo.Name);
                }
            }
        }

        private void ScriptUdfs(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (UserDefinedFunction smo in db.UserDefinedFunctions)
            {
                if (!smo.IsSystemObject && !smo.IsEncrypted)
                {
                    string folderName = Path.Combine(FunctionsLocation, smo.Schema + "_" + FixUpFileName(smo.Name));
                    if (!Directory.Exists(folderName)) Directory.CreateDirectory(folderName);

                    using (StreamWriter sw = GetStreamWriter(Path.Combine(folderName, "Version 000.sql"), false))
                    {
                        if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, smo.Name);
                        if (!_CreateOnly)
                        {
                            so.ScriptDrops = so.IncludeIfNotExists = true;
                            WriteScript(smo.Script(so), sw);
                        }
                        so.ScriptDrops = so.IncludeIfNotExists = false;
                        WriteScript(smo.Script(so), sw);
                    }
                }
                else
                {
                    if (verbose) Console.WriteLine("skipping system object {0}", smo.Name);
                }
            }
        }

        private void ScriptUdts(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (UserDefinedType smo in db.UserDefinedTypes)
            {
                using (StreamWriter sw = GetStreamWriter(UDTsScriptPath, true))
                {
                    if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, smo.Name);
                    if (!_CreateOnly)
                    {
                        so.ScriptDrops = so.IncludeIfNotExists = true;
                        WriteScript(smo.Script(so), sw);
                    }
                    so.ScriptDrops = so.IncludeIfNotExists = false;
                    WriteScript(smo.Script(so), sw);
                }
            }
        }

        private void ScriptUdtts(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (UserDefinedTableType smo in db.UserDefinedTableTypes)
            {
                using (StreamWriter sw = GetStreamWriter(UDTsScriptPath, true))
                {
                    if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, smo.Name);
                    if (!_CreateOnly)
                    {
                        so.ScriptDrops = so.IncludeIfNotExists = true;
                        WriteScript(smo.Script(so), sw);
                    }
                    so.ScriptDrops = so.IncludeIfNotExists = false;
                    WriteScript(smo.Script(so), sw);
                }
            }
        }

        private void ScriptUddts(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (UserDefinedDataType smo in db.UserDefinedDataTypes)
            {
                using (StreamWriter sw = GetStreamWriter(UDTsScriptPath, true))
                {
                    if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, smo.Name);
                    if (!_CreateOnly)
                    {
                        so.ScriptDrops = so.IncludeIfNotExists = true;
                        WriteScript(smo.Script(so), sw);
                    }
                    so.ScriptDrops = so.IncludeIfNotExists = false;
                    WriteScript(smo.Script(so), sw);
                }
            }
        }

        private void ScriptDdlTriggers(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (DatabaseDdlTrigger smo in db.Triggers)
            {
                using (StreamWriter sw = GetStreamWriter(TriggersScriptPath, true))
                {
                    if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, smo.Name);
                    if (!_CreateOnly)
                    {
                        so.ScriptDrops = so.IncludeIfNotExists = true;
                        WriteScript(smo.Script(so), sw);
                    }
                    so.ScriptDrops = so.IncludeIfNotExists = false;
                    WriteScript(smo.Script(so), sw);
                }
            }
        }

        private void ScriptSchemas(bool verbose, Database db, ScriptingOptions so)
        {
            foreach (Schema smo in db.Schemas)
            {
                // IsSystemObject doesn't exist for schemas.  Bad Cip!!!
                if (smo.Name == "sys" ||
                    smo.Name == "dbo" ||
                    smo.Name == "db_accessadmin" ||
                    smo.Name == "db_backupoperator" ||
                    smo.Name == "db_datareader" ||
                    smo.Name == "db_datawriter" ||
                    smo.Name == "db_ddladmin" ||
                    smo.Name == "db_denydatawriter" ||
                    smo.Name == "db_denydatareader" ||
                    smo.Name == "db_owner" ||
                    smo.Name == "db_securityadmin" ||
                    smo.Name == "INFORMATION_SCHEMA" ||
                    smo.Name == "guest") continue;

                using (StreamWriter sw = GetStreamWriter(SchemasScriptPath, true))
                {
                    if (verbose) Console.WriteLine("{0} Scripting {1}", db.Name, smo.Name);
                    if (!_CreateOnly)
                    {
                        so.ScriptDrops = so.IncludeIfNotExists = true;
                        WriteScript(smo.Script(so), sw);
                    }
                    so.ScriptDrops = so.IncludeIfNotExists = false;
                    WriteScript(smo.Script(so), sw);
                }
            }
        }

        #endregion

        #region Private Utility Functions

        private void WriteScript(StringCollection script, StreamWriter sw, string replaceMe, string replaceWith)
        {
            foreach (string ss in script)
            {
                if (ss == "SET QUOTED_IDENTIFIER ON" ||
                    ss == "SET QUOTED_IDENTIFIER OFF" ||
                    ss == "SET ANSI_NULLS ON" ||
                    ss == "SET ANSI_NULLS OFF")
                {
                    continue;
                }

                string sss = ReplaceEx(ss, replaceMe, replaceWith);
                sw.WriteLine(sss);
                sw.WriteLine("GO\r\n");
            }
        }

        private void WriteScript(StringCollection script, StreamWriter sw)
        {
            foreach (string ss in script)
            {
                if (ss == "SET QUOTED_IDENTIFIER ON" ||
                    ss == "SET QUOTED_IDENTIFIER OFF" ||
                    ss == "SET ANSI_NULLS ON" ||
                    ss == "SET ANSI_NULLS OFF")
                {
                    continue;
                }

                sw.WriteLine(ss);
                sw.WriteLine("GO\r\n");
            }
        }

        /// <summary>
        /// for case-insensitive string replace.  from www.codeproject.com
        /// </summary>
        /// <param name="original"></param>
        /// <param name="pattern"></param>
        /// <param name="replacement"></param>
        /// <returns></returns>
        private string ReplaceEx(string original, string pattern, string replacement)
        {
            int count, position0, position1;
            count = position0 = position1 = 0;
            string upperString = original.ToUpper();
            string upperPattern = pattern.ToUpper();
            int inc = (original.Length / pattern.Length) * (replacement.Length - pattern.Length);
            char[] chars = new char[original.Length + Math.Max(0, inc)];
            while ((position1 = upperString.IndexOf(upperPattern, position0)) != -1)
            {
                for (int i = position0; i < position1; ++i) chars[count++] = original[i];
                for (int i = 0; i < replacement.Length; ++i) chars[count++] = replacement[i];
                position0 = position1 + pattern.Length;
            }
            if (position0 == 0) return original;
            for (int i = position0; i < original.Length; ++i) chars[count++] = original[i];
            return new string(chars, 0, count);
        }

        private string FixUpFileName(string name)
        {
            return name
                .Replace("[", ".")
                .Replace("]", ".")
                .Replace(" ", ".")
                .Replace("&", ".")
                .Replace("'", ".")
                .Replace("\"", ".")
                .Replace(">", ".")
                .Replace("<", ".")
                .Replace("!", ".")
                .Replace("@", ".")
                .Replace("#", ".")
                .Replace("$", ".")
                .Replace("%", ".")
                .Replace("^", ".")
                .Replace("*", ".")
                .Replace("(", ".")
                .Replace(")", ".")
                .Replace("+", ".")
                .Replace("{", ".")
                .Replace("}", ".")
                .Replace("|", ".")
                .Replace("\\", ".")
                .Replace("?", ".")
                .Replace(",", ".")
                .Replace("/", ".")
                .Replace(";", ".")
                .Replace(":", ".")
                .Replace("-", ".")
                .Replace("=", ".")
                .Replace("`", ".")
                .Replace("~", ".");
        }

        /// <summary>
        /// THIS FUNCTION HAS A SIDEEFFECT.
        /// If OutputFileName is set, it will always open the filename
        /// </summary>
        /// <param name="Path"></param>
        /// <param name="Append"></param>
        /// <returns></returns>
        private StreamWriter GetStreamWriter(string Path, bool Append)
        {
            if (_OutputFileName != null)
            {
                Path = OutputFileName;
                Append = true;
            }
            if (OutputFileName == "-")
                return new StreamWriter(System.Console.OpenStandardOutput());

            if (!Directory.Exists(System.IO.Path.GetDirectoryName(Path))) Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            return new StreamWriter(Path, Append);
        }

        #endregion

        #region Public Properties

        public bool ScriptAsCreate
        {
            get { return _ScriptAsCreate; }
            set { _ScriptAsCreate = value; }
        }

        public bool Permissions
        {
            get { return _Permissions; }
            set { _Permissions = value; }
        }

        public bool NoCollation
        {
            get { return _NoCollation; }
            set { _NoCollation = value; }
        }

        public bool CreateOnly
        {
            get { return _CreateOnly; }
            set { _CreateOnly = value; }
        }

        public string OutputFileName
        {
            get { return _OutputFileName; }
            set { _OutputFileName = value; }
        }

        public bool IncludeDatabase
        {
            get { return _IncludeDatabase; }
            set { _IncludeDatabase = value; }
        }

        #endregion
    }
}