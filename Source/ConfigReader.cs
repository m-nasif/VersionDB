using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.IO;

namespace VersionDB
{
    public class ConfigReader
    {
        public static Config Config { get; set; }

        private static string ConfigFilePath = System.IO.Path.Combine(Constants.WORKING_DIR_ROOT, @"Config.xml");

        public static void ReadConfig()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Config));

            StreamReader reader = new StreamReader(ConfigFilePath);
            Config = (Config)serializer.Deserialize(reader);
            reader.Close();

            if (Config.DatabaseGroups.Count < 1)
            {
                throw new VersioningException("There is no DatabaseGroup defined in the Config file.");
            }

            if (Config.DatabaseGroups.Count(x => x.Databases == null || x.Databases.Count == 0) > 0)
            {
                throw new VersioningException("One or more DatabaseGroup is missing required Database element in the Config file.");
            }

            if (string.IsNullOrEmpty(Config.ChangeScriptDirectory.Path))
            {
                throw new VersioningException("ChangeScriptDirectory is not defined in the Config file.");
            }

            if (Config.LogTable == null || string.IsNullOrEmpty(Config.LogTable.SchemaName) || string.IsNullOrEmpty(Config.LogTable.TableName))
            {
                throw new VersioningException("LogTable or it's schemaName, tableName attributes are not defined in the Config file.");
            }
        }
    }

    [Serializable()]
    [XmlRoot("Config")]
    public class Config
    {
        [XmlArray("DatabaseGroups")]
        [XmlArrayItem("DatabaseGroup", typeof(DatabaseGroup))]
        public List<DatabaseGroup> DatabaseGroups { get; set; }
        [XmlElement(ElementName = "LogTable", Type = typeof(LogTable))]
        public LogTable LogTable { get; set; }
        [XmlElement(ElementName = "LogDirectory", Type = typeof(Dir))]
        public Dir LogDirectory { get; set; }
        [XmlElement(ElementName = "ChangeScriptDirectory", Type = typeof(Dir))]
        public Dir ChangeScriptDirectory { get; set; }
    }

    [Serializable()]
    public class LogTable
    {
        [XmlAttribute("schemaName")]
        public string SchemaName { get; set; }
        [XmlAttribute("tableName")]
        public string TableName { get; set; }
    }

    [Serializable()]
    public class Dir
    {
        [XmlAttribute("path")]
        public string Path { get; set; }
    }

    [Serializable()]
    public class DatabaseGroup
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlElement("Database")]
        public List<Database> Databases { get; set; }
    }

    [Serializable()]
    public class Database
    {
        [XmlAttribute("name")]
        public string Name { get; set; }
        [XmlAttribute("connectionString")]
        public string ConnectionString { get; set; }
        [XmlArray("Replacements")]
        [XmlArrayItem("Replace", typeof(TextReplacement))]
        public List<TextReplacement> Replacements { get; set; }
    }

    [Serializable()]
    public class TextReplacement
    {
        [XmlAttribute("text")]
        public string Text { get; set; }
        [XmlAttribute("replacementText")]
        public string ReplacementText { get; set; }
    }
}
