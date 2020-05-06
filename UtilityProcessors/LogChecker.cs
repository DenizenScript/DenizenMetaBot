using System;
using System.Collections.Generic;
using System.Text;
using Discord.Net;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticToolkit;

namespace DenizenBot.UtilityProcessors
{
    /// <summary>
    /// Special class to process log paste checking.
    /// </summary>
    public class LogChecker
    {

        /// <summary>
        /// Plugins that should show version output.
        /// </summary>
        public static string[] VERSION_PLUGINS = new string[] { "Citizens", "Denizen", "Depenizen", "Sentinel", "dDiscordBot" };

        /// <summary>
        /// Lowercase text that usually is a bad sign.
        /// </summary>
        public static string[] DANGER_TEXT = new string[] {
            // cracked plugins
            "cracked by", "crack by", "cracked version", "blackspigot",
            // reloads
            "issued server command: /reload", "issued server command: /rl",
            // hosts that break stuff
            "minehut" };

        /// <summary>
        /// The text that comes before the server version report.
        /// </summary>
        public const string SERVER_VERSION_PREFIX = "This server is running CraftBukkit version",
            SERVER_VERSION_PREFIX_BACKUP = "This server is running ";

        /// <summary>
        /// The text that comes before a player UUID post.
        /// </summary>
        public const string PLAYER_UUID_PREFIX = "UUID of player ";

        /// <summary>
        /// The text that indicates a server is in offline mode.
        /// </summary>
        public const string OFFLINE_NOTICE = "**** SERVER IS RUNNING IN OFFLINE/INSECURE MODE!";

        /// <summary>
        /// Matcher for valid text in a player UUID.
        /// </summary>
        public static AsciiMatcher UUID_ASCII_MATCHER = new AsciiMatcher("0123456789abcdef-");

        /// <summary>
        /// A player UUID is always exactly 36 characters long.
        /// </summary>
        public const int UUID_LENGTH = 36;

        /// <summary>
        /// The version ID is always at index 14 of a UUID.
        /// </summary>
        public const int VERSION_ID_LOCATION = 14;

        /// <summary>
        /// Plugins that are suspicious (like ones that relate to cracked servers).
        /// Map of plugin names to messages.
        /// </summary>
        public static readonly Dictionary<string, string> SUSPICIOUS_PLUGINS = new Dictionary<string, string>();

        /// <summary>
        /// Plugins that might cause problems.
        /// Map of plugin names to messages.
        /// </summary>
        public static readonly Dictionary<string, string> MESSY_PLUGINS = new Dictionary<string, string>();

        /// <summary>
        /// Plugins that should be tracked but aren't a problem.
        /// Map of plugin names to messages.
        /// </summary>
        public static readonly Dictionary<string, string> MONITORED_PLUGINS = new Dictionary<string, string>();

        /// <summary>
        /// Adds a plugin by name to the list of special plugins to track and report.
        /// </summary>
        /// <param name="set">The set of plugins to add to.</param>
        /// <param name="message">The message to include, if any.</param>
        /// <param name="names">The name(s) of plugins to track.</param>
        public static void AddReportedPlugin(Dictionary<string, string> set, string message, params string[] names)
        {
            foreach (string name in names)
            {
                set.Add(name, message);
            }
        }

