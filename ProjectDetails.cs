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
    /// Represents information specific to a project.
    /// </summary>
    public class ProjectDetails
    {
        /// <summary>
        /// Properly cased project name.
        /// </summary>
        public string Name = "";

        /// <summary>
        /// The icon image URL for this project.
        /// </summary>
        public string Icon = "";

        /// <summary>
        /// Update message for the project.
        /// </summary>
        public string UpdateMessage = "";

        /// <summary>
        /// GitHub repo URL.
        /// </summary>
        public string GitHub = "";

        /// <summary>
        /// Gets an update embed message object.
        /// </summary>
        public Embed GetUpdateEmbed()
        {
            return new EmbedBuilder().WithTitle("Update " + Name).WithColor(0, 255, 255).WithThumbnailUrl(Icon).WithDescription(UpdateMessage).Build();
        }
    }
}
