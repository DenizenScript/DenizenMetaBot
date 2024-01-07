using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Discord.Net;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using DiscordBotBase;

namespace DenizenBot.UtilityProcessors
{
    /// <summary>Special class to process log paste checking.</summary>
    public class LogChecker
    {
        /// <summary>The emoji code for a yellow warning "!" symbol.</summary>
        public static string WARNING_SYMBOL = ":warning:";

        /// <summary>The emoji code for a red flag symbol.</summary>
        public static string RED_FLAG_SYMBOL = ":triangular_flag_on_post:";

        /// <summary>The emoji code for a green check mark symbol.</summary>
        public static string GREEN_CHECK_MARK_SYMBOL = ":white_check_mark:";

        /// <summary>Plugins that should show version output.</summary>
        public static string[] VERSION_PLUGINS = ["Citizens", "Denizen", "Depenizen", "Sentinel", "dDiscordBot"];

        /// <summary>The text that comes before the server version report.</summary>
        public const string SERVER_VERSION_PREFIX = "This server is running CraftBukkit version",
            SERVER_VERSION_PREFIX_BACKUP = "This server is running ";

        /// <summary>The text that comes before the server's bind address.</summary>
        public const string BIND_ADDRESS_PREFIX = "Starting Minecraft server on ";

        /// <summary>The text that comes before a player UUID post.</summary>
        public const string PLAYER_UUID_PREFIX = "UUID of player ";

        /// <summary>The text that indicates a server is in offline mode.</summary>
        public const string OFFLINE_NOTICE = "**** SERVER IS RUNNING IN OFFLINE/INSECURE MODE!";

        /// <summary>The prefix on the message before the startup Java version report produced by a few different sources, including Denizen and Sentinel.</summary>
        public const string STARTUP_JAVA_VERSION = " java version: ";

        /// <summary>Matcher for valid text in a player UUID.</summary>
        public static AsciiMatcher UUID_ASCII_MATCHER = new("0123456789abcdef-");

        /// <summary>A player UUID is always exactly 36 characters long.</summary>
        public const int UUID_LENGTH = 36;

        /// <summary>The version ID is always at index 14 of a UUID.</summary>
        public const int VERSION_ID_LOCATION = 14;

        /// <summary>
        /// Lowercase text that is suspicious (like ones that relate to cracked plugins).
        /// Map of text to messages.
        /// </summary>
        public static readonly Dictionary<string, string> SUSPICIOUS_TEXT = [];

        /// <summary>
        /// Lowercase text that usually is a bad sign.
        /// Map of text to messages.
        /// </summary>
        public static readonly Dictionary<string, string> DANGER_TEXT = [];

        /// <summary>
        /// Plugins that are suspicious (like ones that relate to cracked servers).
        /// Map of plugin names to messages.
        /// </summary>
        public static readonly Dictionary<string, string> SUSPICIOUS_PLUGINS = [];

        /// <summary>
        /// Plugins that WILL cause problems.
        /// Map of plugin names to messages.
        /// </summary>
        public static readonly Dictionary<string, string> BAD_PLUGINS = [];

        /// <summary>
        /// Plugins that MIGHT cause problems.
        /// Map of plugin names to messages.
        /// </summary>
        public static readonly Dictionary<string, string> MESSY_PLUGINS = [];

        /// <summary>
        /// Plugins that should be tracked but aren't a problem.
        /// Map of plugin names to messages.
        /// </summary>
        public static readonly Dictionary<string, string> MONITORED_PLUGINS = [];

        /// <summary>Adds a plugin by name to the list of special plugins to track and report.</summary>
        /// <param name="set">The set of plugins to add to.</param>
        /// <param name="message">The message to include, if any.</param>
        /// <param name="names">The name(s) of plugins to track.</param>
        public static void AddReportedEntry(Dictionary<string, string> set, string message, params string[] names)
        {
            foreach (string name in names)
            {
                set.Add(name, message);
            }
        }

