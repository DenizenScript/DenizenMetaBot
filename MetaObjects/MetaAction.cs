using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using FreneticUtilities.FreneticExtensions;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// A documented action.
    /// </summary>
    public class MetaAction : MetaObject
    {
        public override MetaType Type => MetaDocs.META_TYPE_ACTION;

        public override string Name => Actions[0];

        public override void AddTo(MetaDocs docs)
        {
            docs.Actions.Add(CleanName, this);
        }

        public override IEnumerable<string> MultiNames => Actions;

        /// <summary>
        /// The names of the action.
        /// </summary>
        public string[] Actions = new string[0];

        /// <summary>
        /// The trigger reason.
        /// </summary>
        public string Triggers = "";

        /// <summary>
        /// Context tags. One tag per string.
        /// </summary>
        public string[] Context = new string[0];

        /// <summary>
        /// Determination options. One Determination per string.
        /// </summary>
        public string[] Determinations = new string[0];

        public override EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = base.GetEmbed();
            AutoField(builder, "Other Action Lines", string.Join("\n", Actions.Skip(1)));
            AutoField(builder, "Triggers", Triggers);
            AutoField(builder, "Context", string.Join("\n", Context));
            AutoField(builder, "Determine", string.Join("\n", Determinations));
            return builder;
        }

        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "actions":
                    Actions = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    HasMultipleNames = Actions.Length > 1;
                    return true;
                case "triggers":
                    Triggers = value;
                    return true;
                case "context":
                    Context = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "determine":
                    Determinations = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            string allActions = string.Join('\n', Actions);
            string allContexts = string.Join('\n', Context);
            string allDeterminations = string.Join('\n', Determinations);
            return $"{baseText}\n{allActions}\n{Triggers}\n{allContexts}\n{allDeterminations}";
        }
    }
}
