using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using Discord.Rest;
using DiscordBotBase;

namespace DenizenBot.HelperClasses
{
    /// <summary>Represents a message that allows a user to click a reaction for to accept/deny a command.</summary>
    public class ReactableMessage
    {
        /// <summary>The message that can be reacted to.</summary>
        public RestUserMessage Message;

        /// <summary>The original message that the reactable message was replying to.</summary>
        public SocketMessage OriginalMessage;

        /// <summary>The command to imitate if accepted.</summary>
        public string Command;

        /// <summary>The time the reaction was created, for timeout purposes.</summary>
        public DateTimeOffset TimeCreated;

        /// <summary>Removes the actual reactions from the message.</summary>
        public void RemoveReactions()
        {
            Message.RemoveReactionsAsync(DiscordBotBaseHelper.CurrentBot.Client.CurrentUser, new IEmote[] { new Emoji(Constants.ACCEPT_EMOJI), new Emoji(Constants.DENY_EMOJI) }).Wait();
        }
    }
}