        static LogChecker()
        {
            AddReportedPlugin(SUSPICIOUS_PLUGINS, "(Login authenticator plugin)", "AuthMe", "LoginSecurity", "nLogin", "PinAuthentication");
            AddReportedPlugin(SUSPICIOUS_PLUGINS, "(Offline-fixer plugin)", "SkinsRestorer", "MySkin", "AntiJoinBot", "AJB");
            AddReportedPlugin(MESSY_PLUGINS, "- PlugMan is dangerous and will cause unpredictable issues. Remove it.", "PlugMan", "PluginManager");
            AddReportedPlugin(MESSY_PLUGINS, "- This plugin adds Below_Name scoreboards to NPCs.", "TAB");
            AddReportedPlugin(MESSY_PLUGINS, "- NPC Command plugins have never had a valid reason to exist, as there have always been better ways to do that. The modern way is <https://wiki.citizensnpcs.co/NPC_Commands>.", "CommandNPC", "CitizensCMD");
            AddReportedPlugin(MESSY_PLUGINS, "- If you want NPCs that send players to other servers, check <https://wiki.citizensnpcs.co/NPC_Commands>.", "BungeeNPC");
            AddReportedPlugin(MESSY_PLUGINS, "- To make NPCs speak, use '/npc text', or '/npc command', or Denizen. You don't need a dedicated text plugin for this.", "CitizensText");
            AddReportedPlugin(MESSY_PLUGINS, "", "FeatherBoard", "MVdWPlaceholderAPI", "AnimatedNames", "CMI");
            AddReportedPlugin(MONITORED_PLUGINS, "", "WorldGuard", "MythicMobs", "NPC_Destinations", "NPC_Police");
        }

        /// <summary>
        /// Gets the text of a plugin name + version, if any.
        /// </summary>
        /// <param name="plugin">The plugin name to search for.</param>
        /// <returns>The plugin name + version, if any.</returns>
        public string GetPluginText(string plugin)
        {
            if (!IsDenizenDebug)
            {
                return GetFromTextTilEndOfLine(FullLogText, LoadMessageFor(plugin)).After("Loading ");
            }
            int start = FullLogText.IndexOf("\nActive Plugins (");
            int end = FullLogText.IndexOf("\nLoaded Worlds (");
            string pluginLine = FullLogText.Substring(start, end - start).Replace(((char)0x01) + "2", "").Replace(((char)0x01) + "4", "");
            int pluginNameIndex = pluginLine.IndexOf(plugin);
            if (pluginNameIndex == -1)
            {
                return "";
            }
            int commaIndex = pluginLine.IndexOf(',', pluginNameIndex);
            int newlineIndex = pluginLine.IndexOf('\n', pluginNameIndex);
            int endIndex = commaIndex;
            if (commaIndex == -1)
            {
                endIndex = newlineIndex;
            }
            else if (newlineIndex != -1)
            {
                endIndex = Math.Min(commaIndex, newlineIndex);
            }
            if (endIndex == -1)
            {
                endIndex = pluginLine.Length;
            }
            return pluginLine.Substring(pluginNameIndex, endIndex - pluginNameIndex);
        }

        /// <summary>
        /// Gets a string of the load message for a plugin name.
        /// </summary>
        public static string LoadMessageFor(string plugin)
        {
            return $"[{plugin}] Loading {plugin} v";
        }

        /// <summary>
        /// Utility to get the text starting at the input text, going to the end of the line.
        /// Returns an empty string if nothing is found.
        /// </summary>
        public static string GetFromTextTilEndOfLine(string fullLog, string text)
        {
            int index = fullLog.IndexOf(text);
            if (index == -1)
            {
                return "";
            }
            int endIndex = fullLog.IndexOf('\n', index);
            if (endIndex == -1)
            {
                endIndex = fullLog.Length;
            }
            return fullLog.Substring(index, endIndex - index);
        }

        /// <summary>
        /// Utility to limit the length of a string for stabler output.
        /// </summary>
        public static string LimitStringLength(string input, int maxLength, int cutLength)
        {
            if (input.Length > maxLength)
            {
                return input.Substring(0, cutLength) + "...";
            }
            return input;
        }

        /// <summary>
        /// Escapes text for Discord output within a code block.
        /// </summary>
        /// <param name="text">The text to escape.</param>
        /// <returns>The escaped text.</returns>
        public static string Escape(string text)
        {
            return text.Replace('`', '\'');
        }

        /// <summary>
        /// Checks the value as not null or whitespace, then adds it to the embed as an inline field in a code block with a length limit applied.
        /// </summary>
        public static void AutoField(EmbedBuilder builder, string key, string value, bool blockCode = true, bool inline = true)
        {
            if (builder.Length + Math.Min(value.Length, 450) + key.Length > 1800)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (blockCode)
                {
                    value = Escape(value);
                }
                value = LimitStringLength(value, 450, 400);
                builder.AddField(key, blockCode ? $"`{value}`" : value, inline);
            }
        }

