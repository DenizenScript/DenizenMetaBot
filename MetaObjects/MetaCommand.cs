using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// A documented command.
    /// </summary>
    public class MetaCommand : MetaObject
    {
        public override string TypeName => "Command";

        public override string WebPath => "cmds";

        public override string Name => CommandName;

        public override void AddTo(MetaDocs docs)
        {
            docs.Commands.Add(CommandName, this);
        }

        /// <summary>
        /// The name of the command.
        /// </summary>
        public string CommandName;

        /// <summary>
        /// How many arguments are required, minimum.
        /// </summary>
        public int Required;

        /// <summary>
        /// The syntax guide.
        /// </summary>
        public string Syntax;

        /// <summary>
        /// The short description.
        /// </summary>
        public string Short;

        /// <summary>
        /// The long-form description.
        /// </summary>
        public string Description;

        /// <summary>
        /// Tags documented for this command. One tag per string.
        /// </summary>
        public string[] Tags;

        /// <summary>
        /// Sample usages.
        /// </summary>
        public List<string> Usages = new List<string>();

        public override EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = base.GetEmbed();
            AutoField(builder, "Syntax", Syntax);
            AutoField(builder, "Short Description", Short);
            builder.Description = Description.Length > 600 ? Description.Substring(0, 500) + "..." : Description;
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
                    Tags = value.Split('\n');
                    return true;
                case "usage":
                    Usages.Add(value);
                    return true;
                default:
                    return base.ApplyValue(key, value);

            }
        }
    }
}
