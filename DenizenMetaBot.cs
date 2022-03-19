using System;
using System.Text;
using System.IO;
using System.Linq;
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
using DenizenBot.CommandHandlers;
using DenizenBot.UtilityProcessors;
using DenizenBot.HelperClasses;
using DiscordBotBase;
using DiscordBotBase.CommandHandlers;
using SharpDenizenTools.MetaHandlers;

namespace DenizenBot
{
    /// <summary>Discord bot for Denizen 1.x (Bukkit) help.</summary>
    public class DenizenMetaBot
    {
        /// <summary>Returns whether a Discord user is a bot commander (via role check).</summary>
        public static bool IsBotCommander(IUser user)
        {
            return (user as SocketGuildUser).Roles.Any((role) => role.Name.ToLowerInvariant() == "botcommander");
        }

        /// <summary>Returns whether meta commands are allowed in a given channel.</summary>
        /// <param name="channel">The channel to check.</param>
        /// <returns>True if allowed, false otherwise.</returns>
        public static bool MetaCommandsAllowed(IMessageChannel channel)
        {
            if (channel is SocketThreadChannel threadChannel)
            {
                channel = threadChannel.ParentChannel as IMessageChannel;
            }
            if (channel is null)
            {
                return false;
            }
            return ChannelToDetails.TryGetValue(channel.Id, out ChannelDetails details) && details.Docs;
        }

        /// <summary>Channels the bot will reply in.</summary>
        public static HashSet<ulong> ValidChannels = new(32);

        /// <summary>Informational replies available, as a map of name to full text.</summary>
        public static Dictionary<string, string> InformationalData = new(512);

        /// <summary>Informational replies names available, only including the primary names.</summary>
        public static List<string> InformationalDataNames = new(128);

        /// <summary>A mapping from channel IDs to project names for the update command.</summary>
        public static Dictionary<ulong, ChannelDetails> ChannelToDetails = new(512);

        /// <summary>A map of project names to the project's details.</summary>
        public static Dictionary<string, ProjectDetails> ProjectToDetails = new(512);

        /// <summary>A map of rule IDs to their text.</summary>
        public static Dictionary<string, string> Rules = new(128);

        /// <summary>A list of MC versions that are acceptable+known.</summary>
        public static List<string> AcceptableServerVersions = new();

        /// <summary>The lowest (oldest) acceptable server version, as a double.</summary>
        public static double LowestServerVersion = 0.0;

        /// <summary>The highest (newest) acceptable server version, as a double.</summary>
        public static double HighestServerVersion = 0.0;

        /// <summary>All quotes in the quotes file.</summary>
        public static string[] Quotes = Array.Empty<string>();

        /// <summary>All quotes in the quotes file, pre-lowercased for searching.</summary>
        public static string[] QuotesLower = Array.Empty<string>();

        /// <summary>The informational commands provider.</summary>
        public InformationCommands InfoCmds = new();

        /// <summary>URLs to send a POST to when reloading.</summary>
        public static string[] ReloadWebooks;

        /// <summary>All RSS trackers.</summary>
        public static List<RSSTracker> Trackers = new();

