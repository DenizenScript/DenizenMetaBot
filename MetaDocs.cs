using System;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord.Net;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticDataSyntax;

namespace DenizenBot
{
    /// <summary>
    /// Helper class to contain the full set of meta documentation, and the logic to load it in.
    /// </summary>
    public class MetaDocs
    {
        public static readonly string[] DENIZEN_SOURCES = new string[]
        {
            "https://github.com/DenizenScript/Denizen-For-Bukkit/archive/dev.zip",
            "https://github.com/DenizenScript/Denizen-Core/archive/master.zip"
        };

        public static readonly string[] DENIZEN_ADDON_SOURCES = new string[]
        {
            "https://github.com/DenizenScript/dDiscordBot/archive/master.zip",
            "https://github.com/DenizenScript/Webizen/archive/master.zip",
            "https://github.com/DenizenScript/dIRCBot/archive/master.zip"
        };

        public static readonly string DEPENIZEN_SPECIAL_LIMITED_SOURCE = "https://github.com/DenizenScript/Depenizen/archive/master.zip";

        public static readonly string DEPENIZEN_FOLDER_LIMIT = "bukkit";

        public void DownloadAll()
        {
            foreach (string src in DENIZEN_SOURCES)
            {
                Download(src);
            }
            foreach (string src in DENIZEN_ADDON_SOURCES)
            {
                Download(src);
            }
            Download(DEPENIZEN_SPECIAL_LIMITED_SOURCE, DEPENIZEN_FOLDER_LIMIT);
        }

        public static ZipArchive DownloadZip(string url)
        {
            HttpClient client = new HttpClient();
            client.Timeout = new TimeSpan(0, 2, 0);
            byte[] zipDataBytes = client.GetByteArrayAsync(url).Result;
            MemoryStream zipDataStream = new MemoryStream(zipDataBytes);
            return new ZipArchive(zipDataStream);
        }

        /// <summary>
        /// End of file marker.
        /// </summary>
        public const string END_OF_FILE_MARK = "\0END_OF_FILE";

        /// <summary>
        /// Start of file marker prefix.
        /// </summary>
        public const string START_OF_FILE_PREFIX = "\0START_OF_FILE ";

        public static string[] ReadLines(ZipArchive zip, string folderLimit = null)
        {
            List<string> lines = new List<string>();
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (folderLimit != null && !entry.FullName.StartsWith(folderLimit))
                {
                    continue;
                }
                if (!entry.FullName.EndsWith(".java"))
                {
                    continue;
                }
                using (Stream entryStream = entry.Open())
                {
                    lines.Add(START_OF_FILE_PREFIX + entry.FullName);
                    lines.AddRange(entryStream.AllLinesOfText().Where((s) => s.TrimStart().StartsWith("// ")).Select((s) => s.Trim().Substring("// ".Length)));
                    lines.Add(END_OF_FILE_MARK);
                }
            }
            return lines.ToArray();
        }

        public void LoadDataFromLines(string[] lines)
        {
            string file = "<unknown>";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith(START_OF_FILE_PREFIX))
                {
                    file = line.Substring(START_OF_FILE_PREFIX.Length);
                }
                else if (line.StartsWith("<--[") && line.EndsWith("]"))
                {
                    string objectType = line.Substring("<--[".Length, lines[i].Length - "<--[]".Length);
                    List<string> objectData = new List<string>();
                    for (i++; i < lines.Length; i++)
                    {
                        if (lines[i] == "-->")
                        {
                            break;
                        }
                        else if (lines[i] == END_OF_FILE_MARK || lines[i].StartsWith(START_OF_FILE_PREFIX))
                        {
                            LoadErrors.Add("While processing " + file + " was not able to find the end of an object's documentation!");
                            objectData = null;
                            break;
                        }
                        objectData.Add(lines[i]);
                    }
                    if (objectData == null)
                    {
                        continue;
                    }
                    LoadInObject(objectType, file, objectData.ToArray());
                }
            }
        }

        public void LoadInObject(string objectType, string file, string[] objectData)
        {
            // TODO
        }

        public void Download(string source, string folderLimit = null)
        {
            try
            {
                ZipArchive zip = DownloadZip(source);
                string[] fullLines = ReadLines(zip, folderLimit);
                LoadDataFromLines(fullLines);
            }
            catch (Exception ex)
            {
                LoadErrors.Add("Internal exception - " + ex.GetType().FullName + " ... see bot console for details.");
                Console.WriteLine("Error: " + ex.ToString());
            }
        }

        /// <summary>
        /// A list of load-time errors, if any.
        /// </summary>
        public List<string> LoadErrors = new List<string>();
    }
}
