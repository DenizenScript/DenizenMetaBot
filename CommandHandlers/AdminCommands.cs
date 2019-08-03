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
        /// Bot restart user command.
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
            SendGenericPositiveMessageReply(message, "Restarting...", "Yes, boss. Restarting now...");
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
            Bot.Client.StopAsync().Wait();
        }
    }
}
