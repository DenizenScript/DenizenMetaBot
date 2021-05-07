using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SharpDenizenTools.MetaObjects;
using SharpDenizenTools.MetaHandlers;
using Discord;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;

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
                    output.Append('\\');
                }
                output.Append(c);
            }
            return output.ToString();
        }

        public static AsciiMatcher NeedsUrlEscape = new AsciiMatcher("<>[]|\"");

        /// <summary>
        /// Escapes a URL input string.
        /// </summary>
        /// <param name="input">The unescaped input.</param>
        /// <returns>The escaped output.</returns>
        public static string UrlEscape(string input)
        {
            input = input.Replace(" ", "%20");
            if (NeedsUrlEscape.ContainsAnyMatch(input))
            {
                input = input.Replace("<", "%3C").Replace(">", "%3E").Replace("[", "%5B").Replace("]", "%5D").Replace("|", "%7C").Replace("\"", "%22");
            }
            return input;
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
                builder.AddField(key, ProcessBlockTextForDiscord(value), false);
            }
        }

        public static string ProcessBlockTextForDiscord(string content)
        {
            int codeBlockStart = content.IndexOf("<code>");
            if (codeBlockStart == -1)
            {
                return EscapeForDiscord(ProcessMetaLinksForDiscord(content));
            }
            int codeBlockEnd = content.IndexOf("</code>", codeBlockStart);
            if (codeBlockEnd == -1)
            {
                // This can happen from trimming, so ignore it.
                return EscapeForDiscord(ProcessMetaLinksForDiscord(content));
            }
            string beforeBlock = EscapeForDiscord(ProcessMetaLinksForDiscord(content[0..codeBlockStart]));
            string code = (content[(codeBlockStart + "<code>".Length)..codeBlockEnd]);
            string afterBlock = ProcessBlockTextForDiscord(content[(codeBlockEnd + "</code>".Length)..]);
            return $"{beforeBlock}\n```yml\n{code}\n```\n{afterBlock}";
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
                    string url = metaCommand["url ".Length..];
                    output.Append($"[{url}]({url})");
                }
                else
                {
                    output.Append($"`!{metaCommand}`");
                }
                lastStartIndex = endIndex + 1;
                nextLinkIndex = linkedtext.IndexOf("<@link", lastStartIndex);
            }
            _ = output.Append(linkedtext[lastStartIndex..]);
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
            EmbedBuilder builder = GetEmbed(command, true);
            builder.Description = "";
            int limitLengthRemaining = 1000;
            int count = 0;
            foreach (string usage in command.Usages.Take(5))
            {
                count++;
                string usageOut = usage;
                string nameBar = "Sample Usage";
                int firstNewline = usageOut.IndexOf('\n');
                if (firstNewline > 0)
                {
                    nameBar = usageOut.Substring(0, firstNewline);
                    limitLengthRemaining -= nameBar.Length;
                    usageOut = usageOut[(firstNewline + 1)..];
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
            if (count < command.Usages.Count)
            {
                AutoField(builder, "Additional Usage Examples", $"... and {command.Usages.Count - count} more.");
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
            int limitLengthRemaining = 850;
            StringBuilder tagsFieldBuilder = new StringBuilder(tags.Count() * 30);
            int count = 0;
            foreach (string tag in tags.Take(10))
            {
                count++;
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
                        tagOut = $"`{tag.Substring(0, endMark)}`{tag[endMark..]}";
                    }
                }
                if (tagOut.Length > 128)
                {
                    tagOut = tagOut.Substring(0, 100) + "...";
                }
                limitLengthRemaining -= tagOut.Length;
                tagsFieldBuilder.Append(tagOut).Append('\n');
                if (limitLengthRemaining <= 0)
                {
                    break;
                }
            }
            if (count < tags.Count())
            {
                tagsFieldBuilder.Append($"... and {tags.Count() - count} more.");
            }
            return tagsFieldBuilder.ToString();
        }

        /// <summary>
        /// Gets an <see cref="EmbedBuilder"/> for a <see cref="MetaObject"/> to be shown on Discord meta output.
        /// </summary>
        /// <param name="obj">The meta object to embed.</param>
        /// <param name="hideLargeData">Whether to hide large data parts (eg command descriptions).</param>
        /// <returns>The Discord-ready embed object.</returns>
        public static EmbedBuilder GetEmbed(this MetaObject obj, bool hideLargeData = false)
        {
            EmbedBuilder builder = new EmbedBuilder().WithColor(0, 255, 255).WithTitle(obj.Type.Name + ": " + obj.Name);
            if (obj is not MetaGuidePage)
            {
                builder = builder.WithUrl(DenizenMetaBotConstants.DOCS_URL_BASE + obj.Type.WebPath + "/" + UrlEscape(obj.CleanName));
            }
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
                if (!hideLargeData)
                {
                    builder.AddField("Description", ProcessBlockTextForDiscord(command.Description.Length > 600 ? command.Description.Substring(0, 500) + "..." : command.Description));
                }
            }
            else if (obj is MetaEvent evt)
            {
                if (evt.Events.Length > 1)
                {
                    AutoField(builder, "Other Event Lines", "`" + string.Join("\n", evt.Events.Skip(1)) + "`");
                }
                AutoField(builder, "Switches", string.Join("\n", evt.Switches));
                AutoField(builder, "Triggers", evt.Triggers);
                if (!string.IsNullOrWhiteSpace(evt.Player))
                {
                    AutoField(builder, "Has Player", evt.Player + " - this adds switches `flagged:<flag name>` + `permission:<node>`, in addition to the `<player>` link.");
                }
                AutoField(builder, "Has NPC", evt.NPC);
                AutoField(builder, "Context", GetTagsField(evt.Context));
                AutoField(builder, "Determine", string.Join("\n", evt.Determinations));
                if (evt.HasLocation)
                {
                    AutoField(builder, "Has Known Location", "True - this adds switches `in:<area>` + `location_flagged:<flag name>`.");
                }
                if (evt.Cancellable)
                {
                    AutoField(builder, "Cancellable", "True - this adds `<context.cancelled>` and determines `cancelled` + `cancelled:false`.");
                }
            }
            else if (obj is MetaGuidePage guidePage)
            {
                builder = builder.WithUrl(guidePage.URL).WithThumbnailUrl(DenizenMetaBotConstants.DENIZEN_LOGO);
                builder.Description = $"View the guide page '**{guidePage.PageName}**' at: {guidePage.URL}";
            }
            else if (obj is MetaLanguage language)
            {
                builder.Description = ProcessBlockTextForDiscord(language.Description.Length > 900 ? language.Description.Substring(0, 800) + "..." : language.Description);
            }
            else if (obj is MetaMechanism mechanism)
            {
                builder = builder.WithTitle(mechanism.MechObject + " mechanism: " + mechanism.MechName);
                AutoField(builder, "Input", mechanism.Input);
                AutoField(builder, "Tags", GetTagsField(mechanism.Tags));
                builder.Description = ProcessBlockTextForDiscord(mechanism.Description.Length > 600 ? mechanism.Description.Substring(0, 500) + "..." : mechanism.Description);
            }
            else if (obj is MetaTag tag)
            {
                AutoField(builder, "Returns", tag.Returns);
                AutoField(builder, "Mechanism", tag.Mechanism);
                builder.Description = ProcessBlockTextForDiscord(tag.Description.Length > 600 ? tag.Description.Substring(0, 500) + "..." : tag.Description);
            }
            return builder;
        }
    }
}
