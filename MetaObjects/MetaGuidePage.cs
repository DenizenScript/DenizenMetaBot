using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using FreneticUtilities.FreneticExtensions;
using DenizenBot.UtilityProcessors;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// A page of the beginner's guide.
    /// </summary>
    public class MetaGuidePage : MetaObject
    {
        public override MetaType Type => MetaDocs.META_TYPE_GUIDEPAGE;

        public override string Name => PageName;

        public override void AddTo(MetaDocs docs)
        {
            docs.GuidePages.Add(CleanName, this);
        }

        /// <summary>
        /// The name of the page.
        /// </summary>
        public string PageName;

        /// <summary>
        /// The URL to the page.
        /// </summary>
        public string URL;

        public override EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = base.GetEmbed().WithUrl(URL).WithThumbnailUrl(Constants.DENIZEN_LOGO);
            builder.Description = $"Read the guide page '**{PageName}**' at: {URL}";
            return builder;
        }

        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            return $"{baseText}\n{URL}";
        }
    }
}