        /// <summary>Generates default command name->method pairs.</summary>
        void DefaultCommands(DiscordBot bot)
        {
            AdminCommands adminCmds = new() { Bot = bot };
            MetaCommands metaCmds = new() { Bot = bot };
            UtilityCommands utilCmds = new() { Bot = bot };
            CoreCommands coreCmds = new(IsBotCommander) { Bot = bot };
            // Informational
            bot.RegisterCommand(InfoCmds.CMD_Help, "help", "halp", "helps", "halps", "hel", "hal", "h");
            bot.RegisterCommand(InfoCmds.CMD_Hello, "hello", "hi", "hey", "source", "src");
            bot.RegisterCommand(InfoCmds.CMD_Info, "info", "notice", "alert");
            bot.RegisterCommand(InfoCmds.CMD_Update, "update", "latest", "current", "build", "builds", "download", "version");
            bot.RegisterCommand(InfoCmds.CMD_GitHub, "github", "git", "gh", "readme", "read", "link");
            bot.RegisterCommand(InfoCmds.CMD_Issues, "issues", "issue", "error", "ghissues", "githubissues");
            bot.RegisterCommand(InfoCmds.CMD_Rule, "rule", "rules");
            bot.RegisterCommand(InfoCmds.CMD_Quote, "quote", "quotes", "q");
            // Meta Docs
            bot.RegisterCommand(metaCmds.CMD_Command, "command", "commands", "cmd", "cmds", "c");
            bot.RegisterCommand(metaCmds.CMD_Mechanism, "mechanism", "mechanisms", "mech", "mechs", "mec", "mecs", "m");
            bot.RegisterCommand(metaCmds.CMD_Tag, "tag", "tags", "t");
            bot.RegisterCommand(metaCmds.CMD_ObjectTypes, "objecttype", "objecttypes", "objtype", "objtypes", "otype", "otypes", "ot", "type", "object", "objects", "types");
            bot.RegisterCommand(metaCmds.CMD_Event, "event", "events", "evt", "evts", "e");
            bot.RegisterCommand(metaCmds.CMD_Action, "action", "actions", "act", "acts", "a");
            bot.RegisterCommand(metaCmds.CMD_Language, "language", "languages", "lang", "langs", "l");
            bot.RegisterCommand(metaCmds.CMD_Guide, "guide", "guides", "g", "beginner", "beginners", "beginnersguide", "guidepage", "guidespage");
            bot.RegisterCommand(metaCmds.CMD_Search, "search", "s", "find", "f", "get", "locate", "what", "where", "how",
                "w", "meta", "metainfo", "docs", "doc", "documentation", "documentations", "document", "documents");
            // Utility
            bot.RegisterCommand(utilCmds.CMD_LogCheck, "logcheck", "checklog", "logscheck", "checklogs");
            bot.RegisterCommand(utilCmds.CMD_VersionCheck, "versioncheck", "checkversion", "iscurrent", "isuptodate", "isupdated", "checkcurrent", "currentcheck");
            bot.RegisterCommand(utilCmds.CMD_ScriptCheck, "script", "scriptcheck", "dscript", "ds", "checkscript", "dscriptcheck", "checkdscript");
            // Admin
            bot.RegisterCommand(coreCmds.CMD_Restart, "restart");
            bot.RegisterCommand(adminCmds.CMD_Reload, "reload");
            // --------------
            // Slash commands
            // --------------
            // Informational
            bot.RegisterSlashCommand(InfoCmds.SlashCMD_Info, "info");
        }

        /// <summary>Converts a Minecraft version string to a double for comparison reasons. Returns -1 if unparsable.</summary>
        public static double VersionToDouble(string version)
        {
            double scale = 1;
            double result = 0;
            foreach (string part in version.Split('.'))
            {
                if (!double.TryParse(part, out double partValue))
                {
                    return -1;
                }
                result += partValue * scale;
                scale *= 0.01;
            }
            return result;
        }

