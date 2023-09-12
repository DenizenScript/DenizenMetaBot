using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using FreneticUtilities.FreneticExtensions;
using YamlDotNet.RepresentationModel;
using System.Json;
using FreneticUtilities.FreneticToolkit;

namespace DenizenBot.UtilityProcessors
{
    /// <summary>Utility class to track current build number.</summary>
    public class BuildNumberTracker
    {
        /// <summary>Represents a single build number.</summary>
        public class BuildNumber
        {
            /// <summary>The name of the project.</summary>
            public string Name;

            /// <summary>A regex matcher for the project's build number.</summary>
            public Regex Matcher;

            /// <summary>The full Jenkins build number URL.</summary>
            public string JenkinsURL;

            /// <summary>How long before an update is needed.</summary>
            public static TimeSpan TIME_BEFORE_UPDATE = new(hours: 1, minutes: 0, seconds: 0);

            /// <summary>The max wait time for a build number download.</summary>
            public static TimeSpan DOWNLOAD_TIMEOUT = new(hours: 0, minutes: 1, seconds: 0);

            /// <summary>Constructs the build number instance, and grabs the current build number.</summary>
            /// <param name="projectName">The name of the project.</param>
            /// <param name="regexText">The regex matcher text.</param>
            /// <param name="jenkinsJobName">The jenkins job name.</param>
            /// <param name="jenkinsUrlBase">The jenkins URL base path, if not the default <see cref="DenizenMetaBotConstants.JENKINS_URL_BASE"/>.</param>
            public BuildNumber(string projectName, string regexText, string jenkinsJobName, string jenkinsUrlBase = DenizenMetaBotConstants.JENKINS_URL_BASE)
            {
                Name = projectName;
                Matcher = new Regex(regexText, RegexOptions.Compiled);
                JenkinsURL = $"{jenkinsUrlBase}/job/{jenkinsJobName}/lastSuccessfulBuild/buildNumber";
                UpdateValue();
            }

            /// <summary>The actual build number value.</summary>
            public int Value;

            /// <summary>When the build number was retrieved from the server.</summary>
            public DateTimeOffset RetrievedAt;

            /// <summary>Whether the build number is currently updating.</summary>
            public bool IsUpdating = false;

            /// <summary>The maximum amount a build can be behind before it should show a warning.</summary>
            public int MaxBehind = 15;

            /// <summary>Returns whether the project+version pair belongs to this build number tracker, and outputs the build number if so.</summary>
            /// <param name="project">The project name.</param>
            /// <param name="version">The version string.</param>
            /// <param name="buildNumber">Output of the build number, if it is a match.</param>
            /// <returns>True if the project+version pair belongs to this instance, otherwise false.</returns>
            public bool BelongsTo(string project, string version, out int buildNumber)
            {
                if (project.ToLowerFast() != Name.ToLowerFast())
                {
                    buildNumber = 0;
                    return false;
                }
                Match m = Matcher.Match(version);
                if (!m.Success || m.Groups.Count != 2 || !int.TryParse(m.Groups[1].Value, out int bnResult))
                {
                    buildNumber = 0;
                    return false;
                }
                buildNumber = bnResult;
                return true;
            }

            /// <summary>Returns whether the user build number value is current.</summary>
            /// <param name="userValue">The user's build number value.</param>
            /// <param name="behindBy">Output of how far behind the user value is.</param>
            /// <returns>True if the value is current, otherwise false.</returns>
            public bool IsCurrent(int userValue, out int behindBy)
            {
                Check();
                if (userValue < Value)
                {
                    behindBy = Value - userValue;
                    return false;
                }
                behindBy = 0;
                return true;
            }

            /// <summary>Checks if the number needs an update, and updates if so.</summary>
            public void Check()
            {
                if (IsUpdating || DateTimeOffset.Now.Subtract(RetrievedAt) > TIME_BEFORE_UPDATE)
                {
                    return;
                }
                UpdateValue();
            }

            /// <summary>Helper random object to spread out update checks.</summary>
            readonly static Random UpdateRandomizer = new();

