using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using DenizenBot;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// Abstract base for a type of meta object.
    /// </summary>
    public abstract class MetaObject
    {
        /// <summary>
        /// Get the name of the object meta type.
        /// </summary>
        public abstract string TypeName { get; }

        /// <summary>
        /// Get the webpath for this object meta type (eg "cmds").
        /// </summary>
        public abstract string WebPath { get; }

        /// <summary>
        /// Get the name of the object.
        /// </summary>
        public abstract string Name { get; }

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
        /// Checks the value as not null or whitespace, then adds it to the embed as an inline field.
        /// </summary>
        /// <param name="builder">The embed builder.</param>
        /// <param name="key">The field key.</param>
        /// <param name="value">The field value.</param>
        public static void AutoField(EmbedBuilder builder, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.AddField(key, value, true);
            }
        }

        /// <summary>
        /// Get an embed object for this meta object.
        /// </summary>
        public virtual EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = new EmbedBuilder().WithColor(0, 255, 255).WithTitle(TypeName + ": " + Name)
                .WithUrl(DenizenMetaBot.URL_BASE + WebPath + "/" + Name.Replace(" ", "%20"));
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
