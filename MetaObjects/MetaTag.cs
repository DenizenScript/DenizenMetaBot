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

        public override void AddTo(MetaDocs docs)
        {
            docs.Tags.Add(Name.ToLowerFast(), this);
        }

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
            builder.Description = Description.Length > 600 ? Description.Substring(0, 500) + "..." : Description;
            return builder;
        }

        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "attribute":
                    TagFull = value;
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
