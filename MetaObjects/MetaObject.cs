using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using DenizenBot;
using FreneticUtilities.FreneticExtensions;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// Abstract base for a type of meta object.
    /// </summary>
    public abstract class MetaObject
    {
        /// <summary>
        /// Get the meta type of the object.
        /// </summary>
        public abstract MetaType Type { get; }

        /// <summary>
        /// Get the name of the object. May have capitals.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Get the clean lowercase name of the object.
        /// </summary>
        public virtual string CleanName => Name.ToLowerFast();

        /// <summary>
        /// What categorization group the object is in.
        /// </summary>
        public string Group;

        /// <summary>
        /// Any warnings applied to this object type.
        /// </summary>
        public List<string> Warnings = new List<string>();

        /// <summary>
        /// Required plugin(s) if applicable.
        /// </summary>
        public string Plugin;

        /// <summary>
        /// The file in source code that defined this meta object.
        /// </summary>
        public string SourceFile;

        /// <summary>
        /// Apply a setting value to this meta object.
        /// </summary>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        /// <returns>Whether the value was applied.</returns>
        public virtual bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "group":
                    Group = value;
                    return true;
                case "warning":
                    Warnings.Add(value);
                    return true;
                case "plugin":
                    Plugin = value;
                    return true;
                default:
                    return false;
            }
        }

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
                else if (!inCodeBlock && (c == '<' || c == '>'))
                {
                    output.Append("\\");
                }
                output.Append(c);
            }
            return output.ToString();
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
                builder.AddField(key, EscapeForDiscord(value), true);
            }
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
        /// Get an embed object for this meta object.
        /// </summary>
        public virtual EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = new EmbedBuilder().WithColor(0, 255, 255).WithTitle(Type.Name + ": " + Name)
                .WithUrl(Constants.DOCS_URL_BASE + Type.WebPath + "/" + UrlEscape(CleanName));
            AutoField(builder, "Required Plugin(s)", Plugin);
            AutoField(builder, "Group", Group);
            foreach (string warn in Warnings)
            {
                AutoField(builder, "**WARNING**", warn);
            }
            return builder;
        }

        /// <summary>
        /// Adds the object to the meta docs set.
        /// </summary>
        /// <param name="docs">The docs set.</param>
        public abstract void AddTo(MetaDocs docs);
    }
}
