using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using FreneticUtilities.FreneticExtensions;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// A documented tag.
    /// </summary>
    public class MetaTag : MetaObject
    {
        public override MetaType Type => MetaDocs.META_TYPE_TAG;

        public override string Name => TagFull;

        public override string CleanName => CleanedName;

        public override void AddTo(MetaDocs docs)
        {
            docs.Tags.Add(CleanedName, this);
        }

        /// <summary>
        /// Cleans tag text for searchability.
        /// </summary>
        public static string CleanTag(string text)
        {
            text = text.ToLowerFast();
            StringBuilder cleaned = new StringBuilder(text.Length);
            bool skipping = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<' || c == '>')
                {
                    continue;
                }
                if (c == '[')
                {
                    skipping = true;
                    continue;
                }
                if (c == ']')
                {
                    skipping = false;
                    continue;
                }
                if (skipping)
                {
                    continue;
                }
                cleaned.Append(c);
            }
            return cleaned.ToString();
        }

        /// <summary>
        /// The cleaned (searchable) name.
        /// </summary>
        public string CleanedName;

        /// <summary>
        /// The full tag syntax text.
        /// </summary>
        public string TagFull;

        /// <summary>
        /// The return type.
        /// </summary>
        public string Returns;

        /// <summary>
        /// The long-form description.
        /// </summary>
        public string Description;

        /// <summary>
        /// The associated mechanism, if any.
        /// </summary>
        public string Mechanism = "";

        public override EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = base.GetEmbed();
            AutoField(builder, "Returns", Returns);
            AutoField(builder, "Mechanism", Mechanism);
            builder.Description = EscapeForDiscord(ProcessMetaLinksForDiscord(Description.Length > 600 ? Description.Substring(0, 500) + "..." : Description));
            return builder;
        }

        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "attribute":
                    TagFull = value;
                    CleanedName = CleanTag(TagFull);
                    return true;
                case "returns":
                    Returns = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                case "mechanism":
                    Mechanism = value;
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }
    }
}
