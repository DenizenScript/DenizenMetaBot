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

namespace DenizenBot.HelperClasses
{
    /// <summary>Represents information specific to a channel.</summary>
    public class ChannelDetails
    {
        /// <summary>Projects to update.</summary>
        public ProjectDetails[] Updates = [];


        /// <summary>Whether docs are allowed.</summary>
        public bool Docs = false;
    }
}
