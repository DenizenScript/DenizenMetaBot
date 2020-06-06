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

namespace DenizenBot
{
    /// <summary>
    /// Discord bot for Denizen 1.x (Bukkit) help.
    /// </summary>
    public class DenizenMetaBot
    {
        /// <summary>
        /// Returns whether a Discord user is a bot commander (via role check).
        /// </summary>
        public static bool IsBotCommander(IUser user)
        {
            return (user as SocketGuildUser).Roles.Any((role) => role.Name.ToLowerInvariant() == "botcommander");
        }

        /// <summary>
        /// Returns whether meta commands are allowed in a given channel.
        /// </summary>
        /// <param name="channel">The channel to check.</param>
        /// <returns>True if allowed, false otherwise.</returns>
        public static bool MetaCommandsAllowed(IMessageChannel channel)
        {
            return ChannelToDetails.TryGetValue(channel.Id, out ChannelDetails details) && details.Docs;
        }

        /// <summary>
        /// Channels the bot will reply in.
        /// </summary>
        public static HashSet<ulong> ValidChannels = new HashSet<ulong>(32);

        /// <summary>
        /// Informational replies available, as a map of name to full text.
        /// </summary>
        public static Dictionary<string, string> InformationalData = new Dictionary<string, string>(512);

        /// <summary>
        /// Informational replies names available, only including the primary names.
        /// </summary>
        public static List<string> InformationalDataNames = new List<string>(128);

        /// <summary>
        /// A mapping from channel IDs to project names for the update command.
        /// </summary>
        public static Dictionary<ulong, ChannelDetails> ChannelToDetails = new Dictionary<ulong, ChannelDetails>(512);

        /// <summary>
        /// A map of project names to the project's details.
        /// </summary>
        public static Dictionary<string, ProjectDetails> ProjectToDetails = new Dictionary<string, ProjectDetails>(512);

        /// <summary>
        /// A map of rule IDs to their text.
        /// </summary>
        public static Dictionary<string, string> Rules = new Dictionary<string, string>(128);

        /// <summary>
        /// A list of MC versions that are acceptable+known.
        /// </summary>
        public static List<string> AcceptableServerVersions = new List<string>();

        /// <summary>
        /// The lowest (oldest) acceptable server version, as a double.
        /// </summary>
        public static double LowestServerVersion = 0.0;

        /// <summary>
        /// The highest (newest) acceptable server version, as a double.
        /// </summary>
        public static double HighestServerVersion = 0.0;

        /// <summary>
        /// All quotes in the quotes file.
        /// </summary>
        public static string[] Quotes = new string[0];

        /// <summary>
        /// All quotes in the quotes file, pre-lowercased for searching.
        /// </summary>
        public static string[] QuotesLower = new string[0];

        /// <summary>
        /// The informational commands provider.
        /// </summary>
        public InformationCommands InfoCmds = new InformationCommands();

        /// <summary>
        /// Generates default command name->method pairs.
        /// </summary>
        void DefaultCommands(DiscordBot bot)
        {
            AdminCommands adminCmds = new AdminCommands();
            MetaCommands metaCmds = new MetaCommands();
            UtilityCommands utilCmds = new UtilityCommands();
            CoreCommands coreCmds = new CoreCommands(IsBotCommander);
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
        }

        /// <summary>
        /// Fills fields with data from the config file.
        /// </summary>
        public void PopulateFromConfig(FDSSection configFile)
        {
            ValidChannels.Clear();
            InformationalData.Clear();
            InformationalDataNames.Clear();
            ChannelToDetails.Clear();
            ProjectToDetails.Clear();
            Rules.Clear();
            AcceptableServerVersions.Clear();
            DenizenMetaBotConstants.DOCS_URL_BASE = configFile.GetString("url_base");
            DenizenMetaBotConstants.COMMAND_PREFIX = configFile.GetString("command_prefix");
            foreach (string channel in configFile.GetStringList("valid_channels"))
            {
                ValidChannels.Add(ulong.Parse(channel.Trim()));
            }
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
            FDSSection projectDetailsSection = configFile.GetSection("project_details");
            foreach (string key in projectDetailsSection.GetRootKeys())
            {
                FDSSection detailsSection = projectDetailsSection.GetSection(key);
                ProjectDetails detail = new ProjectDetails
                {
                    Name = key,
                    Icon = detailsSection.GetString("icon", ""),
                    GitHub = detailsSection.GetString("github", ""),
                    UpdateMessage = detailsSection.GetString("update", "")
                };
                ProjectToDetails.Add(key.ToLowerFast(), detail);
            }
            FDSSection channelDetailsSection = configFile.GetSection("channel_details");
            foreach (string key in channelDetailsSection.GetRootKeys())
            {
                FDSSection detailsSection = channelDetailsSection.GetSection(key);
                ChannelDetails detail = new ChannelDetails();
                List<ProjectDetails> projects = new List<ProjectDetails>();
                foreach (string projName in detailsSection.GetString("updates", "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    projects.Add(ProjectToDetails[projName]);
                }
                detail.Updates = projects.ToArray();
                detail.Docs = detailsSection.GetBool("docs", false).Value;
                ChannelToDetails.Add(ulong.Parse(key), detail);
            }
            FDSSection rulesSection = configFile.GetSection("rules");
            foreach (string rule in rulesSection.GetRootKeys())
            {
                Rules.Add(rule, rulesSection.GetString(rule));
            }
            BuildNumberTracker.Clear();
            FDSSection buildNumbersSection = configFile.GetSection("build_numbers");
            foreach (string projectName in buildNumbersSection.GetRootKeys())
            {
                FDSSection project = buildNumbersSection.GetSection(projectName);
                BuildNumberTracker.AddTracker(project.GetString("name"), project.GetString("regex"), project.GetString("jenkins_job"));
            }
            AcceptableServerVersions = configFile.GetStringList("acceptable_server_versions");
            foreach (string version in AcceptableServerVersions)
            {
                double versionNumber = double.Parse(version);
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
            BuildNumberTracker.LoadSpigotData();
            if (File.Exists(DiscordBot.CONFIG_FOLDER + "quotes.txt"))
            {
                Quotes = File.ReadAllText(DiscordBot.CONFIG_FOLDER + "quotes.txt").Replace("\r", "").Replace('`', '\'').Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
                QuotesLower = Quotes.Select(s => s.ToLowerFast()).ToArray();
            }
        }

        /// <summary>
        /// Initializes the bot object, connects, and runs the active loop.
        /// </summary>
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
                        return Task.CompletedTask;
                    };
                },
                UnknownCommandHandler = (command, args, message) =>
                {
                    if (message.MentionedUserIds.Contains(DiscordBotBaseHelper.CurrentBot.Client.CurrentUser.Id))
                    {
                        message.Channel.SendMessageAsync(embed: UserCommands.GetErrorMessageEmbed("Unknown Command", "Unknown command. Consider the __**help**__ command?")).Wait();
                    }
                    else if (args.Count == 0 && InformationalData.ContainsKey(command.ToLowerFast()))
                    {
                        InfoCmds.CMD_Info(new string[] { command.ToLowerFast() }, message);
                    }
                },
                ShouldPayAttentionToMessage = (message) =>
                {
                    return message.Channel is IGuildChannel;
                }
            });
        }
    }
}
