using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace VersionDB
{
    public static class ChangeReader
    {
        public static List<ReleaseChanges> AllReleaseChanges { get; set; }

        public static void ReadAllChanges()
        {
            AllReleaseChanges = new List<ReleaseChanges>();

            if (!Directory.Exists(Constants.CHANGE_SCRIPT_DIRECTORY))
                Directory.CreateDirectory(Constants.CHANGE_SCRIPT_DIRECTORY);

            string[] files = Directory.GetFiles(Constants.CHANGE_SCRIPT_DIRECTORY, "*.xml");
            foreach (string filePath in files)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ReleaseChanges));

                StreamReader reader = new StreamReader(filePath);
                ReleaseChanges releaseChanges = (ReleaseChanges)serializer.Deserialize(reader);
                reader.Close();

                AllReleaseChanges.Add(releaseChanges);
            }

            if (AllReleaseChanges.Count < 1)
            {
                Display.DisplayMessage(DisplayType.Info, "There is no  database change xml in {0}. If you want to start using the tool based on an existing database use the command -generatescript<space>{{database_group}} to generate the initial scripts for the current state of the database.", Constants.CHANGE_SCRIPT_DIRECTORY);
                return;
            }

            if (AllReleaseChanges.Count(x => string.IsNullOrEmpty(x.Name)) > 0)
            {
                throw new VersioningException("One or more change xml files is missing required attribute - releaseName");
            }

            if (AllReleaseChanges.Count(x => x.Changes == null || x.Changes.Count == 0) > 0)
            {
                throw new VersioningException("There must be at least one \"Change\" element in the change xml file. One or more xml file is missing that.");
            }

            if (AllReleaseChanges.Count(x => x.Changes.Min(y => y.Version) != 1) > 0)
            {
                throw new VersioningException("Change versions must start with value 1. One or more xml file violates that constraint.");
            }

            if (AllReleaseChanges.Count(x => string.IsNullOrEmpty(x.PreviousReleaseName)) != 1)
            {
                throw new VersioningException("There must be exactly one XML with previousReleaseName attribute set to NULL.");
            }

            ReleaseChanges releaseChange = AllReleaseChanges.FirstOrDefault(x => string.IsNullOrEmpty(x.PreviousReleaseName));
            releaseChange.Sequence = 1;
            releaseChange.LastChangeVersion = releaseChange.Changes.Max(x => x.Version);

            int releaseChangesToProcess = AllReleaseChanges.Count;

            while (releaseChangesToProcess > 1)
            {
                string prevReleaseName = releaseChange.Name;
                int prevReleaseSequence = releaseChange.Sequence;

                if (AllReleaseChanges.Count(x => x.PreviousReleaseName == prevReleaseName) != 1)
                {
                    throw new VersioningException("There must be exactly one successor and one predecessor (except the first release) of a release version. Relese version \"" + prevReleaseName + "\" violates that constraint");
                }

                releaseChange = AllReleaseChanges.FirstOrDefault(x => x.PreviousReleaseName == prevReleaseName);
                releaseChange.Sequence = prevReleaseSequence + 1;
                releaseChange.LastChangeVersion = releaseChange.Changes.Max(x => x.Version);

                releaseChangesToProcess--;
            }

            AllReleaseChanges.FirstOrDefault(x => x.Sequence == AllReleaseChanges.Count).IsLatestRelease = true;
        }
    }

    [Serializable()]
    [XmlRoot("ReleaseChanges")]
    public class ReleaseChanges
    {
        [XmlAttribute("releaseName")]
        public string Name { get; set; }

        [XmlAttribute("previousReleaseName")]
        public string PreviousReleaseName { get; set; }

        [XmlElement("Change")]
        public List<Change> Changes { get; set; }

        public int Sequence { get; set; }
        public int LastChangeVersion { get; set; }
        public bool IsLatestRelease { get; set; }
    }

    [Serializable()]
    public class Change
    {
        [XmlAttribute("version")]
        public int Version { get; set; }
        [XmlAttribute("description")]
        public string Description { get; set; }
        [XmlElement("Sql")]
        public List<ChangeSql> ChangeSqls { get; set; }
    }

    [Serializable()]
    public class ChangeSql
    {
        [XmlAttribute("path")]
        public string Path { get; set; }
        [XmlText]
        public string Sql { get; set; }
    }
}
