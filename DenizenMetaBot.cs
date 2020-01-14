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

namespace DenizenBot
{
    /// <summary>
    /// Discord bot for Denizen 1.x (Bukkit) help.
    /// </summary>
    public class DenizenMetaBot
    {
        /// <summary>
        /// Configuration folder path.
        /// </summary>
        public const string CONFIG_FOLDER = "./config/";

        /// <summary>
        /// Bot token file path.
        /// </summary>
        public const string TOKEN_FILE = CONFIG_FOLDER + "token.txt";

        /// <summary>
        /// Configuration file path.
        /// </summary>
        public const string CONFIG_FILE = CONFIG_FOLDER + "config.fds";

        /// <summary>
        /// Bot token, read from config data.
        /// </summary>
        public static readonly string TOKEN = File.ReadAllText(TOKEN_FILE).Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Replace(" ", "");

        /// <summary>
        /// The configuration file section.
        /// </summary>
        public FDSSection ConfigFile;

        /// <summary>
        /// Internal Discord API bot Client handler.
        /// </summary>
        public DiscordSocketClient Client;

        /// <summary>
        /// Bot command response handler.
        /// </summary>
        public void Respond(SocketMessage message, bool outputUnknowns)
        {
            string messageText = message.Content;
            if (messageText.StartsWith(Constants.COMMAND_PREFIX))
            {
                messageText = messageText.Substring(Constants.COMMAND_PREFIX.Length);
            }
            string[] messageDataSplit = messageText.Split(' ');
            StringBuilder resultBuilder = new StringBuilder(messageText.Length);
            List<string> cmds = new List<string>();
            for (int i = 0; i < messageDataSplit.Length; i++)
            {
                if (messageDataSplit[i].Contains("<@") && messageDataSplit[i].Contains(">"))
                {
                    continue;
                }
                resultBuilder.Append(messageDataSplit[i]).Append(" ");
                if (messageDataSplit[i].Length > 0)
                {
                    cmds.Add(messageDataSplit[i]);
                }
            }
            if (cmds.Count == 0)
            {
                Console.WriteLine("Empty input, ignoring: " + message.Author.Username);
                return;
            }
            string fullMessageCleaned = resultBuilder.ToString();
            Console.WriteLine("Found input from: (" + message.Author.Username + "), in channel: " + message.Channel.Name + ": " + fullMessageCleaned);
            string commandNameLowered = cmds[0].ToLowerInvariant();
            cmds.RemoveAt(0);
            if (ChatCommands.TryGetValue(commandNameLowered, out Action<string[], SocketMessage> acto))
            {
                acto.Invoke(cmds.ToArray(), message);
            }
            else if (outputUnknowns)
            {
                message.Channel.SendMessageAsync(embed: UserCommands.GetErrorMessageEmbed("Unknown Command", "Unknown command. Consider the __**help**__ command?")).Wait();
            }
            else if (cmds.Count == 0 && InformationalData.ContainsKey(commandNameLowered))
            {
                InfoCmds.CMD_Info(new string[] { commandNameLowered }, message);
            }
        }

        /// <summary>
        /// All valid user commands in a map of typable command name -> command method.
        /// </summary>
        public readonly Dictionary<string, Action<string[], SocketMessage>> ChatCommands = new Dictionary<string, Action<string[], SocketMessage>>(1024);

        /// <summary>
        /// Returns whether a Discord user is a bot commander (via role check).
        /// </summary>
        public bool IsBotCommander(SocketGuildUser user)
        {
            return user.Roles.Any((role) => role.Name.ToLowerInvariant() == "botcommander");
        }
        
        /// <summary>
        /// Saves the config file.
        /// </summary>
        public void SaveConfig()
        {
            lock (ConfigSaveLock)
            {
                ConfigFile.SaveToFile(CONFIG_FILE);
            }
        }

        /// <summary>
        /// Lock object for config file saving/loading.
        /// </summary>
        public static Object ConfigSaveLock = new Object();

        /// <summary>
        /// Registers a command to a name and any number of aliases.
        /// </summary>
        public void RegisterCommand(Action<string[], SocketMessage> command, params string[] names)
        {
            foreach (string name in names)
            {
                ChatCommands.Add(name, command);
            }
        }

        /// <summary>
        /// The informational commands provider.
        /// </summary>
        public InformationCommands InfoCmds = new InformationCommands();

