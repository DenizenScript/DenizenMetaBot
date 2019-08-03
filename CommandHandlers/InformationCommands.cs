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
                "`help` shows help output, "
                + "`hello` shows a source code link, "
                + "`info <name>` shows a prewritten informational notice reply, "
                + "`update [project ...]` shows an update link for the named project(s), "
                + "`github [project ...]` shows a GitHub link for the named project(s), "
                + "`issues [project ...]` shows an issue posting link for the named project(s), "
                + "...";

        /// <summary>
        /// Simple output string for meta commands.
        /// </summary>
        public static string CmdsMeta =
                "`command [name]` to search commands, "
                + "...";

        /// <summary>
        /// Simple output string for admin commands.
        /// </summary>
        public static string CmdsAdminHelp =
                "`restart` restarts the bot, "
                + "...";

        /// <summary>
        /// User command to get help (shows a list of valid bot commands).
        /// </summary>
        public void CMD_Help(string[] cmds, SocketMessage message)
        {
            string outputMessage = "Available Commands: " + CmdsHelp;
            outputMessage += "\nAvailable Meta Docs Commands: " + CmdsMeta;
            if (Bot.IsBotCommander(message.Author as SocketGuildUser))
            {
                outputMessage += "\nAvailable admin commands: " + CmdsAdminHelp;
            }
            SendGenericPositiveMessageReply(message, "Bot Command Help", outputMessage);
        }

        /// <summary>
        /// User command to say 'hello' and get a source link.
        /// </summary>
        public void CMD_Hello(string[] cmds, SocketMessage message)
        {
            SendGenericPositiveMessageReply(message, "Hello", "Hi! I'm a bot! Find my source code at https://github.com/DenizenScript/DenizenMetaBot");
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
                SendErrorMessageReply(message, "Command Syntax Incorrect", "!info <info item or 'list'>");
                return;
            }
            string commandSearch = cmds[0].ToLowerFast().Trim();
            if (commandSearch == "list")
            {
                string fullList = "`" + string.Join("`, `", Bot.InformationalDataNames) + "`";
                SendGenericPositiveMessageReply(message, "Available Info Names", "Available info names: " + fullList);
            }
            else if (Bot.InformationalData.TryGetValue(commandSearch, out string infoOutput))
            {
                SendGenericPositiveMessageReply(message, "Info: " + commandSearch, infoOutput);
            }
            else
            {
                string closeName = StringConversionHelper.FindClosestString(Bot.InformationalData.Keys, commandSearch, 20);
                SendErrorMessageReply(message, "Cannot Display Info", "Unknown info name." + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
            }
        }
    }
}