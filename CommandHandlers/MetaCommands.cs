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
using DenizenBot.MetaObjects;

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Commands to look up meta documentation.
    /// </summary>
    public class MetaCommands : UserCommands
    {
        /// <summary>
        /// Checks whether meta commands are denied in the relevant channel. If denied, will return 'true' and show a rejection message.
        /// </summary>
        /// <param name="message">The message being replied to.</param>
        /// <returns>True if they are denied.</returns>
        public bool CheckMetaDenied(SocketMessage message)
        {
            if (!Bot.MetaCommandsAllowed(message.Channel))
            {
                SendErrorMessageReply(message, "Command Not Allowed Here",
                    "Meta documentation commands are not allowed in this channel. Please switch to a bot spam channel, or a Denizen channel.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Automatically processes a meta search command.
        /// </summary>
        /// <typeparam name="T">The meta object type.</typeparam>
        /// <param name="docs">The docs mapping.</param>
        /// <param name="type">The meta type.</param>
        /// <param name="cmds">The command args.</param>
        /// <param name="message">The Discord message object.</param>
        /// <param name="secondarySearch">A secondary search string if the first fails.</param>
        /// <param name="secondaryMatcher">A secondary matching function if needed.</param>
        public void AutoMetaCommand<T>(Dictionary<string, T> docs, MetaType type, string[] cmds, SocketMessage message,
            string secondarySearch = null, Func<T, bool> secondaryMatcher = null) where T: MetaObject
        {
            if (CheckMetaDenied(message))
            {
                return;
            }
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, $"Need input for '{type.Name}' command",
                    $"Please specify a {type.Name} to search, like `!{type.Name} Some{type.Name}Here`. Or, use `!{type.Name} all` to view all documented {type.Name.ToLowerFast()}s.");
                return;
            }
            string search = cmds[0].ToLowerFast();
            if (search == "all")
            {
                SendGenericPositiveMessageReply(message, $"All {type.Name}", $"Find all {type.Name} at {Constants.DOCS_URL_BASE}{type.WebPath}/");
                return;
            }
            if (!docs.TryGetValue(search, out T obj) && (secondarySearch == null || !docs.TryGetValue(secondarySearch, out obj)))
            {
                List<string> matched = new List<string>();
                List<string> strongMatched = new List<string>();
                foreach (string cmd in docs.Keys)
                {
                    if (cmd.Contains(search) || (secondarySearch != null && cmd.Contains(secondarySearch)))
                    {
                        matched.Add(cmd);
                    }
                    if (secondaryMatcher != null && secondaryMatcher(docs[cmd]))
                    {
                        strongMatched.Add(cmd);
                    }
                }
                if (strongMatched.Count > 0)
                {
                    matched = strongMatched;
                }
                if (matched.Count == 0)
                {
                    string closeName = StringConversionHelper.FindClosestString(docs.Keys, search, 20);
                    SendErrorMessageReply(message, $"Cannot Find Searched {type.Name}", $"Unknown {type.Name.ToLowerFast()}." + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
                }
                else if (matched.Count > 1)
                {
                    matched = matched.OrderBy((mat) => StringConversionHelper.GetLevenshteinDistance(search, mat)).ToList();
                    string suffix = ".";
                    if (matched.Count > 20)
                    {
                        matched = matched.GetRange(0, 20);
                        suffix = ", ...";
                    }
                    string listText = string.Join("`, `", matched);
                    SendErrorMessageReply(message, $"Cannot Specify Searched {type.Name}", $"Multiple possible {type.Name.ToLowerFast()}s: `{listText}`{suffix}");
                }
                else // Count == 1
                {
                    SendReply(message, docs[matched[0]].GetEmbed().Build());
                }
                return;
            }
            SendReply(message, obj.GetEmbed().Build());
        }

        /// <summary>
        /// Command meta docs user command.
        /// </summary>
        public void CMD_Command(string[] cmds, SocketMessage message)
        {
            AutoMetaCommand(Program.CurrentMeta.Commands, MetaDocs.META_TYPE_COMMAND, cmds, message);
        }

        /// <summary>
        /// Mechanism meta docs user command.
        /// </summary>
        public void CMD_Mechanism(string[] cmds, SocketMessage message)
        {
            string secondarySearch = null;
            if (cmds.Length > 0)
            {
                int dotIndex = cmds[0].IndexOf('.');
                if (dotIndex > 0)
                {
                    secondarySearch = cmds[0].Substring(0, dotIndex) + "tag" + cmds[0].Substring(dotIndex);
                }
            }
            AutoMetaCommand(Program.CurrentMeta.Mechanisms, MetaDocs.META_TYPE_MECHANISM, cmds, message, secondarySearch);
        }

        /// <summary>
        /// Tag meta docs user command.
        /// </summary>
        public void CMD_Tag(string[] cmds, SocketMessage message)
        {
            string secondarySearch = null;
            if (cmds.Length > 0)
            {
                cmds[0] = MetaTag.CleanTag(cmds[0]);
                int dotIndex = cmds[0].IndexOf('.');
                if (dotIndex > 0)
                {
                    secondarySearch = cmds[0].Substring(0, dotIndex) + "tag" + cmds[0].Substring(dotIndex);
                }
            }
            AutoMetaCommand(Program.CurrentMeta.Tags, MetaDocs.META_TYPE_TAG, cmds, message, secondarySearch);
        }

        /// <summary>
        /// Event meta docs user command.
        /// </summary>
        public void CMD_Event(string[] cmds, SocketMessage message)
        {
            string secondarySearch = string.Join(" ", cmds);
            AutoMetaCommand(Program.CurrentMeta.Events, MetaDocs.META_TYPE_EVENT, cmds, message, secondarySearch,
                (e) => e.RegexMatcher.IsMatch(secondarySearch));
        }
    }
}