        /// <summary>
        /// Generates default command name->method pairs.
        /// </summary>
        void DefaultCommands()
        {
            InfoCmds.Bot = this;
            AdminCommands adminCmds = new AdminCommands() { Bot = this };
            MetaCommands metaCmds = new MetaCommands() { Bot = this };
            UtilityCommands utilCmds = new UtilityCommands() { Bot = this };
            // Informational
            RegisterCommand(InfoCmds.CMD_Help, "help", "halp", "helps", "halps", "hel", "hal", "h");
            RegisterCommand(InfoCmds.CMD_Hello, "hello", "hi", "hey", "source", "src");
            RegisterCommand(InfoCmds.CMD_Info, "info", "notice", "alert");
            RegisterCommand(InfoCmds.CMD_Update, "update", "latest", "current", "build", "builds", "download", "version");
            RegisterCommand(InfoCmds.CMD_GitHub, "github", "git", "gh", "readme", "read", "link");
            RegisterCommand(InfoCmds.CMD_Issues, "issues", "issue", "error", "ghissues", "githubissues");
            RegisterCommand(InfoCmds.CMD_Rule, "rule", "rules");
            // Meta Docs
            RegisterCommand(metaCmds.CMD_Command, "command", "commands", "cmd", "cmds", "c");
            RegisterCommand(metaCmds.CMD_Mechanism, "mechanism", "mechanisms", "mech", "mechs", "mec", "mecs", "m");
            RegisterCommand(metaCmds.CMD_Tag, "tag", "tags", "t");
            RegisterCommand(metaCmds.CMD_Event, "event", "events", "evt", "evts", "e");
            RegisterCommand(metaCmds.CMD_Action, "action", "actions", "act", "acts", "a");
            RegisterCommand(metaCmds.CMD_Language, "language", "languages", "lang", "langs", "l");
            RegisterCommand(metaCmds.CMD_Search, "search", "s", "find", "f", "get", "g", "locate", "what", "where", "how",
                "w", "meta", "metainfo", "docs", "doc", "documentation", "documentations", "document", "documents");
            // Utility
            // TODO: CMD_DScriptCheck
            RegisterCommand(utilCmds.CMD_LogCheck, "logcheck", "checklog", "logscheck", "checklogs");
            RegisterCommand(utilCmds.CMD_VersionCheck, "versioncheck", "checkversion", "iscurrent", "isuptodate", "isupdated", "checkcurrent", "currentcheck");
            // Admin
            RegisterCommand(adminCmds.CMD_Restart, "restart");
            RegisterCommand(adminCmds.CMD_Reload, "reload");
        }

        /// <summary>
        /// Shuts the bot down entirely.
        /// </summary>
        public void Shutdown()
        {
            Client.StopAsync().Wait();
            Client.Dispose();
            StoppedEvent.Set();
        }

        /// <summary>
        /// Returns whether meta commands are allowed in a given channel.
        /// </summary>
        /// <param name="channel">The channel to check.</param>
        /// <returns>True if allowed, false otherwise.</returns>
        public bool MetaCommandsAllowed(ISocketMessageChannel channel)
        {
            return ChannelToDetails.TryGetValue(channel.Id, out ChannelDetails details) && details.Docs;
        }

        /// <summary>
        /// Signaled when the bot is stopped.
        /// </summary>
        public ManualResetEvent StoppedEvent = new ManualResetEvent(false);

        /// <summary>
        /// Monitor object to help restart the bot as needed.
        /// </summary>
        public ConnectionMonitor BotMonitor;

        /// <summary>
        /// Channels the bot will reply in.
        /// </summary>
        public HashSet<ulong> ValidChannels = new HashSet<ulong>(32);

        /// <summary>
        /// Informational replies available, as a map of name to full text.
        /// </summary>
        public Dictionary<string, string> InformationalData = new Dictionary<string, string>(512);

        /// <summary>
        /// Informational replies names available, only including the primary names.
        /// </summary>
        public List<string> InformationalDataNames = new List<string>(128);

        /// <summary>
        /// A mapping from channel IDs to project names for the update command.
        /// </summary>
        public Dictionary<ulong, ChannelDetails> ChannelToDetails = new Dictionary<ulong, ChannelDetails>(512);

        /// <summary>
        /// A map of project names to the project's details.
        /// </summary>
        public Dictionary<string, ProjectDetails> ProjectToDetails = new Dictionary<string, ProjectDetails>(512);

        /// <summary>
        /// A map of rule IDs to their text.
        /// </summary>
        public Dictionary<string, string> Rules = new Dictionary<string, string>(128);

        /// <summary>
        /// A list of MC versions that are acceptable+known.
        /// </summary>
        public List<string> AcceptableServerVersions = new List<string>();

        /// <summary>
        /// The lowest (oldest) acceptable server version, as a double.
        /// </summary>
        public static double LowestServerVersion = 0.0;

        /// <summary>
        /// The highest (newest) acceptable server version, as a double.
        /// </summary>
        public static double HighestServerVersion = 0.0;

