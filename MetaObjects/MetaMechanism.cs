using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using FreneticUtilities.FreneticExtensions;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// A documented mechanism.
    /// </summary>
    public class MetaMechanism : MetaObject
    {
        public override MetaType Type => MetaDocs.META_TYPE_MECHANISM;

        public override string Name => $"{MechObject}.{MechName}";

        public override void AddTo(MetaDocs docs)
        {
            docs.Mechanisms.Add(CleanName, this);
        }

        /// <summary>
        /// The object the mechanism applies to.
        /// </summary>
        public string MechObject;

        /// <summary>
        /// The name of the mechanism.
        /// </summary>
        public string MechName;

        /// <summary>
        /// The input type.
        /// </summary>
        public string Input;

        /// <summary>
        /// The long-form description.
        /// </summary>
        public string Description;

        /// <summary>
        /// Tags documented for this mechanism. One tag per string.
        /// </summary>
        public string[] Tags = new string[0];

        public override EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = base.GetEmbed().WithTitle(MechObject + " mechanism: " + MechName);
            AutoField(builder, "Input", Input);
            AutoField(builder, "Tags", String.Join("\n", Tags));
            builder.Description = EscapeForDiscord(ProcessMetaLinksForDiscord(Description.Length > 600 ? Description.Substring(0, 500) + "..." : Description));
            return builder;
        }

        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "object":
                    MechObject = value;
                    return true;
                case "name":
                    MechName = value;
                    return true;
                case "input":
                    Input = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                case "tags":
                    Tags = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        public override void PostCheck(MetaDocs docs)
        {
            PostCheckTags(docs, Tags);
        }

        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            string allTags = string.Join('\n', Tags);
            return $"{baseText}\n{allTags}\n{Input}\n{Description}\n{MechObject}\n{MechName}";
        }
    }
}