            /// <summary>Immediately (but off-thread) update the value to current.</summary>
            public void UpdateValue()
            {
                IsUpdating = true;
                int delay = UpdateRandomizer.Next(3, 50);
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(millisecondsTimeout: 100 * delay);
                    try
                    {
                        int newValue = DirectGrabCurrent();
                        if (newValue == -1)
                        {
                            return;
                        }
                        Value = newValue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"While updating {Name} via URL {JenkinsURL}: {ex}");
                    }
                    IsUpdating = false;
                });
            }

            /// <summary>Return the current build number. -1 indicates no valid value.</summary>
            public virtual int DirectGrabCurrent()
            {
                Task<byte[]> downloadTask = Program.ReusableWebClient.GetByteArrayAsync(JenkinsURL);
                downloadTask.Wait(DOWNLOAD_TIMEOUT);
                if (!downloadTask.IsCompleted)
                {
                    return -1;
                }
                return int.Parse(StringConversionHelper.UTF8Encoding.GetString(downloadTask.Result).Trim());
            }
        }

        /// <summary>Build number tracker but for Paper.</summary>
        public class PaperBuildNumber : BuildNumber
        {
            public PaperBuildNumber(string projectName, string regexText, string _version) : base(projectName, regexText, "Paper", "(Paper API)")
            {
                Version = _version;
            }

            /// <summary>The webpath for the paper API build info JSON.</summary>
            public static string PAPER_API_PATH = "https://papermc.io/api/v2/projects/paper/version_group/{VERSION}/builds";

            /// <summary>The relevant server version, like "1.16".</summary>
            public string Version;

            public override int DirectGrabCurrent()
            {
                Task<byte[]> downloadTask = Program.ReusableWebClient.GetByteArrayAsync(PAPER_API_PATH.Replace("{VERSION}", Version));
                downloadTask.Wait(DOWNLOAD_TIMEOUT);
                if (!downloadTask.IsCompleted)
                {
                    return -1;
                }
                JsonValue json = JsonValue.Parse(StringConversionHelper.UTF8Encoding.GetString(downloadTask.Result));
                JsonArray array = (JsonArray)json["builds"];
                JsonValue lastBuild = array[^1];
                return (int)lastBuild["build"];
            }
        }

        /// <summary>Clears tracker data, for re-init.</summary>
        public static void Clear()
        {
            BuildNumbers.Clear();
            PaperBuildTrackers.Clear();
        }

        /// <summary>All currently tracked build numbers.</summary>
        public static List<BuildNumber> BuildNumbers = new(64);

        /// <summary>A mapping from version names to the relevant paper build number trackers.</summary>
        public static Dictionary<string, BuildNumber> PaperBuildTrackers = new();

        /// <summary>Causes all tracked build numbers to update immediately.</summary>
        public static void UpdateAll()
        {
            foreach (BuildNumber number in BuildNumbers.JoinWith(PaperBuildTrackers.Values))
            {
                number.UpdateValue();
            }
        }

        /// <summary>Adds a new build number instance to the <see cref="BuildNumbers"/> list.</summary>
        /// <param name="projectName">The name of the project.</param>
        /// <param name="regexText">The regex matcher text.</param>
        /// <param name="jenkinsJobName">The jenkins job name.</param>
        /// <param name="maxBehind">How far behind the build is allowed to safely be.</param>
        public static void AddTracker(string name, string regex, string jenkinsJob, int maxBehind)
        {
            BuildNumbers.Add(new BuildNumber(name, regex, jenkinsJob) { MaxBehind = maxBehind });
        }

        /// <summary>Adds a tracker for a Paper version.</summary>
        /// <param name="version">The version.</param>
        public static void AddPaperTracker(string version)
        {
            PaperBuildNumber tracker = new("Paper-" + version, $"git-Paper-(\\d+) \\(MC: {Regex.Escape(version)}(\\.\\d+)?\\)", version);
            PaperBuildTrackers.Add(version, tracker);
        }

        /// <summary>
        /// Gets the build number object corresponding to the project + version pair. Outputs the build tracker object and the version text's input build number.
        /// Returns whether the finding was successful
        /// </summary>
        /// <param name="project">The project name.</param>
        /// <param name="version">The version text.</param>
        /// <param name="foundNumber">The found build tracker.</param>
        /// <param name="userBuild">The user build number.</param>
        /// <returns>Whether a build was found.</returns>
        public static bool TryGetBuildFor(string project, string version, out BuildNumber foundNumber, out int userBuild)
        {
            foreach (BuildNumber number in BuildNumbers)
            {
                if (number.BelongsTo(project, version, out userBuild))
                {
                    foundNumber = number;
                    return true;
                }
            }
            foundNumber = null;
            userBuild = 0;
            return false;
        }

        /// <summary>Splits a name+version into a separate name and version, returning the name and outputting the version.</summary>
        /// <param name="fullText">The full name+version text.</param>
        /// <param name="version">Just the version.</param>
        /// <returns>The name.</returns>
        public static string SplitToNameAndVersion(string fullText, out string version)
        {
            string name = fullText.BeforeAndAfter(' ', out version);
            if (version.StartsWith("version "))
            {
                version = version["version ".Length..];
            }
            if (name.EndsWith(":"))
            {
                name = name[0..^1];
            }
            return name;
        }
    }
}
