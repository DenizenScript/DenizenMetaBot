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
        /// Plugins that go into the <see cref="DangerousPlugins"/> list.
        /// </summary>
        public static string[] BAD_PLUGINS = new string[] { "SkinsRestorer", "AuthMe", "PlugMan", "CMI", "PluginManager", "MySkin" };

        /// <summary>
        /// Plugins that go into the <see cref="IffyPlugins"/> list.
        /// </summary>
        public static string[] IFFY_PLUGINS = new string[] { "FeatherBoard" };

        /// <summary>
        /// Plugins that should show version output.
        /// </summary>
        public static string[] VERSION_PLUGINS = new string[] { "Citizens", "Denizen", "Depenizen", "Sentinel", "dDiscordBot" };

        /// <summary>
        /// Lowercase text that usually is a bad sign.
        /// </summary>
        public static string[] DANGER_TEXT = new string[] { "cracked by", "blackspigot", "issued server command: /reload" };

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
        /// Checks the value as not null or whitespace, then adds it to the embed as an inline field in a code block with a length limit applied.
        /// </summary>
        public static void AutoField(EmbedBuilder builder, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                value = LimitStringLength(value.Replace('`', '\''), 256, 240);
                builder.AddField(key, $"`{value}`", true);
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
        public List<string> DangerousPlugins = new List<string>();

        /// <summary>
        /// Any sometimes-conflictive plugins found in the log.
        /// </summary>
        public List<string> IffyPlugins = new List<string>();

        /// <summary>
        /// Plugins whose versions will be listed.
        /// </summary>
        public List<string> PluginVersions = new List<string>();

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
            IsBungee = FullLogTextLower.Replace("makes it possible to use bungeecord", "").Contains("bungee");
            IsOffline = FullLogText.Contains(OFFLINE_NOTICE);
            Console.WriteLine($"Offline={IsOffline}, Bungee={IsBungee}");
            ServerVersion = GetFromTextTilEndOfLine(FullLogText, SERVER_VERSION_PREFIX);
            if (string.IsNullOrWhiteSpace(ServerVersion))
            {
                ServerVersion = GetFromTextTilEndOfLine(FullLogText, SERVER_VERSION_PREFIX_BACKUP);
            }
            Console.WriteLine($"ServerVersion={ServerVersion}");
        }

        /// <summary>
        /// Gathers a UUID version code if possible from the log.
        /// </summary>
        public void ProcessUUIDCheck()
        {
            string firstUUID = GetFromTextTilEndOfLine(FullLogText, PLAYER_UUID_PREFIX);
            if (firstUUID.Length > UUID_LENGTH)
            {
                string uuid = firstUUID.Substring(firstUUID.Length - UUID_LENGTH, UUID_LENGTH);
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
                string pluginLoadText = GetFromTextTilEndOfLine(FullLogText, LoadMessageFor(plugin));
                if (pluginLoadText.Length != 0)
                {
                    Console.WriteLine($"Plugin Version: {pluginLoadText}");
                    PluginVersions.Add(pluginLoadText);
                }
            }
            foreach (string plugin in BAD_PLUGINS)
            {
                string pluginLoadText = GetFromTextTilEndOfLine(FullLogText, LoadMessageFor(plugin));
                if (pluginLoadText.Length != 0)
                {
                    Console.WriteLine($"Dangerous Plugin: {pluginLoadText}");
                    DangerousPlugins.Add(pluginLoadText);
                }
            }
            foreach (string plugin in IFFY_PLUGINS)
            {
                string pluginLoadText = GetFromTextTilEndOfLine(FullLogText, LoadMessageFor(plugin));
                if (pluginLoadText.Length != 0)
                {
                    Console.WriteLine($"Iffy Plugin: {pluginLoadText}");
                    IffyPlugins.Add(pluginLoadText);
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
        /// Runs the log checker in full.
        /// </summary>
        public void Run()
        {
            Console.WriteLine("Running log check...");
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
            bool shouldWarning = LikelyOffline || (OtherNoteworthyLines.Count > 0) || (DangerousPlugins.Count > 0);
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Log Check Results").WithThumbnailUrl(shouldWarning ? Constants.WARNING_ICON : Constants.INFO_ICON);
            AutoField(embed, "Server Version", ServerVersion);
            if (IsOffline)
            {
                AutoField(embed, "Online/Offline", IsBungee ? "Offline, but running bungee." : "Offline (bungee status unknown).");
            }
            if (UUIDVersion != 0)
            {
                string description = UUIDVersion == 4 ? "Online" : "Offline";
                AutoField(embed, "Detected Player UUID Version", $"UUID Version: {UUIDVersion} ({description})" );
            }
            AutoField(embed, "Plugin Version(s)", string.Join('\n', PluginVersions));
            AutoField(embed, "Bad Plugin(s)", string.Join('\n', DangerousPlugins));
            AutoField(embed, "Iffy Plugin(s)", string.Join('\n', IffyPlugins));
            AutoField(embed, "Potentially Bad Line(s)", string.Join('\n', OtherNoteworthyLines));
            return embed.Build();
        }
    }
}
