using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using FreneticUtilities.FreneticExtensions;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// A language documentation.
    /// </summary>
    public class MetaLanguage : MetaObject
    {
        public override MetaType Type => MetaDocs.META_TYPE_LANGUAGE;

        public override string Name => LangName;

        public override void AddTo(MetaDocs docs)
        {
            docs.Languages.Add(CleanName, this);
        }

        /// <summary>
        /// The name of the language.
        /// </summary>
        public string LangName;

        /// <summary>
        /// The long-form description.
        /// </summary>
        public string Description;

        public override EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = base.GetEmbed();
            builder.Description = EscapeForDiscord(ProcessMetaLinksForDiscord(Description.Length > 900 ? Description.Substring(0, 800) + "..." : Description));
            return builder;
        }

        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "name":
                    LangName = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        public override void PostCheck(MetaDocs docs)
        {
            PostCheckLinkableText(docs, Description);
        }

        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            return $"{baseText}\n{Description}";
        }
    }
}
