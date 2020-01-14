using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using Discord;
using YamlDotNet.RepresentationModel;
using FreneticUtilities.FreneticExtensions;
using DenizenBot.MetaObjects;

namespace DenizenBot.UtilityProcessors
{
    /// <summary>
    /// Utility class to check a script's validity.
    /// </summary>
    public class ScriptChecker
    {
        /// <summary>
        /// The full original script text.
        /// </summary>
        public string FullOriginalScript;

        /// <summary>
        /// All lines of the script.
        /// </summary>
        public string[] Lines;

        /// <summary>
        /// All lines, pre-trimmed and lowercased.
        /// </summary>
        public string[] CleanedLines;

        /// <summary>
        /// The number of lines that were comments.
        /// </summary>
        public int CommentLines = 0;

        /// <summary>
        /// The number of lines that were blank.
        /// </summary>
        public int BlankLines = 0;

        /// <summary>
        /// The number of lines that were structural (ending with a colon).
        /// </summary>
        public int StructureLines = 0;

        /// <summary>
        /// The number of lines that were code (starting with a dash).
        /// </summary>
        public int CodeLines = 0;

        /// <summary>
        /// A list of all errors about this script.
        /// </summary>
        public List<string> Errors = new List<string>();

        /// <summary>
        /// A list of all warnings about this script.
        /// </summary>
        public List<string> Warnings = new List<string>();

        /// <summary>
        /// A list of all minor warnings about this script.
        /// </summary>
        public List<string> MinorWarnings = new List<string>();

        /// <summary>
        /// A list of informational notices about this script.
        /// </summary>
        public List<string> Infos = new List<string>();

        /// <summary>
        /// A list of debug notices about this script, generally don't actually show to users.
        /// </summary>
        public List<string> Debugs = new List<string>();

        /// <summary>
        /// Construct the ScriptChecker instance from a script string.
        /// </summary>
        /// <param name="script">The script contents string.</param>
        public ScriptChecker(string script)
        {
            FullOriginalScript = script;
            Lines = script.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            CleanedLines = Lines.Select(s => s.Trim()).ToArray();
        }

        /// <summary>
        /// Adds a warning to track.
        /// </summary>
        /// <param name="warnType">The warning type (the list object).</param>
        /// <param name="line">The zero-indexed line the warning is regarding.</param>
        /// <param name="message">The warning message.</param>
        public void Warn(List<string> warnType, int line, string message)
        {
            warnType.Add($"On line {line + 1}: {message}");
        }

        /// <summary>
        /// Clears all comment lines.
        /// </summary>
        public void ClearCommentsFromLines()
        {
            for (int i = 0; i < CleanedLines.Length; i++)
            {
                if (CleanedLines[i].StartsWith("#"))
                {
                    CleanedLines[i] = "";
                    Lines[i] = "";
                    CommentLines++;
                }
                else if (CleanedLines[i] == "")
                {
                    BlankLines++;
                }
                else if (CleanedLines[i].StartsWith("-"))
                {
                    CodeLines++;
                }
                else if (CleanedLines[i].EndsWith(":"))
                {
                    StructureLines++;
                }
            }
        }

        /// <summary>
        /// Performs some minimal script cleaning, based on logic in DenizenCore, that matches a script load in as valid YAML, for use with <see cref="CheckYAML"/>.
        /// </summary>
        /// <returns>The cleaned YAML-friendly script.</returns>
        public string CleanScriptForYAMLProcessing()
        {
            StringBuilder result = new StringBuilder(FullOriginalScript.Length);
            for (int i = 0; i < Lines.Length; i++)
            {
                string line = CleanedLines[i];
                if (!line.EndsWith(":") && line.StartsWith("-"))
                {
                    result.Append(Lines[i].Replace(": ", "<&co>").Replace("#", "<&ns>")).Append("\n");
                }
                else
                {
                    result.Append(Lines[i]).Append("\n");
                }
            }
            result.Append("\n");
            return result.ToString();
        }