        /// <summary>Fills fields with data from the config file.</summary>
        public void PopulateFromConfig(FDSSection configFile)
        {
            ValidChannels.Clear();
            InformationalData.Clear();
            InformationalDataNames.Clear();
            ChannelToDetails.Clear();
            ProjectToDetails.Clear();
            Rules.Clear();
            AcceptableServerVersions.Clear();
            BuildNumberTracker.Clear();
            DenizenMetaBotConstants.DOCS_URL_BASE = configFile.GetString("url_base");
            DenizenMetaBotConstants.COMMAND_PREFIX = configFile.GetString("command_prefix");
            DiscordBotBaseHelper.CurrentBot.ClientConfig.CommandPrefix = DenizenMetaBotConstants.COMMAND_PREFIX;
            if (configFile.HasKey("valid_channels"))
            {
                foreach (string channel in configFile.GetStringList("valid_channels"))
                {
                    ValidChannels.Add(ulong.Parse(channel.Trim()));
                }
            }
            if (configFile.HasKey("info_replies"))
            {
                FDSSection infoSection = configFile.GetSection("info_replies");
                foreach (string key in infoSection.GetRootKeys())
                {
                    string infoValue = infoSection.GetRootData(key).AsString;
                    string[] keysSplit = key.SplitFast(',');
                    InformationalDataNames.Add(keysSplit[0]);
                    foreach (string name in keysSplit)
                    {
                        InformationalData[name.Trim()] = infoValue;
                    }
                }
            }
            if (configFile.HasKey("project_details"))
            {
                FDSSection projectDetailsSection = configFile.GetSection("project_details");
                foreach (string key in projectDetailsSection.GetRootKeys())
                {
                    FDSSection detailsSection = projectDetailsSection.GetSection(key);
                    ProjectDetails detail = new()
                    {
                        Name = key,
                        Icon = detailsSection.GetString("icon", ""),
                        GitHub = detailsSection.GetString("github", ""),
                        UpdateMessage = detailsSection.GetString("update", "")
                    };
                    ProjectToDetails.Add(key.ToLowerFast(), detail);
                }
            }
            if (configFile.HasKey("channel_details"))
            {
                FDSSection channelDetailsSection = configFile.GetSection("channel_details");
                foreach (string key in channelDetailsSection.GetRootKeys())
                {
                    FDSSection detailsSection = channelDetailsSection.GetSection(key);
                    ChannelDetails detail = new();
                    List<ProjectDetails> projects = new();
                    foreach (string projName in detailsSection.GetString("updates", "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        projects.Add(ProjectToDetails[projName]);
                    }
                    detail.Updates = projects.ToArray();
                    detail.Docs = detailsSection.GetBool("docs", false).Value;
                    ChannelToDetails.Add(ulong.Parse(key), detail);
                }
            }
            if (configFile.HasKey("rules"))
            {
                FDSSection rulesSection = configFile.GetSection("rules");
                foreach (string rule in rulesSection.GetRootKeys())
                {
                    Rules.Add(rule, rulesSection.GetString(rule));
                }
            }
            if (configFile.HasKey("build_numbers"))
            {
                FDSSection buildNumbersSection = configFile.GetSection("build_numbers");
                foreach (string projectName in buildNumbersSection.GetRootKeys())
                {
                    FDSSection project = buildNumbersSection.GetSection(projectName);
                    BuildNumberTracker.AddTracker(project.GetString("name"), project.GetString("regex"), project.GetString("jenkins_job"), project.GetInt("max_behind", 15).Value);
                }
            }
            if (configFile.HasKey("acceptable_server_versions"))
            {
                AcceptableServerVersions = configFile.GetStringList("acceptable_server_versions");
                foreach (string version in AcceptableServerVersions)
                {
                    double versionNumber = VersionToDouble(version);
                    if (versionNumber == -1)
                    {
                        Console.WriteLine($"Invalid version number '{version}' in config acceptable_server_versions");
                    }
                    if (LowestServerVersion <= 0.01 || versionNumber < LowestServerVersion)
                    {
                        LowestServerVersion = versionNumber;
                    }
                    if (versionNumber > HighestServerVersion)
                    {
                        HighestServerVersion = versionNumber;
                    }
                    BuildNumberTracker.AddPaperTracker(version);
                }
            }
            if (configFile.HasKey("additional_meta_sources"))
            {
                MetaDocsLoader.SourcesToUse = MetaDocsLoader.SourcesToUse.JoinWith(configFile.GetStringList("additional_meta_sources")).Distinct().ToArray();
            }
            ReloadWebooks = Array.Empty<string>();
            if (configFile.HasKey("reload_webhooks"))
            {
                ReloadWebooks = configFile.GetStringList("reload_webhooks").ToArray();
            }
            if (File.Exists(DiscordBot.CONFIG_FOLDER + "quotes.txt"))
            {
                Quotes = File.ReadAllText(DiscordBot.CONFIG_FOLDER + "quotes.txt").Replace("\r", "").Replace('`', '\'').Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
                QuotesLower = Quotes.Select(s => s.ToLowerFast()).ToArray();
            }
            if (configFile.HasKey("rss_feeds"))
            {
                FDSSection feeds = configFile.GetSection("rss_feeds");
                foreach (string hook in feeds.GetRootKeys())
                {
                    FDSSection feed = feeds.GetSection(hook);
                    string url = feed.GetString("url");
                    List<ulong> channels = new();
                    foreach (string channel in feed.GetStringList("channels"))
                    {
                        channels.Add(ulong.Parse(channel.Trim()));
                    }
                    double minutes = feed.GetDouble("check_rate").Value;
                    RSSTracker tracker = new(url, channels.ToArray(), DiscordBotBaseHelper.CurrentBot.BotMonitor, TimeSpan.FromMinutes(minutes));
                    tracker.Start();
                    Trackers.Add(tracker);
                }
            }
        }

        /// <summary>Initializes the bot object, connects, and runs the active loop.</summary>
        public void InitAndRun(string[] args)
        {
            DiscordBotBaseHelper.StartBotHandler(args, new DiscordBotConfig()
            {
                CommandPrefix = DenizenMetaBotConstants.COMMAND_PREFIX,
                Initialize = (bot) =>
                {
                    PopulateFromConfig(bot.ConfigFile);
                    DefaultCommands(bot);
                    bot.Client.Ready += () =>
                    {
                        bot.Client.SetGameAsync("Type !help").Wait();
                        try
                        {
                            const string commandVersionFile = "./config/command_registered_version.dat";
                            const int commandVersion = 1;
                            int confVersion = bot.ConfigFile.GetInt("slash_cmd_version", 0).Value;
                            string fullVers = $"{commandVersion}_{confVersion}";
                            if (!File.Exists(commandVersionFile) || commandVersionFile != fullVers)
                            {
                                RegisterSlashCommands(bot);
                                File.WriteAllText(commandVersionFile, fullVers);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Failed to update slash commands: {ex}");
                        }
                        return Task.CompletedTask;
                    };
                },
                UnknownCommandHandler = (name, command) =>
                {
                    if (command.Message.MentionedUserIds.Contains(DiscordBotBaseHelper.CurrentBot.Client.CurrentUser.Id))
                    {
                        command.Message.Channel.SendMessageAsync(embed: UserCommands.GetErrorMessageEmbed("Unknown Command", "Unknown command. Consider the __**help**__ command?")).Wait();
                    }
                    else if (command.CleanedArguments.Length == 0 && InformationalData.ContainsKey(name.ToLowerFast()))
                    {
                        InfoCmds.CMD_Info(new CommandData() { Message = command.Message, CleanedArguments = new string[] { name.ToLowerFast() } });
                    }
                },
                ShouldPayAttentionToMessage = (message) =>
                {
                    return message.Channel is IGuildChannel;
                },
                OnShutdown = () =>
                {
                    foreach (RSSTracker tracker in Trackers)
                    {
                        tracker.CancelToken.Cancel();
                    }
                }
            });
        }

        /// <summary>Registers all applicable slash commands.</summary>
        public static void RegisterSlashCommands(DiscordBot bot)
        {
            List<ApplicationCommandProperties> cmds = new();
            if (InformationalDataNames.Any())
            {
                SlashCommandBuilder infoCommand = new SlashCommandBuilder().WithName("info").WithDescription("Shows an info-box message.")
                    .AddOption("info-type", ApplicationCommandOptionType.String, "The name of the info message to display.", isRequired: true, isAutocomplete: true)
                    .AddOption("user", ApplicationCommandOptionType.User, "(Optional) A user to ping the information to.", isRequired: false);
                cmds.Add(infoCommand.Build());
            }
            bot.Client.BulkOverwriteGlobalApplicationCommandsAsync(cmds.ToArray()).Wait();
            Console.WriteLine($"Registered slash commands: {string.Join(", ", cmds.Select(c => c.Name))}");
        }
    }
}
