﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using FreneticUtilities.FreneticExtensions;

namespace DenizenBot.MetaObjects
{
    /// <summary>
    /// A documented event.
    /// </summary>
    public class MetaEvent : MetaObject
    {
        public override MetaType Type => MetaDocs.META_TYPE_EVENT;

        public override string Name => Events[0];

        public override void AddTo(MetaDocs docs)
        {
            docs.Events.Add(Name.ToLowerFast(), this);
        }

        /// <summary>
        /// The names of the event.
        /// </summary>
        public string[] Events = new string[0];

        /// <summary>
        /// Switches available to the event.
        /// </summary>
        public List<string> Switches = new List<string>();

        /// <summary>
        /// The regex matcher.
        /// </summary>
        public Regex RegexMatcher = null;

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

        /// <summary>
        /// Whether the event is cancellable.
        /// </summary>
        public bool Cancellable = false;

        /// <summary>
        /// Sample usages.
        /// </summary>
        public List<string> Usages = new List<string>();

        public override EmbedBuilder GetEmbed()
        {
            EmbedBuilder builder = base.GetEmbed();
            AutoField(builder, "Other Event Lines", string.Join("\n", Events.Skip(1)));
            AutoField(builder, "Switches", string.Join("\n", Switches));
            AutoField(builder, "Triggers", Triggers);
            AutoField(builder, "Context", string.Join("\n", Context));
            AutoField(builder, "Determine", string.Join("\n", Determinations));
            if (Cancellable)
            {
                AutoField(builder, "Cancellable", "True - this adds `<context.cancelled>` and determines `cancelled` + `cancelled:false`.");
            }
            return builder;
        }

        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "events":
                    Events = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "triggers":
                    Triggers = value;
                    return true;
                case "regex":
                    RegexMatcher = new Regex(value, RegexOptions.Compiled);
                    return true;
                case "switch":
                    Switches.Add(value);
                    return true;
                case "context":
                    Context = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "determine":
                    Determinations = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    return true;
                case "cancellable":
                    Cancellable = value.Trim().ToLowerFast() == "true";
                    return true;
                default:
                    return base.ApplyValue(key, value);

            }
        }
    }
}