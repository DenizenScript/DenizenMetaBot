using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using Discord.Rest;
using DenizenBot.HelperClasses;

namespace DenizenBot.UtilityProcessors
{
    /// <summary>
    /// Helper for reactions to commands, usually of the "did you mean?" variety.
    /// </summary>
    public static class ReactionsHandler
    {
        /// <summary>
        /// The maximum time a reaction click is still allowed for.
        /// </summary>
        public static TimeSpan MAX_REACT_TIME = new TimeSpan(hours: 0, minutes: 5, seconds: 0);

        /// <summary>
        /// A map from message IDs to their reactable data.
        /// </summary>
        public static Dictionary<ulong, ReactableMessage> Reactables = new Dictionary<ulong, ReactableMessage>(128);

        /// <summary>
        /// Adds a new reactable to be tracked, starting at the current time slot.
        /// </summary>
        /// <param name="originalMessage">The original message, from a user.</param>
        /// <param name="newMessage">The new message, from the bot.</param>
        /// <param name="command">The command to execute if affirmatively clicked.</param>
        public static void AddReactable(SocketMessage originalMessage, RestUserMessage newMessage, string command)
        {
            Reactables[newMessage.Id] = new ReactableMessage() { Command = command, Message = newMessage, OriginalMessage = originalMessage, TimeCreated = DateTimeOffset.Now };
            Console.WriteLine($"New reactable message: {newMessage.Id} with command '{command}'.");
        }

        /// <summary>
        /// Checks all currently tracked reactables, removing ones that have timed out.
        /// </summary>
        public static void CheckReactables()
        {
            if (Reactables.Count == 0)
            {
                return;
            }
            DateTimeOffset now = DateTimeOffset.Now;
            foreach (KeyValuePair<ulong, ReactableMessage> message in new Dictionary<ulong, ReactableMessage>(Reactables))
            {
                if (now.Subtract(message.Value.TimeCreated) > MAX_REACT_TIME)
                {
                    Console.WriteLine($"Removing (due to timeout) reaction on repliable message {message.Key} with command '{message.Value.Command}'.");
                    Reactables.Remove(message.Key);
                    message.Value.RemoveReactions();
                }
            }
        }

        /// <summary>
        /// Tests a new reaction on a message, to see if it needs to be handled. If so, handling will automatically start.
        /// </summary>
        /// <param name="messageId">The relevant message ID.</param>
        /// <param name="reaction">The new reaction.</param>
        public static void TestReaction(ulong messageId, SocketReaction reaction)
        {
            if (!Reactables.TryGetValue(messageId, out ReactableMessage message))
            {
                return;
            }
            Console.WriteLine($"Found reaction on repliable message {messageId} with command '{message.Command}' with reaction: {reaction.Emote.Name}");
            if (reaction.UserId != message.OriginalMessage.Author.Id)
            {
                Console.WriteLine($"User {reaction.User.Value.Username} ignored, as they are not the original author.");
                return;
            }
            if (reaction.Emote.Name == Constants.ACCEPT_EMOJI)
            {
                Console.WriteLine("Reaction is an accept. Running.");
                Program.CurrentBot.Respond(message.OriginalMessage, true, altContent: message.Command);
            }
            else if (reaction.Emote.Name != Constants.DENY_EMOJI)
            {
                Console.WriteLine("Reaction is unknown. Ignoring.");
                return;
            }
            Console.WriteLine("Removing reaction...");
            Reactables.Remove(messageId);
            message.RemoveReactions();
        }
    }
}
