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
        /// Simple output string for general public commands.
        /// </summary>
        public static string CmdsHelp =
                "`help` shows help output\n"
                + "`hello` shows a source code link\n"
                + "`info <name>` shows a prewritten informational notice reply\n"
                + "`update [project ...]` shows an update link for the named project(s)\n"
                + "`github [project ...]` shows a GitHub link for the named project(s)\n"
                + "`issues [project ...]` shows an issue posting link for the named project(s)";

        /// <summary>
        /// Simple output string for meta commands.
        /// </summary>
        public static string CmdsMeta =
                "`command [name]` to search commands\n"
                + "`mechanism [name]` to search mechanisms\n"
                + "`tag [name]` to search tags\n"
                + "`event [name]` to search world script events\n"
                + "`action [name]` to search NPC assignment actions\n";

        /// <summary>
        /// Simple output string for admin commands.
        /// </summary>
        public static string CmdsAdminHelp =
                "`restart` restarts the bot\n"
                + "`reload` reloads the meta docs\n";

        /// <summary>
        /// User command to get help (shows a list of valid bot commands).
        /// </summary>
        public void CMD_Help(string[] cmds, SocketMessage message)
        {
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Bot Command Help");
            embed.AddField("**Available Commands:**", CmdsHelp, true);
            if (Bot.MetaCommandsAllowed(message.Channel))
            {
                embed.AddField("**Available Meta Docs Commands:**", CmdsMeta, true);
            }
            if (Bot.IsBotCommander(message.Author as SocketGuildUser))
            {
                embed.AddField("**Available Admin Commands:**", CmdsAdminHelp, true);
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
                    SendErrorMessageReply(message, "Unknown project name for Update command", "Unknown project name `" + projectName.Replace('`', '\'') + "`.");
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
                    SendErrorMessageReply(message, "Unknown project name for GitHub command", "Unknown project name `" + projectName.Replace('`', '\'') + "`.");
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
                    SendErrorMessageReply(message, "Unknown project name for Issues command", "Unknown project name `" + projectName.Replace('`', '\'') + "`.");
                }
            }
        }

        /// <summary>
        /// User command to get some predefined informational output.
        /// </summary>
        public void CMD_Info(string[] cmds, SocketMessage message)
        {
            if (cmds.Length != 1)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "`!info <info item or 'list'>`");
                return;
            }
            string commandSearch = cmds[0].ToLowerFast().Trim();
            if (commandSearch == "list")
            {
                string fullList = "`" + string.Join("`, `", Bot.InformationalDataNames) + "`";
                SendReply(message, new EmbedBuilder().WithThumbnailUrl(Constants.INFO_ICON).WithTitle("Available Info Names").WithDescription($"Available info names: {fullList}").Build());
            }
            else if (Bot.InformationalData.TryGetValue(commandSearch, out string infoOutput))
            {
                SendReply(message, new EmbedBuilder().WithThumbnailUrl(Constants.INFO_ICON).WithTitle($"Info: {commandSearch}").WithDescription(infoOutput).Build());
            }
            else
            {
                string closeName = StringConversionHelper.FindClosestString(Bot.InformationalData.Keys, commandSearch, 20);
                SendErrorMessageReply(message, "Cannot Display Info", "Unknown info name." + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
            }
        }
    }
}