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

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Commands to administrate the bot.
    /// </summary>
    public class AdminCommands : UserCommands
    {
        /// <summary>
        /// Bot restart admin command.
        /// </summary>
        public void CMD_Restart(string[] cmds, SocketMessage message)
        {
            // NOTE: This implies a one-guild bot. A multi-guild bot probably shouldn't have this "BotCommander" role-based verification.
            // But under current scale, a true-admin confirmation isn't worth the bother.
            if (!Bot.IsBotCommander(message.Author as SocketGuildUser))
            {
                SendErrorMessageReply(message, "Authorization Failure", "Nope! That's not for you!");
                return;
            }
            if (!File.Exists("./start.sh"))
            {
                SendErrorMessageReply(message, "Cannot Comply", "Nope! That's not valid for my current configuration! (`start.sh` missing).");
            }
            SendGenericPositiveMessageReply(message, "Restarting", "Yes, boss. Restarting now...");
            Process.Start("bash", "./start.sh " + message.Channel.Id);
            Task.Factory.StartNew(() =>
            {
                Console.WriteLine("Shutdown start...");
                for (int i = 0; i < 15; i++)
                {
                    Console.WriteLine("T Minus " + (15 - i));
                    Task.Delay(1000).Wait();
                }
                Console.WriteLine("Shutdown!");
                Environment.Exit(0);
            });
            Bot.BotMonitor.StopAllLogic = true;
            Bot.Client.StopAsync().Wait();
        }

        /// <summary>
        /// Bot meta reload admin command.
        /// </summary>
        public void CMD_Reload(string[] cmds, SocketMessage message)
        {
            // NOTE: This implies a one-guild bot. A multi-guild bot probably shouldn't have this "BotCommander" role-based verification.
            // But under current scale, a true-admin confirmation isn't worth the bother.
            if (!Bot.IsBotCommander(message.Author as SocketGuildUser))
            {
                SendErrorMessageReply(message, "Authorization Failure", "Nope! That's not for you!");
                return;
            }
            SendGenericPositiveMessageReply(message, "Reloading", "Yes, boss. Reloading meta documentation now...");
            MetaDocs docs = new MetaDocs();
            docs.DownloadAll();
            Program.CurrentMeta = docs;
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Reload Complete").WithDescription("Documentation reloaded successfully.");
            if (docs.LoadErrors.Count > 0)
            {
                List<string> errors = docs.LoadErrors.Count > 5 ? docs.LoadErrors.GetRange(0, 5) : docs.LoadErrors;
                SendErrorMessageReply(message, "Error(s) While Reloading", string.Join("\n", errors));
                embed.AddField("Errors", docs.LoadErrors.Count, true);
            }
            embed.AddField("Commands", docs.Commands.Count, true);
            embed.AddField("Mechanisms", docs.Mechanisms.Count, true);
            embed.AddField("Tags", docs.Tags.Count, true);
            SendReply(message, embed.Build());
        }
    }
}
