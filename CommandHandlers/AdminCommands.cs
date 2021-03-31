using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using DenizenBot.UtilityProcessors;
using DiscordBotBase;
using DiscordBotBase.CommandHandlers;
using SharpDenizenTools.MetaHandlers;
using System.Net;

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Commands to administrate the bot.
    /// </summary>
    public class AdminCommands : UserCommands
    {
        /// <summary>
        /// Bot meta reload admin command.
        /// </summary>
        public void CMD_Reload(CommandData command)
        {
            // NOTE: This implies a one-guild bot. A multi-guild bot probably shouldn't have this "BotCommander" role-based verification.
            // But under current scale, a true-admin confirmation isn't worth the bother.
            if (!DenizenMetaBot.IsBotCommander(command.Message.Author as SocketGuildUser))
            {
                SendErrorMessageReply(command.Message, "Authorization Failure", "Nope! That's not for you!");
                return;
            }
            SendGenericPositiveMessageReply(command.Message, "Reloading", "Yes, boss. Reloading meta documentation now...");
            BuildNumberTracker.UpdateAll();
            MetaDocs docs = new MetaDocs();
            docs.DownloadAll();
            MetaDocs.CurrentMeta = docs;
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Reload Complete").WithDescription("Documentation reloaded successfully.");
            if (docs.LoadErrors.Count > 0)
            {
                List<string> errors = docs.LoadErrors.Count > 5 ? docs.LoadErrors.GetRange(0, 5) : docs.LoadErrors;
                SendErrorMessageReply(command.Message, "Error(s) While Reloading", string.Join("\n", errors));
                embed.AddField("Errors", docs.LoadErrors.Count, true);
            }
            embed.AddField("Commands", docs.Commands.Count, true);
            embed.AddField("Mechanisms", docs.Mechanisms.Count, true);
            embed.AddField("Tags", docs.Tags.Count, true);
            embed.AddField("Events", docs.Events.Count, true);
            embed.AddField("Actions", docs.Actions.Count, true);
            embed.AddField("Languages", docs.Languages.Count, true);
            embed.AddField("Guide Pages", docs.GuidePages.Count, true);
            SendReply(command.Message, embed.Build());
            foreach (string url in DenizenMetaBot.ReloadWebooks)
            {
                try
                {
                    ReusableWebClient.UploadString(url, "");
                }
                catch (Exception ex)
                {
                    Console.Error.Write($"Failed to ping webhook URL '{url}': {ex}");
                }
            }
        }

        public WebClient ReusableWebClient = new WebClient();

        public AdminCommands()
        {
            ReusableWebClient.Headers["User-Agent"] = "DenizenMetaBot";
        }
    }
}