        /// <summary>
        /// The full log text.
        /// </summary>
        public string FullLogText;

        /// <summary>
        /// The full log text, lowercased.
        /// </summary>
        public string FullLogTextLower;

        /// <summary>
        /// The server version (if any).
        /// </summary>
        public string ServerVersion = "";

        /// <summary>
        /// Any potentially problematic plugins found in the log.
        /// </summary>
        public string DangerousPlugins = "";

        /// <summary>
        /// Any sometimes-conflictive plugins found in the log.
        /// </summary>
        public string IffyPlugins = "";

        /// <summary>
        /// Any other relevant plugins found in the log.
        /// </summary>
        public string OtherPlugins = "";

        /// <summary>
        /// Plugins whose versions will be listed.
        /// </summary>
        public string PluginVersions = "";

        /// <summary>
        /// Lines of note, usually ones that indicate a bad sign.
        /// </summary>
        public List<string> OtherNoteworthyLines = new List<string>();

        /// <summary>
        /// Whether this server log appears to be offline mode.
        /// </summary>
        public bool IsOffline = false;

        /// <summary>
        /// Whether this server mentions bungee (and thus might not actually be offline).
        /// </summary>
        public bool IsBungee = false;

        /// <summary>
        /// Visible UUID version (or 0 if none).
        /// </summary>
        public int UUIDVersion = 0;

        /// <summary>
        /// Whether this looks like a Denizen debug log.
        /// </summary>
        public bool IsDenizenDebug = false;

