using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using FreneticUtilities.FreneticExtensions;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// A documented command.
    /// </summary>
    public class MetaCommand : MetaObject
    {
        public override MetaType Type => MetaDocs.META_TYPE_COMMAND;

        public override string Name => CommandName;

        public override void AddTo(MetaDocs docs)
        {
            docs.Commands.Add(CleanName, this);
        }

        /// <summary>
        /// The name of the command.
        /// </summary>
        public string CommandName = "";

        /// <summary>
        /// How many arguments are required, minimum.
        /// </summary>
        public int Required = 0;

        /// <summary>
        /// The syntax guide.
        /// </summary>
        public string Syntax = "";

        /// <summary>
        /// The short description.
        /// </summary>
        public string Short = "";

        /// <summary>
        /// The long-form description.
        /// </summary>
        public string Description = "";

        /// <summary>
        /// Tags documented for this command. One tag per string.
        /// </summary>
        public string[] Tags = new string[0];

        /// <summary>
        /// An associated beginner's guide link.
        /// </summary>
        public string Guide = "";

        /// <summary>
        /// Sample usages.
        /// </summary>
        public List<string> Usages = new List<string>();

        public override EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = base.GetEmbed();
            AutoField(builder, "Syntax", Syntax);
            AutoField(builder, "Short Description", Short);
            if (!string.IsNullOrWhiteSpace(Guide))
            {
                AutoField(builder, "Related Guide Page", $"[{Guide}]({Guide})");
            }
            builder.Description = EscapeForDiscord(ProcessMetaLinksForDiscord(Description.Length > 600 ? Description.Substring(0, 500) + "..." : Description));
            return builder;
        }

        /// <summary>
        /// Gets a version of the output embed, that showcases related tags.
        /// </summary>
        public EmbedBuilder GetTagsEmbed()
        {
            EmbedBuilder builder = GetEmbed();
            builder.Description = "";
            if (Tags.IsEmpty())
            {
                return builder;
            }
            int limitLengthRemaining = 1000;
            StringBuilder tagsFieldBuilder = new StringBuilder(Tags.Length * 30);
            foreach (string tag in Tags)
            {
                string tagOut = tag;
                if (tagOut.EndsWith(">"))
                {
                    MetaTag realTag = Program.CurrentMeta.FindTag(tagOut);
                    if (realTag == null)
                    {
                        tagOut += " (Invalid tag)";
                    }
                    else
                    {
                        tagOut += " " + realTag.Description.Replace("\n", " ");
                    }
                }
                if (tagOut.Length > 128)
                {
                    tagOut = tagOut.Substring(0, 100) + "...";
                }
                limitLengthRemaining -= tagOut.Length;
                tagsFieldBuilder.Append(tagOut);
                if (limitLengthRemaining <= 0)
                {
                    break;
                }
            }
            AutoField(builder, "Related Tags", tagsFieldBuilder.ToString());
            return builder;
        }

        /// <summary>
        /// Gets a version of the output embed, that showcases sample usages.
        /// </summary>
        public EmbedBuilder GetUsagesEmbed()
        {
            EmbedBuilder builder = GetEmbed();
            builder.Description = "";
            int limitLengthRemaining = 1000;
            foreach (string usage in Usages)
            {
                string usageOut = usage;
                string nameBar = "Sample Usage";
                int firstNewline = usageOut.IndexOf('\n');
                if (firstNewline > 0)
                {
                    nameBar = usageOut.Substring(0, firstNewline);
                    limitLengthRemaining -= nameBar.Length;
                    usageOut = usageOut.Substring(firstNewline + 1);
                }
                usageOut = $"```yml\n{usageOut}\n```";
                if (usageOut.Length > 512)
                {
                    usageOut = usageOut.Substring(0, 500) + "...";
                }
                limitLengthRemaining -= usageOut.Length;
                AutoField(builder, nameBar, usageOut);
                if (limitLengthRemaining <= 0)
                {
                    break;
                }
            }
            return builder;
        }

        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "name":
                    CommandName = value;
                    return true;
                case "required":
                    return int.TryParse(value, out Required);
                case "syntax":
                    Syntax = value;
                    return true;
                case "short":
                    Short = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                case "tags":
                    Tags = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "usage":
                    Usages.Add(value);
                    return true;
                case "guide":
                    Guide = value;
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        public override void PostCheck(MetaDocs docs)
        {
            foreach (string tag in Tags)
            {
                if (tag.EndsWith(">"))
                {
                    MetaTag realTag = Program.CurrentMeta.FindTag(tag);
                    if (realTag == null)
                    {
                        docs.LoadErrors.Add($"Command '{Name}' references tag '{tag}', which doesn't exist.");
                    }
                }
            }
        }

        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            string allUsages = string.Join('\n', Usages);
            string allTags = string.Join('\n', Tags);
            return $"{baseText}\n{allTags}\n{allUsages}\n{Syntax}\n{Short}\n{Description}\n{Guide}";
        }
    }
}
