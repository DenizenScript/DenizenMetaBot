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
        /// Automatically processes a meta search command.
        /// </summary>
        /// <typeparam name="T">The meta object type.</typeparam>
        /// <param name="docs">The docs mapping.</param>
        /// <param name="metaType">The meta type name.</param>
        /// <param name="cmds">The command args.</param>
        /// <param name="message">The Discord message object.</param>
        public void AutoMetaCommand<T>(Dictionary<string, T> docs, string metaType, string[] cmds, SocketMessage message) where T: MetaObject
        {
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, $"Need input for '{metaType}' command",
                    $"Please specify a {metaType} to search, like `!{metaType} Some{metaType}Here`. Or, use `!{metaType} all` to view all documented {metaType.ToLowerFast()}s.");
                return;
            }
            string search = cmds[0].ToLowerFast();
            if (!docs.TryGetValue(search, out T obj))
            {
                string closeName = StringConversionHelper.FindClosestString(docs.Keys, search, 20);
                SendErrorMessageReply(message, $"Cannot Find Searched {metaType}", $"Unknown {metaType.ToLowerFast()}." + (closeName == null ? "" : $" Did you mean `{closeName}`?"));
                return;
            }
            SendReply(message, obj.GetEmbed().Build());
        }

        /// <summary>
        /// Command meta docs user command.
        /// </summary>
        public void CMD_Command(string[] cmds, SocketMessage message)
        {
            AutoMetaCommand(Program.CurrentMeta.Commands, "Command", cmds, message);
        }
    }
}
