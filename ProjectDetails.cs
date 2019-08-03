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

        /// <summary>
        /// Gets a GitHub link embed message object.
        /// </summary>
        public Embed GetGithubEmbed()
        {
            if (GitHub.Length > 0)
            {
                return new EmbedBuilder().WithTitle(Name + " on GitHub").WithColor(0, 64, 255).WithThumbnailUrl(Icon).WithUrl(GitHub)
                    .WithDescription(Name + " can be found on GitHub at: " + GitHub).Build();
            }
            else
            {
                return new EmbedBuilder().WithTitle(Name + " is not on GitHub").WithColor(255, 64, 0).WithThumbnailUrl(Icon)
                    .WithDescription(Name + " is not on GitHub.").Build();
            }
        }

        /// <summary>
        /// Gets a GitHub issues link embed message object.
        /// </summary>
        public Embed GetIssuesEmbed()
        {
            if (GitHub.Length > 0)
            {
                return new EmbedBuilder().WithTitle(Name + " Issues on GitHub").WithColor(0, 64, 255).WithThumbnailUrl(Icon).WithUrl(GitHub + "/issues")
                    .WithDescription(Name + " allows issue posting on GitHub at: " + GitHub + "/issues").Build();
            }
            else
            {
                return new EmbedBuilder().WithTitle(Name + " Issues is not on GitHub").WithColor(255, 64, 0).WithThumbnailUrl(Icon)
                    .WithDescription(Name + " is not on GitHub, so no issues link is available. Ask a human for help.").Build();
            }
        }
    }
}
