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

namespace DenizenBot
{
    /// <summary>
    /// Abstract base class for commands that users can run.
    /// </summary>
    public abstract class UserCommands
    {
        /// <summary>
        /// Prefix for when the bot successfully handles user input.
        /// </summary>
        public const string SUCCESS_PREFIX = "+++ ";

        /// <summary>
        /// Prefix for when the bot refuses user input.
        /// </summary>
        public const string REFUSAL_PREFIX = "--- ";

        /// <summary>
        /// The backing meta bot instance.
        /// </summary>
        public DenizenMetaBot Bot;

        public const string WARNING_EMOJI = "https://i.alexgoodwin.media/i/for_real_usage/13993d.png";

        /// <summary>
        /// Creates an Embed object for an error message.
        /// </summary>
        public Embed GetErrorMessageEmbed(string title, string message)
        {
            return new EmbedBuilder().WithTitle(title).WithColor(255, 64, 32).WithThumbnailUrl(WARNING_EMOJI).WithDescription(message).Build();
        }
    }
}