        /// <summary>
        /// Checks if the script is even valid YAML (if not, critical error).
        /// </summary>
        public void CheckYAML()
        {
            try
            {
                new YamlStream().Load(new StringReader(CleanScriptForYAMLProcessing()));
            }
            catch (Exception ex)
            {
                Errors.Add("Invalid YAML! Error message: " + ex.Message);
            }
        }

        /// <summary>
        /// Checks the basic format of every line of the script, to locate stray text or useless lines.
        /// </summary>
        public void BasicLineFormatCheck()
        {
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].EndsWith(" "))
                {
                    Warn(MinorWarnings, i, "Stray space after end of line (possible copy/paste mixup. Enable View->Render Whitespace in VS Code).");
                }
                else if (!CleanedLines[i].StartsWith("-") && (CleanedLines[i].Contains(":") && !CleanedLines[i].EndsWith(":")))
                {
                    Warn(Warnings, i, "Text after end of script key (possible malformed line).");
                }
                else if (CleanedLines[i].Length > 0 && !CleanedLines[i].StartsWith("-") && !CleanedLines[i].EndsWith(":"))
                {
                    Warn(Warnings, i, "Useless/invalid line (possibly missing a `-` or a `:`, or just accidentally hit enter or paste).");
                }
            }
        }

        /// <summary>
        /// Checks if "\t" tabs are used (instead of spaces). If so, warning.
        /// </summary>
        public void CheckForTabs()
        {
            if (!FullOriginalScript.Contains("\t"))
            {
                return;
            }
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].Contains("\t"))
                {
                    Warn(Warnings, i, "This script uses the raw tab symbol. Please switch these out for 2 or 4 spaces.");
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if { braces } are used (instead of modern "colon:" syntax). If so, error.
        /// </summary>
        public void CheckForBraces()
        {
            if (!FullOriginalScript.Contains("{"))
            {
                return;
            }
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].EndsWith("{") || Lines[i].EndsWith("}"))
                {
                    Warn(Errors, i, "This script uses outdated { braced } syntax. Please update to modern 'colon:' syntax. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#colon-syntax> for more info.");
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if %ancientdef%s are used (instead of modern "&lt;[defname]&gt;" syntax). If so, error.
        /// </summary>
        public void CheckForAncientDefs()
        {
            if (!FullOriginalScript.Contains("%"))
            {
                return;
            }
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].Contains("%"))
                {
                    Warn(Errors, i, "This script uses ancient %defs%. Please update to modern '<[defname]>' syntax. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#definition-syntax> for more info.");
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if &lt;def[oldDefs]&gt; are used (instead of modern "&lt;[defname]&gt;" syntax). If so, warning.
        /// </summary>
        public void CheckForOldDefs()
        {
            if (!FullOriginalScript.Contains("<def["))
            {
                return;
            }
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].Contains("<def["))
                {
                    Warn(Warnings, i, "This script uses <def[old-defs]>. Please update to modern '<[defname]>' syntax. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#definition-syntax> for more info.");
                    break;
                }
            }
        }

        /// <summary>
        /// Performs the necessary checks on a single argument.
        /// </summary>
        /// <param name="line">The line number.</param>
        /// <param name="commandText">The text of the argument.</param>
        public void CheckSingleArgument(int line, string argument)
        {
            // TODO: Check each argument's tags
            // TODO: check for object notation usage
        }

        /// <summary>
        /// Performs the necessary checks on a single command line.
        /// </summary>
        /// <param name="line">The line number.</param>
        /// <param name="commandText">The text of the command line.</param>
        public void CheckSingleCommand(int line, string commandText)
        {
            string commandName = null; // TODO
            string[] arguments = null; // TODO
            // TODO: Command name validity
            // TODO: Argument count
            // TODO: Check for "quoted" arguments without any spaces in them (pointless).
            if (commandName == "adjust")
            {
                // TODO: Mechanism exists check
            }
            if (commandName == "queue" && arguments.Length == 1 && (arguments[0] == "stop" || arguments[0] == "clear"))
            {
                Warn(MinorWarnings, line, "Old style 'queue clear'. Use the modern 'stop' command instead. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#stop-is-the-new-queue-clear> for more info.");
            }
        }

        /// <summary>
        /// Basic metadata about a known script type.
        /// </summary>
        public class KnownScriptType
        {
            /// <summary>
            /// Keys that must always be present.
            /// </summary>
            public string[] RequiredKeys = new string[0];

            /// <summary>
            /// Keys that generally shouldn't be present unless something's gone wrong.
            /// </summary>
            public string[] LikelyBadKeys = new string[0];

            /// <summary>
            /// Value-based keys.
            /// </summary>
            public string[] ValueKeys = new string[0];

            /// <summary>
            /// Data list keys.
            /// </summary>
            public string[] ListKeys = new string[0];

            /// <summary>
            /// Script keys.
            /// </summary>
            public string[] ScriptKeys = new string[0];

            /// <summary>
            /// Whether to be strict in checks (if true, unrecognize keys will receive a warning).
            /// </summary>
            public bool Strict = false;

            /// <summary>
            /// Whether this type can have random extra scripts attached.
            /// </summary>
            public bool CanHaveRandomScripts = true;

            /// <summary>
            /// Constructs the <see cref="KnownScriptType"/> instance.
            /// </summary>
            public KnownScriptType(string[] required = null, string[] bad = null, string[] valueKeys = null, string[] listKeys = null, string[] scriptKeys = null, bool strict = false, bool canHaveScripts = true)
            {
                RequiredKeys = required ?? RequiredKeys;
                LikelyBadKeys = bad ?? LikelyBadKeys;
                ValueKeys = valueKeys ?? ValueKeys;
                ListKeys = listKeys ?? ListKeys;
                ScriptKeys = scriptKeys ?? ScriptKeys;
                Strict = strict;
                CanHaveRandomScripts = canHaveScripts;
            }
        }

        /// <summary>
        /// A set of all known script type names.
        /// </summary>
        public static readonly Dictionary<string, KnownScriptType> KnownScriptTypes = new Dictionary<string, KnownScriptType>()
        {
            // Denizen Core
            { "custom", new KnownScriptType(bad: new[] { "script", "actions", "events", "steps" }, valueKeys: new[] { "inherit", "*" }, scriptKeys: new[] { "tags.*", "mechanisms.*" }, strict: false, canHaveScripts: false) },
            { "procedure", new KnownScriptType(required: new[] { "script" }, bad: new[] { "events", "actions", "steps" }, valueKeys: new[] { "definitions" }, scriptKeys: new[] { "script" }, strict: true) },
            { "task", new KnownScriptType(required: new[] { "script" }, bad: new[] { "events", "actions", "steps" }, valueKeys: new[] { "definitions" }, scriptKeys: new[] { "script" }, strict: false) },
            { "world", new KnownScriptType(required: new[] { "events" }, bad: new[] { "script", "actions", "steps" }, scriptKeys: new[] { "events.*" }, strict: false) },
            { "yaml data", new KnownScriptType(bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "*" }, listKeys: new[] { "*" }, strict: false, canHaveScripts: false) },
            // Denizen-Bukkit
            { "assignment", new KnownScriptType(required: new[] { "actions", "interact scripts" }, bad: new[] { "script", "steps", "events" }, valueKeys: new[] { "default constants.*", "constants.*" }, listKeys: new[] { "interact scripts" }, scriptKeys: new[] { "actions.*" }, strict: true) },
            { "book", new KnownScriptType(required: new[] { "title", "author", "text" }, bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "title", "author", "signed" }, listKeys: new[] { "text" }, strict: true, canHaveScripts: false) },
            { "command", new KnownScriptType(required: new[] { "name", "description", "usage", "script" }, bad: new[] { "steps", "actions", "events" }, valueKeys: new[] { "name", "description", "usage", "permission", "permission message" }, listKeys: new[] { "aliases" }, scriptKeys: new[] { "allowed help", "tab complete", "script" }, strict: false) },
            { "economy", new KnownScriptType(required: new[] { "priority", "name single", "name plural", "digits", "format", "balance", "has", "withdraw", "deposit" }, bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "priority", "name single", "name plural", "digits", "format", "balance", "has" }, scriptKeys: new[] { "withdraw", "deposit" }, strict: true, canHaveScripts: false) },
            { "entity", new KnownScriptType(required: new[] { "entity_type" }, bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "*" }, strict: false) },
            { "format", new KnownScriptType(required: new[] { "format" }, bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "format" }, strict: true, canHaveScripts: false) },
            { "interact", new KnownScriptType(required: new[] { "steps" }, bad: new[] { "script", "actions", "events" }, scriptKeys: new[] { "steps.*" }, strict: true) },
            { "inventory", new KnownScriptType(required: new[] { "inventory" }, bad: new[] { "script", "steps", "actions", "events" }, valueKeys: new[] { "inventory", "title", "size", "definitions.*" }, scriptKeys: new[] { "procedural items" }, listKeys: new[] { "slots" }, strict: true, canHaveScripts: false) },
            { "item", new KnownScriptType(required: new[] { "material" }, bad: new[] { "script", "steps", "actions", "events" }, valueKeys: new[] { "material", "mechanisms.*", "display name", "durability", "recipes.*", "no_id", "color", "book" }, listKeys: new[] { "mechanisms.*", "lore", "enchantments", "recipes.*" }, strict: false, canHaveScripts: false) },
            { "map", new KnownScriptType(bad: new[] { "script", "steps", "actions", "events" }, valueKeys: new[] { "original", "display name", "auto update", "objects.*" }, strict: true, canHaveScripts: false) }
        };

        /// <summary>
        /// Checks a dictionary full of script containers, performing all checks on the scripts from there on.
        /// </summary>
        public void CheckAllContainers(Dictionary<LineTrackedString, object> scriptContainers)
        {
            foreach (KeyValuePair<LineTrackedString, object> scriptPair in scriptContainers)
            {
                void warnScript(List<string> warns, int line, string warning)
                {
                    Warn(warns, line, $"In script '{scriptPair.Key.Text.Replace('`', '\'')}': {warning}");
                }
                try
                {
                    Dictionary<LineTrackedString, object> scriptSection = (Dictionary<LineTrackedString, object>)scriptPair.Value;
                    if (!scriptSection.TryGetValue(new LineTrackedString(0, "type"), out object typeValue) || !(typeValue is LineTrackedString typeString))
                    {
                        warnScript(Errors, scriptPair.Key.Line, "Missing 'type' key!");
                        continue;
                    }
                    if (!KnownScriptTypes.TryGetValue(typeString.Text, out KnownScriptType scriptType))
                    {
                        warnScript(Errors, typeString.Line, "Unknown script type (possibly a typo?)!");
                        continue;
                    }
                    foreach (string key in scriptType.RequiredKeys)
                    {
                        if (!scriptSection.ContainsKey(new LineTrackedString(0, key)))
                        {
                            warnScript(Warnings, typeString.Line, $"Missing required key `{key}` (check `!lang {typeString.Text} script containers` for format rules)!");
                        }
                    }
                    foreach (string key in scriptType.LikelyBadKeys)
                    {
                        if (scriptSection.ContainsKey(new LineTrackedString(0, key)))
                        {
                            warnScript(Warnings, typeString.Line, $"Unexpected key `{key}` (probably doesn't belong in this script type - check `!lang {typeString.Text} script containers` for format rules)!");
                        }
                    }
                    bool matchesSet(string key, string[] keySet)
                    {
                        return keySet.Contains(key) || keySet.Contains($"{key}.*") || keySet.Contains("*");
                    }
                    foreach (KeyValuePair<LineTrackedString, object> keyPair in scriptSection)
                    {
                        string keyName = keyPair.Key.Text;
                        if (keyName == "debug" || keyName == "speed" || keyName == "type")
                        {
                            continue;
                        }
                        if (keyPair.Value is List<LineTrackedString> keyPairList)
                        {
                            if (matchesSet(keyName, scriptType.ListKeys))
                            {
                                foreach (LineTrackedString str in keyPairList)
                                {
                                    CheckSingleArgument(str.Line, str.Text);
                                }
                            }
                            else if (matchesSet(keyName, scriptType.ScriptKeys))
                            {
                                // TODO: Script check
                            }
                            else if (matchesSet(keyName, scriptType.ValueKeys))
                            {
                                warnScript(Warnings, keyPair.Key.Line, $"Bad key `{keyName}` (was expected to be a direct Value, but was instead a list - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (scriptType.Strict)
                            {
                                warnScript(Warnings, keyPair.Key.Line, $"Unexpected list key `{keyName}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (scriptType.CanHaveRandomScripts)
                            {
                                // TODO: Script check
                            }
                            else if (typeString.Text != "yaml data")
                            {
                                foreach (LineTrackedString str in keyPairList)
                                {
                                    CheckSingleArgument(str.Line, str.Text);
                                }
                            }

                        }
                        else if (keyPair.Value is LineTrackedString keyPairLine)
                        {
                            if (matchesSet(keyName, scriptType.ValueKeys))
                            {
                                CheckSingleArgument(keyPair.Key.Line, keyPairLine.Text);
                            }
                            else if (matchesSet(keyName, scriptType.ListKeys) || matchesSet(keyName, scriptType.ScriptKeys))
                            {
                                warnScript(Warnings, keyPair.Key.Line, $"Bad key `{keyName}` (was expected to be a list or script, but was instead a direct Value - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (scriptType.Strict)
                            {
                                warnScript(Warnings, keyPair.Key.Line, $"Unexpected value key `{keyName}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else
                            {
                                CheckSingleArgument(keyPair.Key.Line, keyPairLine.Text);
                            }
                        }
                        else // Must be a submap
                        {
                            string keyText = keyName + ".*";
                            if (scriptType.ValueKeys.Contains(keyText) || scriptType.ListKeys.Contains(keyText) || scriptType.ScriptKeys.Contains(keyText)
                                || scriptType.ValueKeys.Contains("*") || scriptType.ListKeys.Contains("*") || scriptType.ScriptKeys.Contains("*"))
                            {
                                // TODO: Check submapped stuff
                            }
                            else if (scriptType.Strict)
                            {
                                warnScript(Warnings, keyPair.Key.Line, $"Unexpected submapping key `{keyName}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else
                            {
                                // TODO: Check submapped stuff
                            }
                        }
                    }
                    if (typeString.Text == "command")
                    {
                        if (scriptSection.TryGetValue(new LineTrackedString(0, "name"), out object nameValue) && scriptSection.TryGetValue(new LineTrackedString(0, "usage"), out object usageValue))
                        {
                            if (usageValue is LineTrackedString usageString && nameValue is LineTrackedString nameString)
                            {
                                if (!usageString.Text.StartsWith($"/{nameString.Text}"))
                                {
                                    warnScript(MinorWarnings, usageString.Line, $"Command script usage key doesn't match the name key (the name has is the actual thing you need to type in-game, the usage is for '/help')!");
                                }
                            }
                        }
                    }
                    else if (typeString.Text == "assignment")
                    {
                        if (scriptSection.TryGetValue(new LineTrackedString(0, "actions"), out object actionsValue) && actionsValue is Dictionary<LineTrackedString, object> actionsMap)
                        {
                            foreach (LineTrackedString actionValue in actionsMap.Keys)
                            {
                                string actionName = actionValue.Text.Substring("on ".Length);
                                if (!Program.CurrentMeta.Actions.ContainsKey(actionName))
                                {
                                    bool exists = false;
                                    foreach (MetaAction action in Program.CurrentMeta.Actions.Values)
                                    {
                                        if (action.RegexMatcher.IsMatch(actionName))
                                        {
                                            exists = true;
                                            break;
                                        }
                                    }
                                    if (!exists)
                                    {
                                        warnScript(Warnings, actionValue.Line, $"Assignment script action listed doesn't exist. (Check `!act ...` to find proper action names)!");
                                    }
                                }
                            }
                        }
                    }
                    else if (typeString.Text == "events")
                    {
                        if (scriptSection.TryGetValue(new LineTrackedString(0, "events"), out object eventsValue) && eventsValue is Dictionary<LineTrackedString, object> eventsMap)
                        {
                            foreach (LineTrackedString eventValue in eventsMap.Keys)
                            {
                                string eventName = eventValue.Text.Substring("on ".Length);
                                if (!Program.CurrentMeta.Events.ContainsKey(eventName))
                                {
                                    bool exists = false;
                                    foreach (MetaEvent evt in Program.CurrentMeta.Events.Values)
                                    {
                                        if (evt.RegexMatcher.IsMatch(eventName))
                                        {
                                            exists = true;
                                            break;
                                        }
                                    }
                                    if (!exists)
                                    {
                                        warnScript(Warnings, eventValue.Line, $"Script Event listed doesn't exist. (Check `!event ...` to find proper event lines)!");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    warnScript(Warnings, scriptPair.Key.Line, $"Internal exception (check bot debug console)!");
                    Console.WriteLine($"Script check exception: {ex}");
                }
            }
        }

        /// <summary>
        /// Helper class for strings that remember where they came from.
        /// </summary>
        public class LineTrackedString
        {
            /// <summary>
            /// The text of the line.
            /// </summary>
            public string Text;

            /// <summary>
            /// The line number.
            /// </summary>
            public int Line;

            /// <summary>
            /// Constructs the LineTrackedString.
            /// </summary>
            public LineTrackedString(int line, string text)
            {
                Line = line;
                Text = text;
            }

            /// <summary>
            /// HashCode impl, for Dictionary functionality.
            /// </summary>
            public override int GetHashCode()
            {
                return Text.GetHashCode();
            }

            /// <summary>
            /// Equals impl, for Dictionary functionality.
            /// </summary>
            public override bool Equals(object obj)
            {
                return (obj is LineTrackedString lts2) && Text == lts2.Text;
            }

            /// <summary>
            /// ToString override, returns <see cref="Text"/>.
            /// </summary>
            public override string ToString()
            {
                return Text;
            }
        }

        /// <summary>
        /// Gathers a dictionary of all actual containers, checking for errors as it goes, and returning the dictionary.
        /// </summary>
        public Dictionary<LineTrackedString, object> GatherActualContainers()
        {
            Dictionary<LineTrackedString, object> rootScriptSection = new Dictionary<LineTrackedString, object>();
            Dictionary<int, Dictionary<LineTrackedString, object>> spacedsections = new Dictionary<int, Dictionary<LineTrackedString, object>>() { { 0, rootScriptSection } };
            Dictionary<LineTrackedString, object> currentSection = rootScriptSection;
            int pspaces = 0;
            LineTrackedString secwaiting = null;
            List<LineTrackedString> clist = null;
            for (int i = 0; i < Lines.Length; i++)
            {
                string cleaned = CleanedLines[i];
                if (cleaned.Length == 0)
                {
                    continue;
                }
                string line = Lines[i].Replace("\t", "    ");
                int spaces;
                for (spaces = 0; spaces < line.Length; spaces++)
                {
                    if (line[spaces] != ' ')
                    {
                        break;
                    }
                }
                if (spaces < pspaces)
                {
                    if (spacedsections.TryGetValue(spaces, out Dictionary<LineTrackedString, object> temp))
                    {
                        currentSection = temp;
                        foreach (int test in new List<int>(spacedsections.Keys))
                        {
                            if (test > spaces)
                            {
                                spacedsections.Remove(test);
                            }
                        }
                    }
                    else
                    {
                        Warn(Warnings, i, $"Simple spacing error - shrunk unexpectedly to new space count, from {pspaces} down to {spaces}, while expecting any of: {string.Join(", ", spacedsections.Keys)}.");
                        pspaces = spaces;
                        continue;
                    }
                }
                if (cleaned.StartsWith("- "))
                {
                    if (clist == null)
                    {
                        if (spaces >= pspaces && secwaiting != null)
                        {
                            clist = new List<LineTrackedString>();
                            currentSection[secwaiting] = clist;
                            secwaiting = null;
                        }
                        else
                        {
                            Warn(Warnings, i, "Line purpose unknown, attempted list entry when not building a list (likely line format error, perhaps missing or misplaced a `:` on lines above?).");
                            pspaces = spaces;
                            continue;
                        }
                    }
                    clist.Add(new LineTrackedString(i, cleaned.Substring("- ".Length)));
                    continue;
                }
                clist = null;
                string startofline;
                string endofline = "";
                if (cleaned.EndsWith(":"))
                {
                    startofline = cleaned.Substring(0, cleaned.Length - 1);
                }
                else if (cleaned.Contains(": "))
                {
                    startofline = cleaned.BeforeAndAfter(": ", out endofline);
                }
                else
                {
                    Warn(Warnings, i, "Line purpose unknown, no identifier (missing a `:` or a `-`?).");
                    continue;
                }
                if (startofline.Length == 0)
                {
                    Warn(Warnings, i, "key line missing contents (misplaced a `:`)?");
                    continue;
                }
                if (spaces > pspaces)
                {
                    if (secwaiting == null)
                    {
                        Warn(Warnings, i, "Spacing grew for no reason (missing a ':', or accidental over-spacing?).");
                        pspaces = spaces;
                        continue;
                    }
                    Dictionary<LineTrackedString, object> sect = new Dictionary<LineTrackedString, object>();
                    currentSection[secwaiting] = sect;
                    currentSection = sect;
                    spacedsections[spaces] = sect;
                    secwaiting = null;
                }
                if (endofline.Length == 0)
                {
                    secwaiting = new LineTrackedString(i, startofline.ToLowerFast());
                }
                else
                {
                    currentSection[new LineTrackedString(i, startofline.ToLowerFast())] = new LineTrackedString(i, endofline);
                }
                pspaces = spaces;
            }
            return rootScriptSection;
        }

        /// <summary>
        /// Adds <see cref="Infos"/> entries for basic statistics.
        /// </summary>
        public void CollectStatisticInfos()
        {
            Infos.Add($"(Statistics) Total structural lines: {StructureLines}");
            Infos.Add($"(Statistics) Total live code lines: {CodeLines}");
            Infos.Add($"(Statistics) Total comment lines: {CommentLines}");
            Infos.Add($"(Statistics) Total blank lines: {BlankLines}");
        }

        /// <summary>
        /// Runs the full script check.
        /// </summary>
        public void Run()
        {
            ClearCommentsFromLines();
            CheckYAML();
            BasicLineFormatCheck();
            CheckForTabs();
            CheckForBraces();
            CheckForAncientDefs();
            CheckForOldDefs();
            Dictionary<LineTrackedString, object> containers = GatherActualContainers();
            CheckAllContainers(containers);
            CollectStatisticInfos();
        }

        /// <summary>
        /// Gets the result Discord embed for the script check.
        /// </summary>
        /// <returns>The embed to send.</returns>
        public Embed GetResult()
        {
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Script Check Results").WithThumbnailUrl((Errors.Count + Warnings.Count > 0) ? Constants.WARNING_ICON : Constants.INFO_ICON);
            void embedList(List<string> list, string title)
            {
                if (list.Count > 0)
                {
                    StringBuilder thisListResult = new StringBuilder(list.Count * 200);
                    foreach (string entry in list)
                    {
                        if (embed.Length + thisListResult.Length + entry.Length < 1800)
                        {
                            thisListResult.Append($"{entry}\n");
                        }
                    }
                    if (thisListResult.Length > 0)
                    {
                        embed.AddField(title, thisListResult.ToString());
                    }
                }
            }
            embedList(Errors, "Encountered Critical Errors");
            embedList(Warnings, "Script Warnings");
            embedList(MinorWarnings, "Minor Warnings");
            embedList(Infos, "Other Script Information");
            foreach (string debug in Debugs)
            {
                Console.WriteLine($"Script checker debug: {debug}");
            }
            return embed.Build();
        }
    }
}