        /// <summary>
        /// Whether the server is likely to be offline.
        /// </summary>
        public bool LikelyOffline
        {
            get
            {
                if (UUIDVersion == 4)
                {
                    return false;
                }
                if (UUIDVersion == 3)
                {
                    return true;
                }
                if (IsOffline && !IsBungee)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Construct the log checker system.
        /// </summary>
        /// <param name="fullLog">The full log text.</param>
        public LogChecker(string fullLog)
        {
            if (fullLog.Length > 1024 * 1024)
            {
                fullLog = fullLog.Substring(0, 1024 * 1024);
            }
            FullLogText = fullLog.Replace("\r\n", "\n").Replace('\r', '\n');
            FullLogTextLower = FullLogText.ToLowerFast();
        }

        /// <summary>
        /// Gathers the most basic information from the log.
        /// </summary>
        public void ProcessBasics()
        {
            if (IsDenizenDebug)
            {
                string mode = GetFromTextTilEndOfLine(FullLogText, "Mode: ");
                IsBungee = mode.Contains("BungeeCord");
                IsOffline = mode.Contains("offline");
                ServerVersion = GetFromTextTilEndOfLine(FullLogText, "Server Version: ").After("Server Version: ");
            }
            else
            {
                IsBungee = FullLogTextLower.Replace("makes it possible to use bungeecord", "").Contains("bungee");
                IsOffline = FullLogText.Contains(OFFLINE_NOTICE);
                ServerVersion = GetFromTextTilEndOfLine(FullLogText, SERVER_VERSION_PREFIX);
                if (string.IsNullOrWhiteSpace(ServerVersion))
                {
                    ServerVersion = GetFromTextTilEndOfLine(FullLogText, SERVER_VERSION_PREFIX_BACKUP);
                }
            }
            Console.WriteLine($"Offline={IsOffline}, Bungee={IsBungee}");
            Console.WriteLine($"ServerVersion={ServerVersion}");
            CheckServerVersion();
            Console.WriteLine($"ServerVersionChecked={ServerVersion}");
        }

        /// <summary>
        /// Gets the server version status result (outdated vs not) as a string.
        /// </summary>
        /// <param name="version">The user's version.</param>
        /// <param name="isGood">Whether the result is a positive result (current) or not (outdated or invalid).</param>
        /// <returns>The result string, or an empty string if none.</returns>
        public static string ServerVersionStatusOutput(string versionInput, out bool isGood)
        {
            isGood = false;
            if (string.IsNullOrWhiteSpace(versionInput))
            {
                Console.WriteLine("No server version, disregarding check.");
                return "";
            }
            versionInput = versionInput.ToLowerFast();
            if (versionInput.StartsWith("this server is running "))
            {
                versionInput = versionInput.Substring("this server is running ".Length);
            }
            string[] subData = versionInput.Split(' ', 4);
            if (subData[1] != "version" || !subData[2].StartsWith("git-") || subData[2].CountCharacter('-') < 2 || !subData[3].StartsWith("(mc: "))
            {
                Console.WriteLine("Server version doesn't match expected format, disregarding check.");
                return "";
            }
            string spigotVersionText = subData[2].Split('-', 3)[2];
            string mcVersionText = subData[3].Substring("(mc: ".Length).Before(')');
            string majorMCVersion = mcVersionText.CountCharacter('.') == 2 ? mcVersionText.BeforeLast('.') : mcVersionText;
            if (!double.TryParse(majorMCVersion, out double versionNumb))
            {
                Console.WriteLine($"Major MC version '{majorMCVersion}' is not a double, disregarding check.");
                return "";
            }
            if (versionNumb < DenizenMetaBot.LowestServerVersion)
            {
                return "Outdated MC version";
            }
            if (versionNumb > DenizenMetaBot.HighestServerVersion)
            {
                return "New MC version? Bot may need config update";
            }
            if (subData[0] == "paper")
            {
                if (!int.TryParse(spigotVersionText, out int paperVersionNumber))
                {
                    Console.WriteLine($"Paper version '{spigotVersionText}' is not an integer, disregarding check.");
                    return "";
                }
                if (!BuildNumberTracker.PaperBuildTrackers.TryGetValue(majorMCVersion, out BuildNumberTracker.BuildNumber buildTracker))
                {
                    Console.WriteLine($"Paper version {paperVersionNumber} is not tracked, disregarding check.");
                    return "";
                }
                if (buildTracker.IsCurrent(paperVersionNumber, out int behindBy))
                {
                    isGood = true;
                    return "Current build";
                }
                else
                {
                    return $"Outdated build, behind by {behindBy}... Current build is {buildTracker.Value}";
                }
            }
            else if (subData[0] == "spigot" || subData[0] == "craftbukkit")
            {
                spigotVersionText = spigotVersionText.Before('-');
                if (spigotVersionText.Length != 7 || !HEX_ASCII_MATCHER.IsOnlyMatches(spigotVersionText))
                {
                    Console.WriteLine($"Spigot version '{spigotVersionText}' is wrong format, disregarding check.");
                    return "";
                }
                int behind = BuildNumberTracker.GetSpigotVersionsBehindBy(spigotVersionText);
                if (behind == -1)
                {
                    Console.WriteLine($"Spigot version '{spigotVersionText}' is not tracked, disregarding check.");
                    return "";
                }
                if (behind == 0)
                {
                    isGood = true;
                    return "Current build";
                }
                else
                {
                    return $"Outdated build, behind by {behind}";
                }
            }
            else
            {
                Console.WriteLine($"Server type '{subData[0]}' is not managed, disregarding check.");
            }
            return "";
        }

        /// <summary>
        /// A matcher for hex characters: 0123456789abcdef.
        /// </summary>
        public static AsciiMatcher HEX_ASCII_MATCHER = new AsciiMatcher("0123456789abcdef");

        /// <summary>
        /// Checks the linked server version against the current known server version.
        /// Expects a version output of the format: "This server is running {TYPE} version git-{TYPE}-{VERS} (MC: {MCVERS}) (Implementing API version {MCVERS}-{SUBVERS}-SNAPSHOT)".
        /// Will note on outdated MCVERS (per config), or note on newer. Will identify an outdated Spigot (or paper) sub-version.
        /// </summary>
        public void CheckServerVersion()
        {
            ServerVersion = ServerVersion.Replace('`', '\'');
            bool startsWithRunning = ServerVersion.StartsWith("This server is running ");
            if (string.IsNullOrWhiteSpace(ServerVersion) || (!IsDenizenDebug && !startsWithRunning))
            {
                Console.WriteLine("No server version, disregarding check.");
                ServerVersion = $"`{LimitStringLength(ServerVersion, 400, 350)}`";
                return;
            }
            string versionToCheck = ServerVersion.ToLowerFast();
            if (IsDenizenDebug)
            {
                versionToCheck = versionToCheck.Replace("version:", "version");
            }
            string output = ServerVersionStatusOutput(versionToCheck, out _);
            if (startsWithRunning)
            {
                ServerVersion = ServerVersion.Substring("This server is running ".Length).BeforeLast(" (Implementing API version");
                ServerVersion = $"`{LimitStringLength(ServerVersion, 400, 350)}`";
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                ServerVersion += $"-- ({output})";
            }
        }

        /// <summary>
        /// Gathers a UUID version code if possible from the log.
        /// </summary>
        public void ProcessUUIDCheck()
        {
            string uuid;
            if (IsDenizenDebug)
            {
                uuid = GetFromTextTilEndOfLine(FullLogText, "p@").After("p@");
            }
            else
            {
                uuid = GetFromTextTilEndOfLine(FullLogText, PLAYER_UUID_PREFIX).After(" is ");
            }
            if (uuid.Length >= UUID_LENGTH)
            {
                uuid = uuid.Substring(0, UUID_LENGTH);
                Console.WriteLine($"Player UUID: {uuid}");
                if (UUID_ASCII_MATCHER.IsOnlyMatches(uuid))
                {
                    char versCode = uuid[VERSION_ID_LOCATION];
                    Console.WriteLine($"Player UUID version: {versCode}");
                    if (versCode == '3')
                    {
                        UUIDVersion = 3;
                    }
                    else if (versCode == '4')
                    {
                        UUIDVersion = 4;
                    }
                }
            }
        }

        /// <summary>
        /// Gathers data about plugins loading from the log.
        /// </summary>
        public void ProcessPluginLoads()
        {
            foreach (string plugin in VERSION_PLUGINS)
            {
                string pluginLoadText = GetPluginText(plugin);
                if (pluginLoadText.Length != 0)
                {
                    string projectName = BuildNumberTracker.SplitToNameAndVersion(pluginLoadText, out string versionText);
                    if (BuildNumberTracker.TryGetBuildFor(projectName, versionText, out BuildNumberTracker.BuildNumber build, out int buildNum))
                    {
                        string resultText = "";
                        if (build.IsCurrent(buildNum, out int behindBy))
                        {
                            resultText = "Current build";
                        }
                        else
                        {
                            resultText = $"**Outdated build**, behind by {behindBy}";
                        }
                        PluginVersions += $"`{pluginLoadText.Replace('`', '\'')}` -- ({resultText})\n";
                        Console.WriteLine($"Plugin Version: {pluginLoadText} -> {resultText}");
                    }
                    else
                    {
                        PluginVersions += $"`{pluginLoadText.Replace('`', '\'')}`\n";
                    }
                }
            }
            ProcessPluginSet(SUSPICIOUS_PLUGINS, ref DangerousPlugins, "Dangerous/Suspicious/Bad");
            ProcessPluginSet(MESSY_PLUGINS, ref IffyPlugins, "Iffy/Messy");
            ProcessPluginSet(MONITORED_PLUGINS, ref OtherPlugins, "Monitored/Noteworthy");
        }

        /// <summary>
        /// Processes a set of monitored plugins, adding them to the output string if found in the log.
        /// </summary>
        /// <param name="pluginSet">The plugins to track.</param>
        /// <param name="listOutput">The output string to append to.</param>
        /// <param name="type">The category of plugin being checked for.</param>
        public void ProcessPluginSet(Dictionary<string, string> pluginSet, ref string listOutput, string type)
        {
            string lastmessage = "";
            foreach ((string plugin, string notice) in pluginSet)
            {
                string pluginLoadText = GetPluginText(plugin);
                if (pluginLoadText.Length != 0)
                {
                    Console.WriteLine($"{type} Plugin: {pluginLoadText}");
                    string message = "";
                    if (lastmessage != notice)
                    {
                        message = notice;
                        lastmessage = message;
                    }
                    string toOutput = $"`{pluginLoadText}` {message}\n";
                    if (listOutput.Length + toOutput.Length < 430)
                    {
                        listOutput += toOutput;
                    }
                    else if (listOutput.Length + plugin.Length < 430)
                    {
                        listOutput += $"`{plugin}`\n";
                    }
                }
            }
        }

        /// <summary>
        /// Looks for dangerous text sometimes found in logs.
        /// </summary>
        public void ProcessDangerText()
        {
            foreach (string sign in DANGER_TEXT)
            {
                int signIndex = FullLogTextLower.IndexOf(sign);
                if (signIndex >= 0)
                {
                    int lineStart = FullLogText.LastIndexOf('\n', signIndex) + 1;
                    int lineEnd = FullLogText.IndexOf('\n', signIndex);
                    if (lineEnd == -1)
                    {
                        lineEnd = FullLogText.Length;
                    }
                    string dangerousLine = FullLogText.Substring(lineStart, lineEnd - lineStart);
                    Console.WriteLine($"Dangerous Text: {dangerousLine}");
                    OtherNoteworthyLines.Add(dangerousLine);
                }
            }
        }

        /// <summary>
        /// Performs a test to see if this paste is a Denizen debug log. If it is, <see cref="IsDenizenDebug"/> will be set to true.
        /// </summary>
        public void TestForDenizenDebug()
        {
            if (!FullLogText.StartsWith("Java Version: "))
            {
                return;
            }
            if (!FullLogText.Contains("\nUp-time: ") || (!FullLogText.Contains("\nServer Version: ") && !FullLogText.Contains("\nCraftBukkit Version: "))
                || !FullLogText.Contains("\nDenizen Version: ") || !FullLogText.Contains("\nActive Plugins (")
                || !FullLogText.Contains("\nLoaded Worlds (") || !FullLogText.Contains("\nOnline Players (")
                || !FullLogText.Contains("\nOffline Players: ") || !FullLogText.Contains("\nMode: "))
            {
                return;
            }
            IsDenizenDebug = true;
        }

        /// <summary>
        /// Runs the log checker in full.
        /// </summary>
        public void Run()
        {
            Console.WriteLine("Running log check...");
            TestForDenizenDebug();
            ProcessBasics();
            ProcessUUIDCheck();
            ProcessPluginLoads();
            ProcessDangerText();
        }

        /// <summary>
        /// Gets an output embed result.
        /// </summary>
        public Embed GetResult()
        {
            bool shouldWarning = LikelyOffline || (OtherNoteworthyLines.Count > 0) || (DangerousPlugins.Length > 0);
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Log Check Results").WithThumbnailUrl(shouldWarning ? Constants.WARNING_ICON : Constants.INFO_ICON);
            AutoField(embed, "Server Version", ServerVersion, blockCode: false, inline: false);
            if (IsOffline)
            {
                AutoField(embed, "Online/Offline", IsBungee ? "Offline, but running bungee." : "Offline (bungee status unknown).");
            }
            if (UUIDVersion != 0)
            {
                string description = UUIDVersion == 4 ? "Online" : "Offline";
                AutoField(embed, "Detected Player UUID Version", $"UUID Version: {UUIDVersion} ({description})" );
            }
            AutoField(embed, "Plugin Version(s)", string.Join('\n', PluginVersions), blockCode: false, inline: false);
            AutoField(embed, "Bad Plugin(s)", DangerousPlugins, blockCode: false);
            AutoField(embed, "Iffy Plugin(s)", IffyPlugins, blockCode: false);
            AutoField(embed, "Other Noteworthy Plugin(s)", OtherPlugins, blockCode: false);
            AutoField(embed, "Potentially Bad Line(s)", string.Join('\n', OtherNoteworthyLines), inline: false);
            return embed.Build();
        }
    }
}
