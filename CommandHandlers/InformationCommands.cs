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
using FreneticUtilities.FreneticToolkit;

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Commands that give basic information to the user.
    /// </summary>
    public class InformationCommands : UserCommands
    {
        /// <summary>
        /// Simple output string for basic info commands.
        /// </summary>
        public static string CmdsInfo =
                "`help` shows help output\n"
                + "`hello` shows a source code link\n"
                + "`info <name ...>` shows a prewritten informational notice reply\n"
                + "`update [project ...]` shows an update link for the named project(s)\n"
                + "`github [project ...]` shows a GitHub link for the named project(s)\n"
                + "`issues [project ...]` shows an issue posting link for the named project(s)\n"
                + "`rule [rule ...]` shows the identified rule\n"
                + "`quote [quote ...]` shows a random quote that matches the search (if any)\n";

        /// <summary>
        /// Simple output string for meta commands.
        /// </summary>
        public static string CmdsMeta =
                "`command [name] [usage/tags]` to search commands\n"
                + "`mechanism [name]` to search mechanisms\n"
                + "`tag [name]` to search tags\n"
                + "`event [name]` to search world script events\n"
                + "`action [name]` to search NPC assignment actions\n"
                + "`language [name]` to search language docs";

        /// <summary>
        /// Simple output string for utility commands.
        /// </summary>
        public static string CmdsUtility =
                "`logcheck <link>` gathers information from a server log paste\n"
                + "`versioncheck <version text>` checks whether a project version is current\n";

        /// <summary>
        /// Simple output string for admin commands.
        /// </summary>
        public static string CmdsAdmin =
                "`restart` restarts the bot\n"
                + "`reload` reloads the meta docs";

        /// <summary>
        /// User command to get help (shows a list of valid bot commands).
        /// </summary>
        public void CMD_Help(string[] cmds, SocketMessage message)
        {
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Bot Command Help");
            embed.AddField("**Available Informational Commands:**", CmdsInfo, true);
            embed.AddField("**Available Utility Commands:**", CmdsUtility, true);
            if (Bot.MetaCommandsAllowed(message.Channel))
            {
                embed.AddField("**Available Meta Docs Commands:**", CmdsMeta, true);
            }
            if (Bot.IsBotCommander(message.Author as SocketGuildUser))
            {
                embed.AddField("**Available Admin Commands:**", CmdsAdmin, true);
            }
            SendReply(message, embed.Build());
        }

        /// <summary>
        /// User command to say 'hello' and get a source link.
        /// </summary>
        public void CMD_Hello(string[] cmds, SocketMessage message)
        {
            SendReply(message, new EmbedBuilder().WithTitle("Hello").WithThumbnailUrl(Constants.DENIZEN_LOGO).WithUrl(Constants.SOURCE_CODE_URL)
                .WithDescription($"Hi! I'm a bot! Find my source code at {Constants.SOURCE_CODE_URL}").Build());
        }

        /// <summary>
        /// User command to see information on how to update projects.
        /// </summary>
        public void CMD_Update(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 0)
            {
                if (!Bot.ChannelToDetails.TryGetValue(message.Channel.Id, out ChannelDetails details) || details.Updates.Length == 0)
                {
                    SendErrorMessageReply(message, "Unknown input for Update command", "Please specify which project(s) you want the update link for.");
                    return;
                }
                foreach (ProjectDetails proj in details.Updates)
                {
                    SendReply(message, proj.GetUpdateEmbed());
                }
                return;
            }
            foreach (string projectName in cmds)
            {
                string projectNameLower = projectName.ToLowerFast();
                if (Bot.ProjectToDetails.TryGetValue(projectNameLower, out ProjectDetails detail))
                {
                    SendReply(message, detail.GetUpdateEmbed());
                }
                else
                {
                    string closeName = StringConversionHelper.FindClosestString(Bot.ProjectToDetails.Keys, projectName, 20);
                    SendErrorMessageReply(message, "Unknown project name for Update command", $"Unknown project name `{projectName.Replace('`', '\'')}`."
                         + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
                }
            }
        }

        /// <summary>
        /// User command to see a link to the GitHub.
        /// </summary>
        public void CMD_GitHub(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 0)
            {
                if (!Bot.ChannelToDetails.TryGetValue(message.Channel.Id, out ChannelDetails details) || details.Updates.Length == 0)
                {
                    SendErrorMessageReply(message, "Unknown input for GitHub command", "Please specify which project(s) you want the GitHub link for.");
                    return;
                }
                SendReply(message, details.Updates[0].GetGithubEmbed());
                return;
            }
            foreach (string projectName in cmds)
            {
                string projectNameLower = projectName.ToLowerFast();
                if (Bot.ProjectToDetails.TryGetValue(projectNameLower, out ProjectDetails detail))
                {
                    SendReply(message, detail.GetGithubEmbed());
                }
                else
                {
                    string closeName = StringConversionHelper.FindClosestString(Bot.ProjectToDetails.Keys, projectName, 20);
                    SendErrorMessageReply(message, "Unknown project name for GitHub command", $"Unknown project name `{projectName.Replace('`', '\'')}`."
                         + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
                }
            }
        }

        /// <summary>
        /// User command to see a link to the GitHub.
        /// </summary>
        public void CMD_Issues(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 0)
            {
                if (!Bot.ChannelToDetails.TryGetValue(message.Channel.Id, out ChannelDetails details) || details.Updates.Length == 0)
                {
                    SendErrorMessageReply(message, "Unknown input for Issues command", "Please specify which project(s) you want the Issues link for.");
                    return;
                }
                SendReply(message, details.Updates[0].GetIssuesEmbed());
                return;
            }
            foreach (string projectName in cmds)
            {
                string projectNameLower = projectName.ToLowerFast();
                if (Bot.ProjectToDetails.TryGetValue(projectNameLower, out ProjectDetails detail))
                {
                    SendReply(message, detail.GetIssuesEmbed());
                }
                else
                {
                    string closeName = StringConversionHelper.FindClosestString(Bot.ProjectToDetails.Keys, projectName, 20);
                    SendErrorMessageReply(message, "Unknown project name for Issues command", $"Unknown project name `{projectName.Replace('`', '\'')}`."
                         + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
                }
            }
        }

        /// <summary>
        /// User command to get some predefined informational output.
        /// </summary>
        public void CMD_Info(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "`!info <info item or 'list'>`");
                return;
            }
            if (cmds.Length > 5)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "Please request no more than 5 info items at a time.");
                return;
            }
            foreach (string searchRaw in cmds)
            {
                string commandSearch = searchRaw.ToLowerFast().Trim();
                if (commandSearch == "list")
                {
                    string fullList = "`" + string.Join("`, `", Bot.InformationalDataNames) + "`";
                    SendReply(message, new EmbedBuilder().WithThumbnailUrl(Constants.INFO_ICON).WithTitle("Available Info Names").WithDescription($"Available info names: {fullList}").Build());
                }
                else if (Bot.InformationalData.TryGetValue(commandSearch, out string infoOutput))
                {
                    infoOutput = infoOutput.Trim();
                    if (infoOutput.StartsWith("NO_BOX:"))
                    {
                        infoOutput = infoOutput.Substring("NO_BOX:".Length).Trim();
                        message.Channel.SendMessageAsync("+++ Info `" + commandSearch + "`: " + infoOutput);
                    }
                    else
                    {
                        SendReply(message, new EmbedBuilder().WithThumbnailUrl(Constants.INFO_ICON).WithTitle($"Info: {commandSearch}").WithDescription(infoOutput).Build());
                    }
                }
                else
                {
                    string closeName = StringConversionHelper.FindClosestString(Bot.InformationalData.Keys, commandSearch, 20);
                    SendErrorMessageReply(message, "Cannot Display Info", "Unknown info name." + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
                }
            }
        }

        /// <summary>
        /// User command to display a rule.
        /// </summary>
        public void CMD_Rule(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 0)
            {
                cmds = new string[] { "all" };
            }
            if (cmds.Length > 5)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "Please request no more than 5 rules at a time.");
                return;
            }
            foreach (string searchRaw in cmds)
            {
                string ruleSearch = searchRaw.ToLowerFast().Trim();
                if (Bot.Rules.TryGetValue(ruleSearch, out string ruleText))
                {
                    ruleText = ruleText.Trim();
                    SendReply(message, new EmbedBuilder().WithThumbnailUrl(Constants.INFO_ICON).WithTitle($"Rule {ruleSearch}").WithDescription(ruleText).Build());
                }
                else
                {
                    SendErrorMessageReply(message, "Cannot Display Rule", "Unknown rule ID.");
                }
            }
        }

        /// <summary>
        /// Dictionary of quotes recently seen to the time it was last seen, to reduce duplication.
        /// </summary>
        public Dictionary<int, DateTimeOffset> QuotesSeen = new Dictionary<int, DateTimeOffset>();

        private static readonly Random _Random = new Random();

        /// <summary>
        /// User command to display a quote.
        /// </summary>
        public void CMD_Quote(string[] cmds, SocketMessage message)
        {
            if (Bot.Quotes.Length == 0)
            {
                SendErrorMessageReply(message, "Quotes Unavailable", "This bot currently has no quotes configured.");
                return;
            }
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (KeyValuePair<int, DateTimeOffset> recentlySeen in new Dictionary<int, DateTimeOffset>(QuotesSeen))
            {
                if (Math.Abs(now.Subtract(recentlySeen.Value).TotalMinutes) > 5)
                {
                    QuotesSeen.Remove(recentlySeen.Key);
                }
            }
            void sendQuote(int quoteId)
            {
                QuotesSeen[quoteId] = now;
                //SendReply(message, new EmbedBuilder().WithThumbnailUrl(Constants.SPEECH_BUBBLE_ICON).WithTitle($"Quote #{quoteId + 1}").WithDescription($"```xml\n{Bot.Quotes[quoteId]}\n```").Build());
                message.Channel.SendMessageAsync($"+> Quote **{quoteId + 1}**:\n```xml\n{Bot.Quotes[quoteId]}\n```").Wait();
            }
            if (cmds.Length == 0)
            {
                int qid = 0;
                for (int i = 0; i < 15; i++)
                {
                    qid = _Random.Next(Bot.Quotes.Length);
                    if (!QuotesSeen.ContainsKey(qid))
                    {
                        break;
                    }
                }
                sendQuote(qid);
                return;
            }
            if (cmds.Length == 1 && int.TryParse(cmds[0].Replace("#", ""), out int id))
            {
                if (id < 1)
                {
                    id = 1;
                }
                else if (id > Bot.Quotes.Length)
                {
                    id = Bot.Quotes.Length;
                }
                sendQuote(id - 1);
                return;
            }
            List<int> matchedQuoteIDs = new List<int>(32);
            string rawInputLow = string.Join(" ", cmds).ToLowerInvariant();
            for (int i = 0; i < Bot.Quotes.Length; i++)
            {
                if (Bot.QuotesLower[i].Contains(rawInputLow))
                {
                    matchedQuoteIDs.Add(i);
                }
            }
            if (matchedQuoteIDs.Count == 0)
            {
                SendErrorMessageReply(message, "Quote Unknown", "No quote found for that search text.");
                return;
            }
            int matchId = matchedQuoteIDs[0];
            for (int i = 0; i < 15; i++)
            {
                int temp = _Random.Next(matchedQuoteIDs.Count);
                matchId = matchedQuoteIDs[temp];
                if (!QuotesSeen.ContainsKey(matchId))
                {
                    break;
                }
            }
            sendQuote(matchId);
        }
    }
}