        static LogChecker()
        {
            // Danger text
            AddReportedEntry(SUSPICIOUS_TEXT, $"{RED_FLAG_SYMBOL} Server is likely running cracked plugins.", "cracked by", "crack by", "cracked version", "blackspigot", "leaked by", "@bsmc", "directleaks", "leakmania", "mcleaks", "[LibsDisguises] Registered to: 1592 ");
            AddReportedEntry(DANGER_TEXT, $"{WARNING_SYMBOL} NEVER reload your server. If you change plugin files, you MUST RESTART your server properly.", "issued server command: /reload", "issued server command: /rl", ": reload complete.");
            AddReportedEntry(DANGER_TEXT, $"{WARNING_SYMBOL} Free server providers cannot be properly supported. Refer to <https://wiki.citizensnpcs.co/Frequently_Asked_Questions#I_have_a_free_server_.28Aternos.2C_Minehut.2C_....29_but_there.27s_problems>.", "minehut", "aternos");
            AddReportedEntry(DANGER_TEXT, $"{WARNING_SYMBOL} You should not have the CitizensAPI in your plugins folder, you only need the Citizens jar itself.", "could not load 'plugins/citizensapi");
            AddReportedEntry(DANGER_TEXT, $"{WARNING_SYMBOL} Log contains error messages.", "caused by: ", "[server thread/error]: ");
            AddReportedEntry(DANGER_TEXT, $"{WARNING_SYMBOL}{WARNING_SYMBOL} Server is likely infected with malware! Detectable via 'javassist' related errors. You must delete all server and plugin jars and reinstall from known-good copies, and should run a malware scan of your PC and server. {WARNING_SYMBOL}{WARNING_SYMBOL}", "class javassist.f from", "is not assignable to 'javassist/ctclass'");
            // Plugins
            AddReportedEntry(SUSPICIOUS_PLUGINS, $"{RED_FLAG_SYMBOL} **(Offline login authenticator plugin)**", "AuthMe", "UTitleAuth", "LoginSecurity", "nLogin", "PinAuthentication", "LockLogin", "JPremium", "FastLogin", "AmkMcAuth", "RoyalAuth", "JAuth", "AdvancedLogin", "OpeNLogin", "NexAuth", "Authy", "PasswordLogOn");
            AddReportedEntry(SUSPICIOUS_PLUGINS, $"{RED_FLAG_SYMBOL} **(Offline skins fixer plugin)**", "SkinsRestorer", "MySkin");
            AddReportedEntry(SUSPICIOUS_PLUGINS, $"{RED_FLAG_SYMBOL} **(Offline exploits fixer plugin)**", "AntiJoinBot", "AJB", "ExploitFixer", "AvakumAntibot", "HamsterAPI", "MineCaptcha", "UUIDSpoof-Fix", "AntiBotDeluxe", "nAntiBot", "LockProxy", "IPWhitelist");
            //AddReportedEntry(SUSPICIOUS_PLUGINS, $"{RED_FLAG_SYMBOL} **(Authentication breaker)**", "floodgate-bukkit", "floodgate", "BedrockPlayerManager");
            AddReportedEntry(SUSPICIOUS_PLUGINS, $"{RED_FLAG_SYMBOL} Fake online players (this is forbidden by Mojang)", "FakePlayersOnline");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} Plugin Managers are dangerous and will cause unpredictable issues. Remove it.", "PlugMan", "PluginManager", "PlugManX", "ServerUtils-Bukkit");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} NPC Command plugins have never had a valid reason to exist, as there have always been better ways to do that. The modern way is <https://wiki.citizensnpcs.co/NPC_Commands>.", "CommandNPC", "CitizensCMD", "NPCCommand");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} If you want NPCs that send players to other servers, check <https://wiki.citizensnpcs.co/NPC_Commands>.", "BungeeNPC", "CitizensServerSelector");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} To make NPCs speak, use '/npc text', or '/npc command', or Denizen. You don't need a dedicated text plugin for this.", "CitizensText");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} To give an NPC a hologram, just use the built in '/npc hologram' command, you don't need a separate plugin for this anymore.", "CitizensHologram");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} To use placeholders with Citizens, just use the normal commands. You don't need a separate plugin for this anymore.", "CitizensPlaceholderAPI");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} Messing with basic plugin core functionality can lead to unexpected issues.", "PerWorldPlugins");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} PvPManager is known to cause issues related to Citizens and Sentinel.", "PvPManager");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} Faking having real players online is expressly forbidden by Mojang (for obvious reasons) and can get you in trouble.", "FakePlayers", "FakePlayer", "FakePlayersOnline", "AutoFakePlayers", "XtremeSpoofer");
            AddReportedEntry(BAD_PLUGINS, $"- {WARNING_SYMBOL} ZNetwork has been caught including malware in their plugins: <https://gist.github.com/mergu/62f46ed15bd60e78eeb305ee38ed80f0>", "ServersNPC", "ZNPCs");
            AddReportedEntry(BAD_PLUGINS, "- Bedrock clients are unsupportable. Please do all testing with a Java Edition client.", "Geyser", "Geyser-Spigot", "floodgate-bukkit", "floodgate", "BedrockPlayerManager");
            AddReportedEntry(MESSY_PLUGINS, "- GadgetsMenu has been linked to compatibility issues with Citizens.", "GadgetsMenu");
            AddReportedEntry(MESSY_PLUGINS, "- Some scoreboard or name-edit plugins may lead to scoreboard control instability.", "FeatherBoard", "MVdWPlaceholderAPI", "AnimatedNames", "NametagEdit");
            AddReportedEntry(MESSY_PLUGINS, "- This plugin adds Below_Name scoreboards to NPCs.", "TAB");
            AddReportedEntry(MESSY_PLUGINS, "- Mixed client vs server versions can sometimes cause packet-related issues.", "ViaVersion", "ProtocolSupport");
            AddReportedEntry(MESSY_PLUGINS, "- HeadDatabase has been known to cause issues with skins.", "HeadDatabase");
            AddReportedEntry(MESSY_PLUGINS, "- CMI tends to mess with a large variety of server features and often gets in the way of issue debugging.", "CMI"); 
            AddReportedEntry(MESSY_PLUGINS, "- GriefDefender v2.2.2 has been seen to mistake NPCs for players in some cases, and may interfere with NPC teleporting.", "GriefDefender");
            AddReportedEntry(MESSY_PLUGINS, "- ModelEngine has Citizens support, but that support is known to be buggy. Issues related to NPCs that use ModelEngine should be reported to ModelEngine support, not Citizens.", "ModelEngine");
            AddReportedEntry(MESSY_PLUGINS, "- 'PlayerProfiles' has been seen to cause breaking issues with Citizens.", "PlayerProfiles");
            AddReportedEntry(MESSY_PLUGINS, "- Multi-world configuration plugins may affect NPCs in unexpected ways.", "Multiverse", "Multiverse-Core", "Universes");
            AddReportedEntry(MESSY_PLUGINS, "- 'Lag fix' plugins usually don't actually fix lag, they just delete spawned entities. If you have issues with entities disappearing, this plugin is likely the cause.", "ClearLagg", "LaggRemover");
            AddReportedEntry(MESSY_PLUGINS, "- This plugin has been known to break the plugin load order on many servers, due to usage of the 'loadbefore' directive in its 'plugin.yml'.", "FastAsyncWorldEdit", "SimplePets", "Enchantssquared", "Gringotts");
            AddReportedEntry(MESSY_PLUGINS, "- 'Sit on other players' or 'sit on mobs' plugins sometimes allow players to sit on NPCs.", "GSit");
            AddReportedEntry(MONITORED_PLUGINS, "", "WorldGuard", "MythicMobs", "NPC_Destinations", "NPCDestinations_Rancher", "NPCDestinations_Farmer", "NPCDestinations_Animator", "NPC_Police", "ProtocolLib", "Quests", "BeautyQuests");
        }

        /// <summary>Gets the text of a plugin name + version, if any.</summary>
        /// <param name="plugin">The plugin name to search for.</param>
        /// <returns>The plugin name + version, if any.</returns>
        public string GetPluginText(string plugin)
        {
            if (!IsDenizenDebug)
            {
                string result = GetFromTextTilEndOfLine(FullLogText, $"[{plugin}] Loading server plugin {plugin} v").After("Loading server plugin ");
                if (string.IsNullOrWhiteSpace(result))
                {
                    result = GetFromTextTilEndOfLine(FullLogText, $"[{plugin}] Loading {plugin} v").After("Loading ");
                }
                return result;
            }
            int start = FullLogText.IndexOf("\nActive Plugins (");
            int end = FullLogText.IndexOf("\nLoaded Worlds (");
            string pluginLine = FullLogText[start..end].Replace(((char)0x01) + "2", "").Replace(((char)0x01) + "4", "");
            int pluginNameIndex = pluginLine.IndexOf(plugin + ":");
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
            return pluginLine[pluginNameIndex..endIndex];
        }

        /// <summary>
        /// Utility to get the text starting at the input text, going to the end of the line.
        /// Returns an empty string if nothing is found.
        /// </summary>
        public static string GetFromTextTilEndOfLine(string fullLog, string text, int minIndex, out int foundIndex)
        {
            foundIndex = fullLog.IndexOf(text, minIndex);
            if (foundIndex == -1)
            {
                return "";
            }
            int endIndex = fullLog.IndexOf('\n', foundIndex);
            if (endIndex == -1)
            {
                endIndex = fullLog.Length;
            }
            return fullLog[foundIndex..endIndex];
        }

        /// <summary>
        /// Utility to get the text starting at the input text, going to the end of the line.
        /// Returns an empty string if nothing is found.
        /// </summary>
        public static string GetFromTextTilEndOfLine(string fullLog, string text)
        {
            return GetFromTextTilEndOfLine(fullLog, text, 0, out _);
        }

        /// <summary>Utility to limit the length of a string for stabler output.</summary>
        public static string LimitStringLength(string input, int maxLength, int cutLength)
        {
            if (input.Length > maxLength)
            {
                return input[..cutLength] + "...";
            }
            return input;
        }

        /// <summary>Escapes text for Discord output within a code block.</summary>
        /// <param name="text">The text to escape.</param>
        /// <returns>The escaped text.</returns>
        public static string Escape(string text)
        {
            return text.Replace('`', '\'');
        }

        /// <summary>Checks the value as not null or whitespace, then adds it to the embed as an inline field in a code block with a length limit applied.</summary>
        public static void AutoField(EmbedBuilder builder, string key, string value, bool inline = true)
        {
            if (builder.Length + Math.Min(value.Length, 850) + key.Length > 3600)
            {
                return;
            }
            if (!string.IsNullOrWhiteSpace(value))
            {
                value = LimitStringLength(value, 850, 800);
                builder.AddField(key, value, inline);
            }
        }

        /// <summary>The full log text.</summary>
        public string FullLogText;

        /// <summary>The full log text, lowercased.</summary>
        public string FullLogTextLower;

        /// <summary>The server version (if any).</summary>
        public string ServerVersion = "";

        /// <summary>Any potentially problematic plugins found in the log.</summary>
        public string SuspiciousPlugins = "";

        /// <summary>Any often plugins found in the log.</summary>
        public string BadPlugins = "";

        /// <summary>Any sometimes-conflictive plugins found in the log.</summary>
        public string IffyPlugins = "";

        /// <summary>Any other relevant plugins found in the log.</summary>
        public string OtherPlugins = "";

        /// <summary>Plugins whose versions will be listed.</summary>
        public string PluginVersions = "";

        /// <summary>Lines that are suspicious, usually ones that indicate cracked plugins.</summary>
        public List<string> SuspiciousLines = [];

        /// <summary>Lines of note, usually ones that indicate a bad sign.</summary>
        public List<string> OtherNoteworthyLines = [];

        /// <summary>Whether this server log appears to be offline mode.</summary>
        public bool IsOffline = false;

        /// <summary>Whether this server mentions a proxy like Bungee or Velocity (and thus might not actually be offline).</summary>
        public bool IsProxied = false;

        /// <summary>Visible UUID version (if anny).</summary>
        public int? UUIDVersion = null;

        /// <summary>Whether this looks like a Denizen debug log.</summary>
        public bool IsDenizenDebug = false;

        /// <summary>What Java version is running on this server (if findable).</summary>
        public string JavaVersion = "";

        /// <summary>Whether the server is likely to be offline.</summary>
        public bool LikelyOffline
        {
            get
            {
                if (UUIDVersion != null)
                {
                    if (UUIDVersion == 4)
                    {
                        return false;
                    }
                    if (UUIDVersion == 3 || UUIDVersion == 0)
                    {
                        return true;
                    }
                }
                if (IsOffline && !IsProxied)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>Construct the log checker system.</summary>
        /// <param name="fullLog">The full log text.</param>
        public LogChecker(string fullLog)
        {
            if (fullLog.Length > 1024 * 1024)
            {
                fullLog = fullLog[..(1024 * 1024)];
            }
            fullLog = fullLog.Replace("\r\n", "\n").Replace('\r', '\n');
            IEnumerable<string> lines = fullLog.Split('\n').Where(l => !l.EndsWith("Initializing Legacy Material Support. Unless you have legacy plugins and/or data this is a bug!"));
            FullLogText = string.Join('\n', lines.ToArray());
            FullLogTextLower = FullLogText.ToLowerFast();
        }

        /// <summary>Gathers the most basic information from the log.</summary>
        public void ProcessBasics()
        {
            if (IsDenizenDebug)
            {
                string mode = GetFromTextTilEndOfLine(FullLogText, "Mode: ");
                IsProxied = mode.Contains("BungeeCord") || mode.Contains("Velocity");
                IsOffline = mode.Contains("offline");
                ServerVersion = GetFromTextTilEndOfLine(FullLogText, "Server Version: ").After("Server Version: ");
                JavaVersion = GetFromTextTilEndOfLine(FullLogText, "Java Version: ").After("Java Version: ");
                string players = GetFromTextTilEndOfLine(FullLogText, "Total Players Ever: ").After("Total Players Ever: ");
                if (players.Contains(" v3"))
                {
                    UUIDVersion = 3;
                }
                else if (players.Contains(" v0"))
                {
                    UUIDVersion = 0;
                }
                else if (players.Contains(" normal"))
                {
                    UUIDVersion = 4;
                }
            }
            else
            {
                string rawMinusBungeeNotice = FullLogTextLower
                    .Replace("makes it possible to use bungeecord or velocity", "").Replace("compression from velocity", "").Replace("cipher from velocity", "") // Paper Velocity
                    .Replace("makes it possible to use bungeecord", "") // Spigot Bungee
                    .Replace("will not load bungee bridge.", "") // Depenizen
                    .Replace("  initializing bungeecord", "") // CMI
                    .Replace("such as BungeeCord/Spigot", "").Replace("setting is-bungeecord=true in", ""); // BuyCraftX
                IsProxied = rawMinusBungeeNotice.Contains("bungee") || rawMinusBungeeNotice.Contains("velocity");
                if (IsProxied && FullLogTextLower.Contains("bungee isn't enabled"))
                {
                    IsProxied = false;
                }
                IsOffline = FullLogText.Contains(OFFLINE_NOTICE);
                ServerVersion = GetFromTextTilEndOfLine(FullLogText, SERVER_VERSION_PREFIX);
                if (string.IsNullOrWhiteSpace(ServerVersion))
                {
                    ServerVersion = GetFromTextTilEndOfLine(FullLogText, SERVER_VERSION_PREFIX_BACKUP);
                }
                JavaVersion = GetFromTextTilEndOfLine(FullLogTextLower, STARTUP_JAVA_VERSION).After(STARTUP_JAVA_VERSION);
                if (string.IsNullOrWhiteSpace(JavaVersion))
                {
                    if (FullLogText.Contains("Use --illegal-access=warn to enable warnings of further illegal reflective access operations"))
                    {
                        JavaVersion = "11 (based on 'reflective access' message)";
                    }
                    else if (FullLogText.Contains("java.lang.Thread.run"))
                    {
                        if (FullLogText.Contains("java.lang.Thread.run(Thread.java:748) [?:1.8."))
                        {
                            JavaVersion = "8 (based on stack trace content)";
                        }
                        else if (FullLogText.Contains("java.base/java.lang.Thread.run(Thread.java:834)"))
                        {
                            JavaVersion = "11 (based on stack trace content)";
                        }
                        else if (FullLogText.Contains("java.base/java.lang.Thread.run(Thread.java:832)"))
                        {
                            JavaVersion = "14 or newer (based on stack trace content)";
                        }
                        else if (FullLogText.Contains("java.base/java.lang.Thread.run(Thread.java:833)"))
                        {
                            JavaVersion = "17 or newer (based on stack trace content)";
                        }
                    }
                }
                if (IsOffline)
                {
                    string bindAddress = GetFromTextTilEndOfLine(FullLogText, BIND_ADDRESS_PREFIX);
                    if (!string.IsNullOrWhiteSpace(bindAddress))
                    {
                        string bindAddrText = bindAddress.After(BIND_ADDRESS_PREFIX);
                        if (bindAddrText.StartsWith("0.0.0.0:") || bindAddrText.StartsWith("*:"))
                        {
                            OtherNoteworthyLines.Add($"`{Escape(bindAddress)}` - server is offline but has no address bind. This might mean you're using a system level firewall, but if not, it means your proxy is bypassable by hackers. Either enable a system level firewall, or bind your server to localhost in `server.properties` via `server-ip=127.0.0.1`.");
                        }
                    }
                }
            }
            Console.WriteLine($"Offline={IsOffline}, Proxied={IsProxied}");
            Console.WriteLine($"JavaVersion={JavaVersion}");
            ProcessJavaVersion();
            Console.WriteLine($"JavaVersionProcessed={JavaVersion}");
            Console.WriteLine($"ServerVersion={ServerVersion}");
            CheckServerVersion();
            Console.WriteLine($"ServerVersionChecked={ServerVersion}");
        }

        /// <summary>Processes and formats info about the Java version found in the log (if any).</summary>
        public void ProcessJavaVersion()
        {
            if (string.IsNullOrWhiteSpace(JavaVersion?.Trim()))
            {
                JavaVersion = "";
                return;
            }
            if (JavaVersion.StartsWith('8') || JavaVersion.StartsWith("1.8") || JavaVersion.StartsWith("16") || JavaVersion.StartsWith("17"))
            {
                JavaVersion = $"`{Escape(JavaVersion)}` {GREEN_CHECK_MARK_SYMBOL}";
                return;
            }
            JavaVersion = $"`{Escape(JavaVersion)}` {WARNING_SYMBOL} - Only Java versions 17, 16, and 8 are fully supported";
        }

        /// <summary>Gets the server version status result (outdated vs not) as a string.</summary>
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
                versionInput = versionInput["this server is running ".Length..];
            }
            string[] subData = versionInput.Split(' ', 4);
            if (subData.Length != 4 || subData[1] != "version" || subData[2].CountCharacter('-') < 2 || !subData[3].StartsWith("(mc: "))
            {
                Console.WriteLine("Server version doesn't match expected format, disregarding check.");
                return "";
            }
            string[] versionParts = subData[2].Split('-', 3);
            string spigotVersionText = versionParts[2];
            string mcVersionText = subData[3]["(mc: ".Length..].Before(')');
            string majorMCVersion = mcVersionText.CountCharacter('.') == 2 ? mcVersionText.BeforeLast('.') : mcVersionText;
            double versionNumb = DenizenMetaBot.VersionToDouble(majorMCVersion);
            if (versionNumb == -1)
            {
                Console.WriteLine($"Major MC version '{majorMCVersion}' is not a valid version string, disregarding check.");
                return "";
            }
            if (versionNumb < DenizenMetaBot.LowestServerVersion)
            {
                Console.WriteLine($"Major MC version {versionNumb} is less than minimum version {DenizenMetaBot.LowestServerVersion}, disregarding as outdated.");
                return $"{WARNING_SYMBOL} Outdated MC version";
            }
            if (versionNumb > DenizenMetaBot.HighestServerVersion)
            {
                Console.WriteLine($"Major MC version {versionNumb} is higher than minimum version {DenizenMetaBot.HighestServerVersion}, disregarding as too-new (config may need update?).");
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
                if (buildTracker.Value == 0)
                {
                    isGood = true;
                    return "(Version tracker error)";
                }
                if (buildTracker.IsCurrent(paperVersionNumber, out int behindBy))
                {
                    isGood = true;
                    return $"Current build {GREEN_CHECK_MARK_SYMBOL}";
                }
                else
                {
                    return (behindBy > 20 ? WARNING_SYMBOL + " " : "") + $"Outdated build, behind by {behindBy}... Current build is {buildTracker.Value}";
                }
            }
            else if (subData[0] == "spigot" || subData[0] == "craftbukkit")
            {
                if (versionParts[1] == "bukkit")
                {
                    return $"Bukkit is unsupported - use Spigot or Paper {WARNING_SYMBOL}";
                }
                spigotVersionText = spigotVersionText.Before('-');
                if (spigotVersionText.Length != 7 || !HEX_ASCII_MATCHER.IsOnlyMatches(spigotVersionText))
                {
                    Console.WriteLine($"Spigot version '{spigotVersionText}' is wrong format, disregarding check.");
                    return "";
                }
                /*
                int behind = BuildNumberTracker.GetSpigotVersionsBehindBy(spigotVersionText);
                if (behind == -1)
                {
                    Console.WriteLine($"Spigot version '{spigotVersionText}' is not tracked, disregarding check.");
                    return "";
                }
                if (behind == 0)
                {
                    isGood = true;
                    return $"Current build {GREEN_CHECK_MARK_SYMBOL}";
                }
                else
                {
                    return (behind > 20 ? WARNING_SYMBOL + " " : "") + $"Outdated build, behind by {behind}";
                }*/
            }
            else
            {
                Console.WriteLine($"Server type '{subData[0]}' is not managed, disregarding check.");
            }
            return "";
        }

        /// <summary>A matcher for hex characters: 0123456789abcdef.</summary>
        public static AsciiMatcher HEX_ASCII_MATCHER = new("0123456789abcdef");

        /// <summary>
        /// Checks the linked server version against the current known server version.
        /// Expects a version output of the format: "This server is running {TYPE} version git-{TYPE}-{VERS} (MC: {MCVERS}) (Implementing API version {MCVERS}-{SUBVERS}-SNAPSHOT)".
        /// Will note on outdated MCVERS (per config), or note on newer. Will identify an outdated Spigot (or paper) sub-version.
        /// </summary>
        public void CheckServerVersion()
        {
            if (string.IsNullOrWhiteSpace(ServerVersion))
            {
                Console.WriteLine("No server version, disregarding check.");
                return;
            }
            ServerVersion = ServerVersion.Replace('`', '\'');
            bool startsWithRunning = ServerVersion.StartsWith("This server is running ");
            if (!IsDenizenDebug && !startsWithRunning)
            {
                Console.WriteLine("Invalid server version, disregarding check.");
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
                ServerVersion = ServerVersion["This server is running ".Length..].BeforeLast(" (Implementing API version");
                ServerVersion = $"`{LimitStringLength(ServerVersion, 400, 350)}`";
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                ServerVersion += $"-- ({output})";
            }
        }

        /// <summary>Gathers a UUID version code if possible from the log.</summary>
        public void ProcessUUIDCheck()
        {
            int index = 0;
            while (index != -1)
            {
                index = UUIDCheckSingle(index);
                if (index != -1)
                {
                    index = FullLogText.IndexOf('\n', index);
                }
            }
        }

        private int UUIDCheckSingle(int minIndex)
        {
            string uuid;
            int result;
            if (IsDenizenDebug)
            {
                uuid = GetFromTextTilEndOfLine(FullLogText, "p@", minIndex, out result).After("p@");
            }
            else
            {
                uuid = GetFromTextTilEndOfLine(FullLogText, PLAYER_UUID_PREFIX, minIndex, out result).After(" is ");
            }
            if (uuid.Length >= UUID_LENGTH)
            {
                uuid = uuid[..UUID_LENGTH];
                Console.WriteLine($"Player UUID: {uuid}");
                if (UUID_ASCII_MATCHER.IsOnlyMatches(uuid))
                {
                    char versCode = uuid[VERSION_ID_LOCATION];
                    Console.WriteLine($"Player UUID version: {versCode}");
                    int newVers = versCode switch { '4' => 4, '3' => 3, _ => 0 };
                    if (UUIDVersion is null || newVers == 3 || (newVers == 0 && UUIDVersion == 4)) // Priority: 3, 0, 4, null
                    {
                        UUIDVersion = newVers;
                    }
                    return result;
                }
            }
            return -1;
        }

        /// <summary>Gathers data about plugins loading from the log.</summary>
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
                        string resultText;
                        if (build.IsCurrent(buildNum, out int behindBy))
                        {
                            resultText = build.Value == 0 ? "Version tracker error" : $"Current build {GREEN_CHECK_MARK_SYMBOL}";
                        }
                        else
                        {
                            resultText = (behindBy > build.MaxBehind ? WARNING_SYMBOL : "") + $"**Outdated build**, behind by {behindBy}";
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
            ProcessPluginSet(SUSPICIOUS_PLUGINS, ref SuspiciousPlugins, "Dangerous/Suspicious/Bad");
            ProcessPluginSet(BAD_PLUGINS, ref BadPlugins, "Bad");
            ProcessPluginSet(MESSY_PLUGINS, ref IffyPlugins, "Iffy/Messy");
            ProcessPluginSet(MONITORED_PLUGINS, ref OtherPlugins, "Monitored/Noteworthy");
        }

        /// <summary>Processes a set of monitored plugins, adding them to the output string if found in the log.</summary>
        /// <param name="pluginSet">The plugins to track.</param>
        /// <param name="listOutput">The output string to append to.</param>
        /// <param name="type">The category of plugin being checked for.</param>
        public void ProcessPluginSet(Dictionary<string, string> pluginSet, ref string listOutput, string type)
        {
            string lastmessage = "";
            foreach ((string plugin, string notice) in pluginSet)
            {
                string pluginLoadText = Escape(GetPluginText(plugin));
                if (pluginLoadText.Length != 0)
                {
                    Console.WriteLine($"{type} Plugin: {pluginLoadText}");
                    string message = "";
                    if (lastmessage != notice)
                    {
                        message = notice;
                        lastmessage = message;
                    }
                    string toOutput = string.IsNullOrWhiteSpace(message) ? $"`{pluginLoadText}`\n" : $"`{pluginLoadText}` {message}\n";
                    if (listOutput.Length + toOutput.Length < 730)
                    {
                        listOutput += toOutput;
                    }
                    else if (listOutput.Length + plugin.Length < 730)
                    {
                        listOutput += $"`{plugin}`\n";
                    }
                }
            }
        }

        /// <summary>Looks for dangerous text sometimes found in logs.</summary>
        public void ProcessDangerText(Dictionary<string, string> textMap, List<string> lineList)
        {
            HashSet<string> messagesUsed = [];
            foreach ((string sign, string message) in textMap)
            {
                int signIndex = FullLogTextLower.IndexOf(sign);
                if (signIndex >= 0 && !messagesUsed.Contains(message))
                {
                    int lineStart = FullLogText.LastIndexOf('\n', signIndex) + 1;
                    int lineEnd = FullLogText.IndexOf('\n', signIndex);
                    if (lineEnd == -1)
                    {
                        lineEnd = FullLogText.Length;
                    }
                    string dangerousLine = Escape(FullLogText[lineStart..lineEnd]);
                    Console.WriteLine($"Dangerous Text: {dangerousLine}");
                    lineList.Add($"`{dangerousLine}` {message}");
                    messagesUsed.Add(message);
                }
            }
        }

        /// <summary>Performs a test to see if this paste is a Denizen debug log. If it is, <see cref="IsDenizenDebug"/> will be set to true.</summary>
        public void TestForDenizenDebug()
        {
            if (!FullLogText.StartsWith("Java Version: "))
            {
                return;
            }
            if (!FullLogText.Contains("\nUp-time: ") || (!FullLogText.Contains("\nServer Version: ") && !FullLogText.Contains("\nCraftBukkit Version: "))
                || !FullLogText.Contains("\nDenizen Version: ") || !FullLogText.Contains("\nActive Plugins (")
                || !FullLogText.Contains("\nLoaded Worlds (") || !FullLogText.Contains("\nOnline Players (")
                || !FullLogText.Contains("\nMode: "))
            {
                return;
            }
            IsDenizenDebug = true;
        }

        /// <summary>Runs the log checker in full.</summary>
        public void Run()
        {
            Console.WriteLine("Running log check...");
            TestForDenizenDebug();
            ProcessBasics();
            ProcessUUIDCheck();
            ProcessPluginLoads();
            ProcessDangerText(DANGER_TEXT, OtherNoteworthyLines);
            ProcessDangerText(SUSPICIOUS_TEXT, SuspiciousLines);
        }

        /// <summary>Gets an output embed result.</summary>
        public EmbedBuilder GetResult(Action<EmbedBuilder> addFields)
        {
            string icon;
            if (UUIDVersion == 3 || (UUIDVersion == null && SuspiciousPlugins.Length > 0) || SuspiciousLines.Count > 0)
            {
                icon = Constants.RED_FLAG_ICON;
            }
            else if (LikelyOffline || (OtherNoteworthyLines.Count > 0) || (BadPlugins.Length + SuspiciousPlugins.Length > 0) || ServerVersion.Contains(WARNING_SYMBOL) || PluginVersions.Contains(WARNING_SYMBOL))
            {
                icon = Constants.WARNING_ICON;
            }
            else
            {
                icon = Constants.INFO_ICON;
            }
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Log Check Results").WithThumbnailUrl(icon);
            AutoField(embed, "Server Version", ServerVersion, inline: false);
            AutoField(embed, "Plugin Version(s)", PluginVersions, inline: false);
            addFields?.Invoke(embed);
            if (IsOffline)
            {
                AutoField(embed, "Online/Offline", IsProxied ? "Offline, but proxied." : (IsDenizenDebug ? $"{RED_FLAG_SYMBOL} Offline." : (UUIDVersion == 4 ? "Offline (proxy likely)." : "Offline (proxy status unknown).")));
            }
            if (UUIDVersion != null)
            {
                string description = UUIDVersion switch { 4 => $"{GREEN_CHECK_MARK_SYMBOL} Online", 3 => $"{RED_FLAG_SYMBOL} Offline", _ => $"{RED_FLAG_SYMBOL} Hacked or Invalid ID" };
                AutoField(embed, "UUID Version", $"{UUIDVersion} ({description})");
            }
            AutoField(embed, "Java Version", JavaVersion);
            AutoField(embed, "Other Noteworthy Plugin(s)", string.Join(", ", OtherPlugins.Split('\n', StringSplitOptions.RemoveEmptyEntries)), inline: false);
            AutoField(embed, "Suspicious Line(s)", string.Join('\n', SuspiciousLines), inline: false);
            AutoField(embed, "Suspicious Plugin(s)", SuspiciousPlugins, inline: false);
            AutoField(embed, "Problematic Plugin(s)", BadPlugins, inline: false);
            AutoField(embed, "Possibly Relevant Plugin(s)", IffyPlugins, inline: false);
            AutoField(embed, "Potentially Bad Line(s)", string.Join('\n', OtherNoteworthyLines), inline: false);
            return embed;
        }
    }
}
