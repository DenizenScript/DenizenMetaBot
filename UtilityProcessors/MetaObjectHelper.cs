using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SharpDenizenTools.MetaObjects;
using SharpDenizenTools.MetaHandlers;
using Discord;
using FreneticUtilities.FreneticExtensions;

namespace DenizenBot.UtilityProcessors
{
    /// <summary>
    /// Helper logic for meta objects.
    /// </summary>
    public static class MetaObjectHelper
    {
        /// <summary>
        /// Escapes some text for safe Discord output.
        /// </summary>
        /// <param name="input">The input text (unescaped).</param>
        /// <returns>The output text (escaped).</returns>
        public static string EscapeForDiscord(string input)
        {
            if (input.Contains("```"))
            {
                return input;
            }
            StringBuilder output = new StringBuilder(input.Length * 2);
            bool inCodeBlock = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '`')
                {
                    inCodeBlock = !inCodeBlock;
                }
                else if (!inCodeBlock && (c == '<' || c == '>' || c == ':' || c == '|'))
                {
                    output.Append("\\");
                }
                output.Append(c);
            }
            return output.ToString();
        }

        /// <summary>
        /// Escapes a URL input string.
        /// </summary>
        /// <param name="input">The unescaped input.</param>
        /// <returns>The escaped output.</returns>
        public static string UrlEscape(string input)
        {
            return input.Replace(" ", "%20").Replace("<", "%3C").Replace(">", "%3E").Replace("[", "%5B").Replace("]", "%5D");
        }

        /// <summary>
        /// Checks the value as not null or whitespace, then adds it to the embed as an inline field.
        /// </summary>
        /// <param name="builder">The embed builder.</param>
        /// <param name="key">The field key.</param>
        /// <param name="value">The field value.</param>
        public static void AutoField(EmbedBuilder builder, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (value.Length > 1024)
                {
                    value = value.Substring(0, 1000) + "...";
                }
                builder.AddField(key, EscapeForDiscord(ProcessMetaLinksForDiscord(value)), false);
            }
        }

        /// <summary>
        /// Processes meta "@link"s for Discord output.
        /// </summary>
        /// <param name="linkedtext">The text which may contain links.</param>
        /// <returns>The text, with links processed.</returns>
        public static string ProcessMetaLinksForDiscord(string linkedtext)
        {
            int nextLinkIndex = linkedtext.IndexOf("<@link");
            if (nextLinkIndex < 0)
            {
                return linkedtext;
            }
            int lastStartIndex = 0;
            StringBuilder output = new StringBuilder(linkedtext.Length);
            while (nextLinkIndex >= 0)
            {
                output.Append(linkedtext[lastStartIndex..nextLinkIndex]);
                int endIndex = MetaObject.FindClosingTagMark(linkedtext, nextLinkIndex + 1);
                if (endIndex < 0)
                {
                    lastStartIndex = nextLinkIndex;
                    break;
                }
                int startOfMetaCommand = nextLinkIndex + "<@link ".Length;
                string metaCommand = linkedtext[startOfMetaCommand..endIndex];
                if (metaCommand.StartsWith("url"))
                {
                    string url = metaCommand.Substring("url ".Length);
                    output.Append($"[{url}]({url})");
                }
                else
                {
                    output.Append($"`!{metaCommand}`");
                }
                lastStartIndex = endIndex + 1;
                nextLinkIndex = linkedtext.IndexOf("<@link", lastStartIndex);
            }
            output.Append(linkedtext.Substring(lastStartIndex));
            return output.ToString();
        }

        /// <summary>
        /// Gets a version of the output embed, that showcases related tags.
        /// </summary>
        public static EmbedBuilder GetCommandTagsEmbed(this MetaCommand command)
        {
            EmbedBuilder builder = GetEmbed(command);
            builder.Description = "";
            if (command.Tags.IsEmpty())
            {
                return builder;
            }
            AutoField(builder, "Related Tags", GetTagsField(command.Tags));
            return builder; 
        }

        /// <summary>
        /// Gets a version of the output embed, that showcases sample usages.
        /// </summary>
        public static EmbedBuilder GetCommandUsagesEmbed(this MetaCommand command)
        {
            EmbedBuilder builder = GetEmbed(command);
            builder.Description = "";
            int limitLengthRemaining = 1000;
            foreach (string usage in command.Usages)
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

        /// <summary>
        /// Converts a tags array to a valid tags field text output for embedding.
        /// Used by <see cref="MetaCommand"/> and <see cref="MetaMechanism"/>.
        /// </summary>
        /// <param name="tags">The tags array.</param>
        /// <returns>The tags field text.</returns>
        public static string GetTagsField(IEnumerable<string> tags)
        {
            int limitLengthRemaining = 1000;
            StringBuilder tagsFieldBuilder = new StringBuilder(tags.Count() * 30);
            foreach (string tag in tags)
            {
                string tagPreSpace = tag.BeforeAndAfter(" ", out string tagAfterSpace);
                string tagOut;
                if (tagPreSpace.EndsWith(">") && string.IsNullOrWhiteSpace(tagAfterSpace))
                {
                    tagOut = $"`{tag}`";
                    MetaTag realTag = MetaDocs.CurrentMeta.FindTag(tag);
                    if (realTag == null)
                    {
                        tagOut += " (Invalid tag)";
                    }
                    else
                    {
                        tagOut += " " + realTag.Description.Replace("\n", " ");
                    }
                }
                else
                {
                    int endMark = tagPreSpace.LastIndexOf('>') + 1;
                    if (endMark == 0)
                    {
                        tagOut = tag;
                    }
                    else
                    {
                        tagOut = $"`{tag.Substring(0, endMark)}`{tag.Substring(endMark)}";
                    }
                }
                if (tagOut.Length > 128)
                {
                    tagOut = tagOut.Substring(0, 100) + "...";
                }
                limitLengthRemaining -= tagOut.Length;
                tagsFieldBuilder.Append(tagOut).Append("\n");
                if (limitLengthRemaining <= 0)
                {
                    break;
                }
            }
            return tagsFieldBuilder.ToString();
        }

        /// <summary>
        /// Gets an <see cref="EmbedBuilder"/> for a <see cref="MetaObject"/> to be shown on Discord meta output.
        /// </summary>
        /// <param name="obj">The meta object to embed.</param>
        /// <returns>The Discord-ready embed object.</returns>
        public static EmbedBuilder GetEmbed(this MetaObject obj)
        {
            EmbedBuilder builder = new EmbedBuilder().WithColor(0, 255, 255).WithTitle(obj.Type.Name + ": " + obj.Name)
                .WithUrl(DenizenMetaBotConstants.DOCS_URL_BASE + obj.Type.WebPath + "/" + UrlEscape(obj.CleanName));
            AutoField(builder, "Required Plugins or Platforms", obj.Plugin);
            AutoField(builder, "Group", obj.Group);
            foreach (string warn in obj.Warnings)
            {
                AutoField(builder, "**WARNING**", warn);
            }
            if (obj is MetaAction action)
            {
                if (action.Actions.Length > 1)
                {
                    AutoField(builder, "Other Action Lines", "`" + string.Join("\n", action.Actions.Skip(1)) + "`");
                }
                AutoField(builder, "Triggers", action.Triggers);
                AutoField(builder, "Context", string.Join("\n", action.Context));
                AutoField(builder, "Determine", string.Join("\n", action.Determinations));
            }
            else if (obj is MetaCommand command)
            {
                AutoField(builder, "Syntax", $"`{command.Syntax}`");
                AutoField(builder, "Short Description", command.Short);
                if (!string.IsNullOrWhiteSpace(command.Guide))
                {
                    AutoField(builder, "Related Guide Page", $"[{command.Guide}]({command.Guide})");
                }
                builder.AddField("Description", EscapeForDiscord(ProcessMetaLinksForDiscord(command.Description.Length > 600 ? command.Description.Substring(0, 500) + "..." : command.Description)));
            }
            else if (obj is MetaEvent evt)
            {
                if (evt.Events.Length > 1)
                {
                    AutoField(builder, "Other Event Lines", "`" + string.Join("\n", evt.Events.Skip(1)) + "`");
                }
                AutoField(builder, "Switches", string.Join("\n", evt.Switches));
                AutoField(builder, "Triggers", evt.Triggers);
                AutoField(builder, "Has Player", evt.Player);
                AutoField(builder, "Has NPC", evt.NPC);
                AutoField(builder, "Context", GetTagsField(evt.Context));
                AutoField(builder, "Determine", string.Join("\n", evt.Determinations));
                if (evt.Cancellable)
                {
                    AutoField(builder, "Cancellable", "True - this adds `<context.cancelled>` and determines `cancelled` + `cancelled:false`.");
                }
            }
            else if (obj is MetaGuidePage guidePage)
            {
                builder = builder.WithUrl(guidePage.URL).WithThumbnailUrl(DenizenMetaBotConstants.DENIZEN_LOGO);
                builder.Description = $"Read the guide page '**{guidePage.PageName}**' at: {guidePage.URL}";
            }
            else if (obj is MetaLanguage language)
            {
                builder.Description = EscapeForDiscord(ProcessMetaLinksForDiscord(language.Description.Length > 900 ? language.Description.Substring(0, 800) + "..." : language.Description));
            }
            else if (obj is MetaMechanism mechanism)
            {
                builder = builder.WithTitle(mechanism.MechObject + " mechanism: " + mechanism.MechName);
                AutoField(builder, "Input", mechanism.Input);
                AutoField(builder, "Tags", GetTagsField(mechanism.Tags));
                builder.Description = EscapeForDiscord(ProcessMetaLinksForDiscord(mechanism.Description.Length > 600 ? mechanism.Description.Substring(0, 500) + "..." : mechanism.Description));
            }
            else if (obj is MetaTag tag)
            {
                AutoField(builder, "Returns", tag.Returns);
                AutoField(builder, "Mechanism", tag.Mechanism);
                builder.Description = EscapeForDiscord(ProcessMetaLinksForDiscord(tag.Description.Length > 600 ? tag.Description.Substring(0, 500) + "..." : tag.Description));
            }
            return builder;
        }
    }
}
