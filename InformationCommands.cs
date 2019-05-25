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

namespace DenizenBot
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
            if (Bot.IsBotCommander(message.Author as SocketGuildUser))
            {
                outputMessage += "\nAvailable admin commands: " + CmdsAdminHelp;
            }
            message.Channel.SendMessageAsync(SUCCESS_PREFIX + outputMessage).Wait();
        }

        /// <summary>
        /// User command to say 'hello' and get a source link.
        /// </summary>
        public void CMD_Hello(string[] cmds, SocketMessage message)
        {
            message.Channel.SendMessageAsync(SUCCESS_PREFIX + "Hi! I'm a bot! Find my source code at https://github.com/DenizenScript/DenizenMetaBot").Wait();
        }

        /// <summary>
        /// User command to see information on how to update projects.
        /// </summary>
        public void CMD_Update(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 0)
            {
                if (!Bot.ChannelToProject.TryGetValue(message.Channel.Id, out cmds))
                {
                    message.Channel.SendMessageAsync(embed: GetErrorMessageEmbed("Unknown input for Update command", "Please specify which project(s) you want the update link for.")).Wait();
                    return;
                }
            }
            StringBuilder toOutput = new StringBuilder();
            foreach (string projectName in cmds)
            {
                string projectNameLower = projectName.ToLowerFast();
                if (Bot.UpdateMessages.TryGetValue(projectNameLower, out string updateMessage))
                {
                    Embed embed = new EmbedBuilder().WithTitle("Update " + Bot.ProjectNames[projectNameLower]).WithColor(0, 255, 255)
                        .WithThumbnailUrl(Bot.ProjectImageUrls[projectNameLower]).WithDescription(updateMessage).Build();
                    message.Channel.SendMessageAsync(embed: embed).Wait();
                }
                else
                {
                    message.Channel.SendMessageAsync(embed: GetErrorMessageEmbed("Unknown project name for Update command", "Unknown project name `" + projectName.Replace('`', '\'') + "`.")).Wait();
                }
            }
        }

        /// <summary>
        /// User command to get some predefined informational output.
        /// </summary>
        public void CMD_Info(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 1)
            {
                string commandSearch = cmds[0].ToLowerFast().Trim();
                if (commandSearch == "list")
                {
                    string fullList = "`" + string.Join("`, `", Bot.InformationalDataNames) + "`";
                    message.Channel.SendMessageAsync(SUCCESS_PREFIX + "Available info names: " + fullList).Wait();
                }
                else if (Bot.InformationalData.TryGetValue(commandSearch, out string infoOutput))
                {
                    message.Channel.SendMessageAsync(SUCCESS_PREFIX + infoOutput).Wait();
                }
                else
                {
                    int lowestDistance = 20;
                    string lowestName = null;
                    foreach (string option in Bot.InformationalData.Keys)
                    {
                        int currentDistance = StringConversionHelper.GetLevenshteinDistance(commandSearch, option);
                        if (currentDistance < lowestDistance)
                        {
                            lowestDistance = currentDistance;
                            lowestName = option;
                        }
                    }
                    message.Channel.SendMessageAsync(REFUSAL_PREFIX + "Unknown info name. " + (lowestName == null ? "" : "Did you mean `" + lowestName + "`?")).Wait();
                }
            }
            else
            {
                message.Channel.SendMessageAsync(REFUSAL_PREFIX + "!info <info item or 'list'>").Wait();
            }
        }
    }
}