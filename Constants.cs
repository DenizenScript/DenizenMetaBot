using System;
using System.Collections.Generic;
using System.Text;

namespace DenizenBot
{
    /// <summary>
    /// Constants (links, image urls, etc).
    /// Either absolute constants, or config-loaded pseudo-constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The base for the meta docs URL.
        /// </summary>
        public static string DOCS_URL_BASE = "https://one.denizenscript.com/denizen/";

        /// <summary>
        /// The prefix for non-ping-based command usage.
        /// </summary>
        public static string COMMAND_PREFIX = "!";

        /// <summary>
        /// Link to the GitHub repo for this bot.
        /// </summary>
        public const string SOURCE_CODE_URL = "https://github.com/DenizenScript/DenizenMetaBot";

        /// <summary>
        /// A warning emoji image URL.
        /// </summary>
        public const string WARNING_ICON = "https://i.alexgoodwin.media/i/for_real_usage/13993d.png";

        /// <summary>
        /// The Denizen logo.
        /// </summary>
        public const string DENIZEN_LOGO = "https://i.alexgoodwin.media/i/for_real_usage/ec5694.png";

        /// <summary>
        /// Generic reusable "information" icon.
        /// </summary>
        public const string INFO_ICON = "https://i.alexgoodwin.media/i/for_real_usage/911f66.png";

    }
}