        /// <summary>
        /// Fills fields with data from the config file.
        /// </summary>
        public void PopulateFromConfig()
        {
            ValidChannels.Clear();
            Constants.DOCS_URL_BASE = ConfigFile.GetString("url_base");
            Constants.COMMAND_PREFIX = ConfigFile.GetString("command_prefix");
            foreach (string channel in ConfigFile.GetStringList("valid_channels"))
            {
                ValidChannels.Add(ulong.Parse(channel.Trim()));
            }
            FDSSection infoSection = ConfigFile.GetSection("info_replies");
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
            FDSSection projectDetailsSection = ConfigFile.GetSection("project_details");
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
            FDSSection channelDetailsSection = ConfigFile.GetSection("channel_details");
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
            FDSSection rulesSection = ConfigFile.GetSection("rules");
            foreach (string rule in rulesSection.GetRootKeys())
            {
                Rules.Add(rule, rulesSection.GetString(rule));
            }
            FDSSection buildNumbersSection = ConfigFile.GetSection("build_numbers");
            foreach (string projectName in buildNumbersSection.GetRootKeys())
            {
                FDSSection project = buildNumbersSection.GetSection(projectName);
                BuildNumberTracker.AddTracker(project.GetString("name"), project.GetString("regex"), project.GetString("jenkins_job"));
            }
            AcceptableServerVersions = ConfigFile.GetStringList("acceptable_server_versions");
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
        }

        /// <summary>
        /// Initializes the bot object, connects, and runs the active loop.
        /// </summary>
        public void InitAndRun(string[] args)
        {
            Console.WriteLine("Preparing...");
            BotMonitor = new ConnectionMonitor(this);
            if (File.Exists(CONFIG_FILE))
            {
                lock (ConfigSaveLock)
                {
                    ConfigFile = FDSUtility.ReadFile(CONFIG_FILE);
                }
            }
            PopulateFromConfig();
            DefaultCommands();
            Console.WriteLine("Loading Discord...");
            DiscordSocketConfig config = new DiscordSocketConfig
            {
                MessageCacheSize = 256
            };
            //config.LogLevel = LogSeverity.Debug;
            Client = new DiscordSocketClient(config);
            /*Client.Log += (m) =>
            {
                Console.WriteLine(m.Severity + ": " + m.Source + ": " + m.Exception + ": "  + m.Message);
                return Task.CompletedTask;
            };*/
            Client.Ready += () =>
            {
                if (BotMonitor.ShouldStopAllLogic())
                {
                    return Task.CompletedTask;
                }
                BotMonitor.ConnectedCurrently = true;
                Client.SetGameAsync("Type !help").Wait();
                if (BotMonitor.ConnectedOnce)
                {
                    return Task.CompletedTask;
                }
                Console.WriteLine($"Args: {args.Length}");
                if (args.Length > 0 && ulong.TryParse(args[0], out ulong argument1))
                {
                    ISocketMessageChannel channelToNotify = Client.GetChannel(argument1) as ISocketMessageChannel;
                    Console.WriteLine($"Restarted as per request in channel: {channelToNotify.Name}");
                    channelToNotify.SendMessageAsync(embed: UserCommands.GetGenericPositiveMessageEmbed("Restarted", "Connected and ready!")).Wait();
                }
                BotMonitor.ConnectedOnce = true;
                return Task.CompletedTask;
            };
            Client.MessageReceived += (message) =>
            {
                if (BotMonitor.ShouldStopAllLogic())
                {
                    return Task.CompletedTask;
                }
                if (message.Author.Id == Client.CurrentUser.Id)
                {
                    return Task.CompletedTask;
                }
                BotMonitor.LoopsSilent = 0;
                if (message.Author.IsBot || message.Author.IsWebhook)
                {
                    return Task.CompletedTask;
                }
                if (message.Channel.Name.StartsWith("@") || !(message.Channel is SocketGuildChannel sgc))
                {
                    Console.WriteLine($"Refused message from ({message.Author.Username}): (Invalid Channel: {message.Channel.Name}): {message.Content}");
                    return Task.CompletedTask;
                }
                if (ValidChannels.Count != 0 && !ValidChannels.Contains(message.Channel.Id))
                {
                    Console.WriteLine($"Refused message from ({message.Author.Username}): (Non-whitelisted Channel: {message.Channel.Name}): {message.Content}");
                    return Task.CompletedTask;
                }
                bool mentionedMe = message.MentionedUsers.Any((su) => su.Id == Client.CurrentUser.Id);
                Console.WriteLine($"Parsing message from ({message.Author.Username}), in channel: {message.Channel.Name}: {message.Content}");
                if (mentionedMe || message.Content.StartsWith(Constants.COMMAND_PREFIX))
                {
                    try
                    {
                        Respond(message, mentionedMe);
                    }
                    catch (Exception ex)
                    {
                        if (ex is ThreadAbortException)
                        {
                            throw;
                        }
                        Console.WriteLine($"Error handling command: {ex.ToString()}");
                    }
                }
                return Task.CompletedTask;
            };
            Console.WriteLine("Logging in to Discord...");
            Client.LoginAsync(TokenType.Bot, TOKEN).Wait();
            Console.WriteLine("Connecting to Discord...");
            Client.StartAsync().Wait();
            Console.WriteLine("Running Discord!");
            Console.WriteLine("Starting monitor...");
            BotMonitor.StartMonitorLoop();
            StoppedEvent.WaitOne();
        }
    }
}
