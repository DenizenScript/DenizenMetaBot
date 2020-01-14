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
        /// Represents a warning about a script.
        /// </summary>
        public class ScriptWarning
        {
            /// <summary>
            /// A unique key for this *type* of warning.
            /// </summary>
            public string WarningUniqueKey;

            /// <summary>
            /// The locally customized message form.
            /// </summary>
            public string CustomMessageForm;

            /// <summary>
            /// The line this applies to.
            /// </summary>
            public int Line;
        }

        /// <summary>
        /// A list of all errors about this script.
        /// </summary>
        public List<ScriptWarning> Errors = new List<ScriptWarning>();

        /// <summary>
        /// A list of all warnings about this script.
        /// </summary>
        public List<ScriptWarning> Warnings = new List<ScriptWarning>();

        /// <summary>
        /// A list of all minor warnings about this script.
        /// </summary>
        public List<ScriptWarning> MinorWarnings = new List<ScriptWarning>();

        /// <summary>
        /// A list of informational notices about this script.
        /// </summary>
        public List<ScriptWarning> Infos = new List<ScriptWarning>();

        /// <summary>
        /// A list of debug notices about this script, generally don't actually show to users.
        /// </summary>
        public List<string> Debugs = new List<string>();

        /// <summary>
        /// A track of all script names that appear to be injected, for false-warning reduction.
        /// </summary>
        public List<string> Injects = new List<string>();

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
        public void Warn(List<ScriptWarning> warnType, int line, string key, string message)
        {
            warnType.Add(new ScriptWarning() { Line = line, WarningUniqueKey = key, CustomMessageForm = message });
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
                else if (line.EndsWith(":") && !line.StartsWith("-"))
                {
                    result.Append(Lines[i].Replace("*", "asterisk").Replace(".", "dot")).Append("\n");
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
                Warn(Errors, -1, "yaml_load", "Invalid YAML! Error message: " + ex.Message);
                Console.WriteLine($"YAML error: {ex}\n\nFrom:\n{CleanScriptForYAMLProcessing()}");
            }
        }

        /// <summary>
        /// Looks for injects, to prevent issues with later checks.
        /// </summary>
        public void LoadInjects()
        {
            for (int i = 0; i < CleanedLines.Length; i++)
            {
                if (CleanedLines[i].StartsWith("- inject "))
                {
                    string line = CleanedLines[i].Substring("- inject ".Length);
                    if (line.Contains("locally"))
                    {
                        for (int x = i; x >= 0; x--)
                        {
                            if (CleanedLines[x].Length > 0 && CleanedLines[x].EndsWith(":") && !Lines[x].Replace("\t", "    ").StartsWith(" "))
                            {
                                string scriptName = CleanedLines[x].Substring(0, CleanedLines[x].Length - 1);
                                Injects.Add(scriptName);
                                Console.WriteLine($"ScriptChecker: Inject locally became {scriptName}");
                                break;
                            }
                        }
                    }
                    else
                    {
                        string target = line.Before(" ");
                        Injects.Add(target.Before("."));
                        if (target.Contains("<"))
                        {
                            Injects.Add("*");
                            Console.WriteLine("ScriptChecker: Inject EVERYTHING");
                        }
                    }
                }
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
                    Warn(MinorWarnings, i, "stray_space_eol", "Stray space after end of line (possible copy/paste mixup. Enable View->Render Whitespace in VS Code).");
                }
                else if (CleanedLines[i].Length > 0 && !CleanedLines[i].StartsWith("-") && !CleanedLines[i].Contains(":"))
                {
                    Warn(Warnings, i, "useless_invalid_line", "Useless/invalid line (possibly missing a `-` or a `:`, or just accidentally hit enter or paste).");
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
                    Warn(Warnings, i, "raw_tab_symbol", "This script uses the raw tab symbol. Please switch these out for 2 or 4 spaces.");
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
                    Warn(Errors, i, "brace_syntax", "This script uses outdated { braced } syntax. Please update to modern 'colon:' syntax. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#colon-syntax> for more info.");
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
                    Warn(Errors, i, "ancient_defs", "This script uses ancient %defs%. Please update to modern '<[defname]>' syntax. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#definition-syntax> for more info.");
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
                    Warn(Warnings, i, "old_defs", "This script uses <def[old-defs]>. Please update to modern '<[defname]>' syntax. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#definition-syntax> for more info.");
                    break;
                }
            }
        }

        /// <summary>
        /// Performs the necessary checks on a single tag.
        /// </summary>
        /// <param name="line">The line number.</param>
        /// <param name="commandText">The text of the tag.</param>
        public void CheckSingleTag(int line, string tag)
        {
            tag = tag.ToLowerFast();
            int brackets = 0;
            List<string> tagParts = new List<string>(tag.CountCharacter('.'));
            int firstBracket = 0;
            int start = 0;
            bool foundABracket = false;
            for (int i = 0; i < tag.Length; i++)
            {
                if (tag[i] == '[')
                {
                    brackets++;
                    if (brackets == 1)
                    {
                        tagParts.Add(tag.Substring(start, i - start));
                        foundABracket = true;
                        start = i;
                        firstBracket = i;
                    }
                }
                else if (tag[i] == ']')
                {
                    brackets--;
                    if (brackets == 0)
                    {
                        CheckSingleArgument(line, tag.Substring(firstBracket + 1, i - firstBracket - 1));
                    }
                }
                else if (tag[i] == '.' && brackets == 0)
                {
                    if (!foundABracket)
                    {
                        tagParts.Add(tag.Substring(start, i - start));
                    }
                    foundABracket = false;
                    start = i + 1;
                }
                else if (tag[i] == '|' && brackets == 0 && i + 1 < tag.Length && tag[i + 1] == '|')
                {
                    if (!foundABracket)
                    {
                        tagParts.Add(tag.Substring(start, i - start));
                    }
                    CheckSingleArgument(line, tag.Substring(i + 2));
                    foundABracket = true;
                    break;
                }
            }
            if (!foundABracket)
            {
                tagParts.Add(tag.Substring(start, tag.Length - start));
            }
            if (tagParts[0] == "entry" || tagParts[0] == "context")
            {
                return;
            }
            if (!Program.CurrentMeta.TagBases.Contains(tagParts[0]) && tagParts[0].Length > 0)
            {
                Warn(Warnings, line, "bad_tag_base", $"Invalid tag base `{tagParts[0].Replace('`', '\'')}` (check `!tag ...` to find valid tags).");
            }
            else if (tagParts[0].EndsWith("tag"))
            {
                Warn(Warnings, line, "xtag_notation", $"'XTag' notation is for documentation purposes, and is not to be used literally in a script. (replace the 'XTag' text with a valid real tagbase that returns a tag of that type).");
            }
            for (int i = 1; i < tagParts.Count; i++)
            {
                if (!Program.CurrentMeta.TagParts.Contains(tagParts[i]))
                {
                    Warn(Warnings, line, "bad_tag_part", $"Invalid tag part `{tagParts[i].Replace('`', '\'')}` (check `!tag ...` to find valid tags).");
                    if (tagParts[i].EndsWith("tag"))
                    {
                        Warn(Warnings, line, "xtag_notation", $"'XTag' notation is for documentation purposes, and is not to be used literally in a script. (replace the 'XTag' text with a valid real tagbase that returns a tag of that type).");
                    }
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
            string argNoArrows = argument.Replace("<-", "arrow_left").Replace("->", "arrow_right");
            if (argument.Length > 2 && argNoArrows.CountCharacter('<') != argNoArrows.CountCharacter('>'))
            {
                Warn(Warnings, line, "uneven_tags", $"Uneven number of tag marks (forgot to close a tag?).");
                Console.WriteLine("Uneven tag marks for: " + argNoArrows);
            }
            int tagIndex = argNoArrows.IndexOf('<');
            while (tagIndex != -1)
            {
                int bracks = 0;
                int endIndex = -1;
                for (int i = tagIndex; i < argNoArrows.Length; i++)
                {
                    if (argNoArrows[i] == '<')
                    {
                        bracks++;
                    }
                    if (argNoArrows[i] == '>')
                    {
                        if (bracks == 0)
                        {
                            endIndex = i;
                            break;
                        }
                        bracks--;
                    }
                }
                if (endIndex == -1)
                {
                    break;
                }
                string tag = argNoArrows.Substring(tagIndex + 1, endIndex - tagIndex - 1);
                CheckSingleTag(line, tag);
                tagIndex = argNoArrows.IndexOf('<', endIndex);
            }
        }

        /// <summary>
        /// Build args, as copied from Denizen Core -> ArgumentHelper.
        /// </summary>
        /// <param name="line">The line number.</param>
        /// <param name="stringArgs">The raw arguments input.</param>
        /// <returns>The argument array.</returns>
        public string[] BuildArgs(int line, string stringArgs)
        {
            stringArgs = stringArgs.Trim().Replace('\r', ' ').Replace('\n', ' ');
            List<string> matchList = new List<string>(stringArgs.CountCharacter(' '));
            int start = 0;
            int len = stringArgs.Length;
            char currentQuote = '\0';
            for (int i = 0; i < len; i++)
            {
                char c = stringArgs[i];
                if (c == ' ' && currentQuote == '\0')
                {
                    if (i > start)
                    {
                        matchList.Add(stringArgs.Substring(start, i - start));
                    }
                    start = i + 1;
                }
                else if (c == '"' || c == '\'')
                {
                    if (currentQuote == '\0')
                    {
                        if (i - 1 < 0 || stringArgs[i - 1] == ' ')
                        {
                            currentQuote = c;
                            start = i + 1;
                        }
                    }
                    else if (currentQuote == c)
                    {
                        if (i + 1 >= len || stringArgs[i + 1] == ' ')
                        {
                            currentQuote = '\0';
                            if (i >= start)
                            {
                                string matched = stringArgs.Substring(start, i - start);
                                matchList.Add(matched);
                                if (!matched.Contains(" "))
                                {
                                    Warn(MinorWarnings, line, "bad_quotes", "Pointless quotes (arguments quoted but do not contain spaces).");
                                }
                            }
                            i++;
                            start = i + 1;
                        }
                    }
                }
            }
            if (currentQuote != '\0')
            {
                Warn(MinorWarnings, line, "missing_quotes", "Uneven quotes (forgot to close a quote?).");
            }
            if (start < len)
            {
                matchList.Add(stringArgs.Substring(start));
            }
            return matchList.ToArray();
        }

        /// <summary>
        /// Performs the necessary checks on a single command line.
        /// </summary>
        /// <param name="line">The line number.</param>
        /// <param name="commandText">The text of the command line.</param>
        /// <param name="definitions">The definitions tracker.</param>
        public void CheckSingleCommand(int line, string commandText, HashSet<string> definitions)
        {
            if (commandText.Contains("@"))
            {
                Warn(Warnings, line, "raw_object_notation", "This line appears to contain raw object notation. There is almost always a better way to write a line than using raw object notation. Consider the relevant object constructor tags.");
            }
            string[] parts = commandText.Split(' ', 2);
            string commandName = parts[0].ToLowerFast();
            if (commandName.StartsWith("~") || commandName.StartsWith("^"))
            {
                commandName = commandName.Substring(1);
            }
            string[] arguments = parts.Length == 1 ? new string[0] : BuildArgs(line, parts[1]);
            if (!Program.CurrentMeta.Commands.TryGetValue(commandName, out MetaCommand command))
            {
                if (commandName != "case" && commandName != "default")
                {
                    Warn(Errors, line, "unknown_command", $"Unknown command `{commandName.Replace('`', '\'')}` (typo? Use `!command [...]` to find a valid command).");
                }
                return;
            }
            if (arguments.Length < command.Required)
            {
                Warn(Errors, line, "too_few_args", $"Insufficient arguments... the `{command.Name}` command requires at least {command.Required} arguments, but you only provided {arguments.Length}.");
            }
            if (commandName == "adjust")
            {
                string mechanism = arguments.FirstOrDefault(s => s.Contains(":") && !s.StartsWith("def:")) ?? arguments.FirstOrDefault(s => !s.Contains("<"));
                if (mechanism == null)
                {
                    Warn(Errors, line, "bad_adjust_no_mech", $"Malformed adjust command. No mechanism input given.");
                }
                else
                {
                    mechanism = mechanism.Before(':').ToLowerFast();
                    if (!Program.CurrentMeta.Mechanisms.Values.Any(mech => mech.MechName == mechanism))
                    {
                        Warn(Errors, line, "bad_adjust_unknown_mech", $"Malformed adjust command. Mechanism name given is unrecognized.");
                        Console.WriteLine($"Unrecognized mechanism '{mechanism}' for script check line {line}.");
                    }
                }
            }
            else if (commandName == "inject")
            {
                definitions.Add("*");
            }
            else if (commandName == "queue" && arguments.Length == 1 && (arguments[0] == "stop" || arguments[0] == "clear"))
            {
                Warn(MinorWarnings, line, "queue_clear", "Old style 'queue clear'. Use the modern 'stop' command instead. Refer to <https://guide.denizenscript.com/guides/troubleshooting/updates-since-videos.html#stop-is-the-new-queue-clear> for more info.");
            }
            else if (commandName == "define")
            {
                string defName = arguments[0].Before(":").ToLowerFast();
                definitions.Add(defName);
            }
            else if (commandName == "foreach" || commandName == "while" || commandName == "repeat")
            {
                string asArgument = arguments.FirstOrDefault(s => s.StartsWith("as:"));
                if (asArgument == null)
                {
                    asArgument = "value";
                }
                else
                {
                    asArgument = asArgument.Substring("as:".Length);
                }
                definitions.Add(asArgument.ToLowerFast());
                if (commandName == "foreach")
                {
                    definitions.Add("loop_index");
                }
            }
            string saveArgument = arguments.FirstOrDefault(s => s.StartsWith("save:"));
            if (saveArgument != null)
            {
                definitions.Add(ENTRY_PREFIX + saveArgument.Substring("save:".Length).ToLowerFast());
            }
            foreach (string argument in arguments)
            {
                CheckSingleArgument(line, argument);
                int entrySpot = argument.IndexOf("<entry[");
                if (entrySpot != -1)
                {
                    entrySpot += "<entry[".Length;
                    int endSpot = argument.IndexOf("]", entrySpot);
                    if (endSpot != -1)
                    {
                        string entryText = argument.Substring(entrySpot, endSpot - entrySpot).ToLowerFast();
                        if (!definitions.Contains(ENTRY_PREFIX + entryText) && !definitions.Contains("*"))
                        {
                            Warn(Warnings, line, "entry_to_nowhere", "entry[...] tag points to non-existent save entry (typo, or bad copypaste?).");
                        }
                    }
                }
                int defSpot = argument.IndexOf("<[");
                if (defSpot != -1)
                {
                    defSpot += "<[".Length;
                    int endSpot = argument.IndexOf("]", defSpot);
                    if (endSpot != -1)
                    {
                        string defText = argument.Substring(defSpot, endSpot - defSpot).ToLowerFast();
                        if (!definitions.Contains(defText) && !definitions.Contains("*"))
                        {
                            Warn(Warnings, line, "def_of_nothing", "Definition tag points to non-existent definition (typo, or bad copypaste?).");
                        }
                    }
                }
            }
        }

        private static readonly string ENTRY_PREFIX = ((char)0x01) + "entry_";

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
                void warnScript(List<ScriptWarning> warns, int line, string key, string warning)
                {
                    Warn(warns, line, key, $"In script `{scriptPair.Key.Text.Replace('`', '\'')}`: {warning}");
                }
                try
                {
                    Dictionary<LineTrackedString, object> scriptSection = (Dictionary<LineTrackedString, object>)scriptPair.Value;
                    if (!scriptSection.TryGetValue(new LineTrackedString(0, "type"), out object typeValue) || !(typeValue is LineTrackedString typeString))
                    {
                        warnScript(Errors, scriptPair.Key.Line, "no_type_key", "Missing 'type' key!");
                        continue;
                    }
                    if (!KnownScriptTypes.TryGetValue(typeString.Text, out KnownScriptType scriptType))
                    {
                        warnScript(Errors, typeString.Line, "wrong_type", "Unknown script type (possibly a typo?)!");
                        continue;
                    }
                    foreach (string key in scriptType.RequiredKeys)
                    {
                        if (!scriptSection.ContainsKey(new LineTrackedString(0, key)))
                        {
                            warnScript(Warnings, typeString.Line, "missing_key_" + typeString.Text, $"Missing required key `{key}` (check `!lang {typeString.Text} script containers` for format rules)!");
                        }
                    }
                    foreach (string key in scriptType.LikelyBadKeys)
                    {
                        if (scriptSection.ContainsKey(new LineTrackedString(0, key)))
                        {
                            warnScript(Warnings, typeString.Line, "bad_key_" + typeString.Text, $"Unexpected key `{key.Replace('`', '\'')}` (probably doesn't belong in this script type - check `!lang {typeString.Text} script containers` for format rules)!");
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
                        void checkAsScript(List<object> list, HashSet<string> definitionsKnown)
                        {
                            if (scriptSection.TryGetValue(new LineTrackedString(0, "definitions"), out object defList) && defList is LineTrackedString defListVal)
                            {
                                definitionsKnown.UnionWith(defListVal.Text.ToLowerFast().Split('|').Select(s => s.Trim()));
                            }
                            if (typeString.Text == "task")
                            {
                                // Workaround the weird way shoot command does things
                                definitionsKnown.UnionWith(new[] { "shot_entities", "last_entity", "location", "hit_entities" });
                            }
                            // Default run command definitions get used sometimes
                            definitionsKnown.UnionWith(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" });
                            if (Injects.Contains(scriptPair.Key.Text) || Injects.Contains("*"))
                            {
                                definitionsKnown.Add("*");
                            }
                            foreach (object entry in list)
                            {
                                if (entry is LineTrackedString str)
                                {
                                    CheckSingleCommand(str.Line, str.Text, definitionsKnown);
                                }
                                else if (entry is Dictionary<LineTrackedString, object> subMap)
                                {
                                    KeyValuePair<LineTrackedString, object> onlyEntry = subMap.First();
                                    CheckSingleCommand(onlyEntry.Key.Line, onlyEntry.Key.Text, definitionsKnown);
                                    checkAsScript((List<object>)onlyEntry.Value, definitionsKnown);
                                }
                            }
                        }
                        void checkBasicList(List<object> list)
                        {
                            foreach (object entry in list)
                            {
                                if (entry is LineTrackedString str)
                                {
                                    CheckSingleArgument(str.Line, str.Text);
                                }
                                else
                                {
                                    warnScript(Warnings, keyPair.Key.Line, "script_should_be_list", $"Key `{keyName.Replace('`', '\'')}` appears to contain a script, when a data list was expected (check `!lang {typeString.Text} script containers` for format rules).");
                                }
                            }
                        }
                        if (keyPair.Value is List<object> keyPairList)
                        {
                            if (matchesSet(keyName, scriptType.ScriptKeys))
                            {
                                checkAsScript(keyPairList, new HashSet<string>());
                            }
                            else if (matchesSet(keyName, scriptType.ListKeys))
                            {
                                checkBasicList(keyPairList);
                            }
                            else if (matchesSet(keyName, scriptType.ValueKeys))
                            {
                                warnScript(Warnings, keyPair.Key.Line, "list_should_be_value", $"Bad key `{keyName.Replace('`', '\'')}` (was expected to be a direct Value, but was instead a list - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (scriptType.Strict)
                            {
                                warnScript(Warnings, keyPair.Key.Line, "unknown_key_" + typeString.Text, $"Unexpected list key `{keyName.Replace('`', '\'')}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (scriptType.CanHaveRandomScripts)
                            {
                                checkAsScript(keyPairList, new HashSet<string>());
                            }
                            else if (typeString.Text != "yaml data")
                            {
                                checkBasicList(keyPairList);
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
                                warnScript(Warnings, keyPair.Key.Line, "bad_key_" + typeString.Text, $"Bad key `{keyName.Replace('`', '\'')}` (was expected to be a list or script, but was instead a direct Value - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else if (scriptType.Strict)
                            {
                                warnScript(Warnings, keyPair.Key.Line, "unknown_key_" + typeString.Text, $"Unexpected value key `{keyName.Replace('`', '\'')}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else
                            {
                                CheckSingleArgument(keyPair.Key.Line, keyPairLine.Text);
                            }
                        }
                        else if (keyPair.Value is Dictionary<LineTrackedString, object> keyPairMap)
                        {
                            string keyText = keyName + ".*";
                            void checkSubMaps(Dictionary<LineTrackedString, object> subMap)
                            {
                                foreach (object subValue in subMap.Values)
                                {
                                    if (subValue is LineTrackedString textLine)
                                    {
                                        CheckSingleArgument(textLine.Line, textLine.Text);
                                    }
                                    else if (subValue is List<object> listKey)
                                    {
                                        if (scriptType.ScriptKeys.Contains(keyText) || (!scriptType.ListKeys.Contains(keyText) && scriptType.CanHaveRandomScripts))
                                        {
                                            checkAsScript(listKey, new HashSet<string>());
                                        }
                                        else
                                        {
                                            checkBasicList(listKey);
                                        }
                                    }
                                    else if (subValue is Dictionary<LineTrackedString, object> mapWithin)
                                    {
                                        checkSubMaps(mapWithin);
                                    }
                                }
                            }
                            if (scriptType.ValueKeys.Contains(keyText) || scriptType.ListKeys.Contains(keyText) || scriptType.ScriptKeys.Contains(keyText)
                                || scriptType.ValueKeys.Contains("*") || scriptType.ListKeys.Contains("*") || scriptType.ScriptKeys.Contains("*"))
                            {
                                if (typeString.Text != "yaml data")
                                {
                                    checkSubMaps(keyPairMap);
                                }
                            }
                            else if (scriptType.Strict)
                            {
                                warnScript(Warnings, keyPair.Key.Line, "unknown_key_" + typeString.Text, $"Unexpected submapping key `{keyName.Replace('`', '\'')}` (unrecognized - check `!lang {typeString.Text} script containers` for format rules)!");
                            }
                            else
                            {
                                if (typeString.Text != "yaml data")
                                {
                                    checkSubMaps(keyPairMap);
                                }
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
                                    warnScript(MinorWarnings, usageString.Line, "command_script_usage", "Command script usage key doesn't match the name key (the name has is the actual thing you need to type in-game, the usage is for '/help' - refer to `!lang command script containers`)!");
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
                                if (actionName.Contains("@"))
                                {
                                    Warn(Warnings, actionValue.Line, "action_object_notation", "This action line appears to contain raw object notation. Object notation is not allowed in action lines.");
                                }
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
                                        warnScript(Warnings, actionValue.Line, "action_missing", $"Assignment script action listed doesn't exist. (Check `!act ...` to find proper action names)!");
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
                                if (eventName.Contains("@"))
                                {
                                    Warn(Warnings, eventValue.Line, "event_object_notation", "This event line appears to contain raw object notation. Object notation is not allowed in event lines.");
                                }
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
                                        warnScript(Warnings, eventValue.Line, "event_missing", $"Script Event listed doesn't exist. (Check `!event ...` to find proper event lines)!");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    warnScript(Warnings, scriptPair.Key.Line, "exception_internal", $"Internal exception (check bot debug console)!");
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
            Dictionary<int, List<object>> spacedlists = new Dictionary<int, List<object>>();
            Dictionary<LineTrackedString, object> currentSection = rootScriptSection;
            int pspaces = 0;
            LineTrackedString secwaiting = null;
            List<object> clist = null;
            bool buildingSubList = false;
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
                    if (spacedlists.TryGetValue(spaces, out List<object> tempList))
                    {
                        clist = tempList;
                    }
                    else if (spacedsections.TryGetValue(spaces, out Dictionary<LineTrackedString, object> temp))
                    {
                        currentSection = temp;
                    }
                    else
                    {
                        Warn(Warnings, i, "shrunk_spacing", $"Simple spacing error - shrunk unexpectedly to new space count, from {pspaces} down to {spaces}, while expecting any of: {string.Join(", ", spacedsections.Keys)}.");
                        pspaces = spaces;
                        continue;
                    }
                    foreach (int test in new List<int>(spacedsections.Keys))
                    {
                        if (test > spaces)
                        {
                            spacedsections.Remove(test);
                        }
                    }
                    foreach (int test in new List<int>(spacedlists.Keys))
                    {
                        if (test > spaces)
                        {
                            spacedlists.Remove(test);
                        }
                    }
                }
                if (cleaned.StartsWith("- "))
                {
                    if (spaces > pspaces && clist != null && !buildingSubList)
                    {
                        Warn(Warnings, i, "growing_spaces_in_script", "Spacing grew for no reason (missing a ':' on a command, or accidental over-spacing?).");
                        Console.WriteLine($"Line {i + 1}, Spacing is {spaces}, but was {pspaces}... unexplained growth");
                    }
                    if (secwaiting != null)
                    {
                        if (clist == null)
                        {
                            clist = new List<object>();
                            spacedlists[spaces] = clist;
                            currentSection[secwaiting] = clist;
                            secwaiting = null;
                        }
                        else if (buildingSubList)
                        {
                            List<object> newclist = new List<object>();
                            clist.Add(new Dictionary<LineTrackedString, object>() { { secwaiting, newclist } });
                            secwaiting = null;
                            buildingSubList = false;
                            clist = newclist;
                            spacedlists[spaces] = newclist;
                        }
                        else
                        {
                            Warn(Warnings, i, "growing_spacing_impossible", "Line grew when that isn't possible (spacing error?).");
                            pspaces = spaces;
                            continue;
                        }
                    }
                    else if (clist == null)
                    {
                        Warn(Warnings, i, "weird_line_growth", "Line purpose unknown, attempted list entry when not building a list (likely line format error, perhaps missing or misplaced a `:` on lines above, or incorrect tabulation?).");
                        pspaces = spaces;
                        continue;
                    }
                    if (cleaned.EndsWith(":"))
                    {
                        secwaiting = new LineTrackedString(i, cleaned.Substring("- ".Length, cleaned.Length - "- :".Length));
                        buildingSubList = true;
                    }
                    else
                    {
                        clist.Add(new LineTrackedString(i, cleaned.Substring("- ".Length)));
                    }
                    pspaces = spaces;
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
                    Warn(Warnings, i, "identifier_missing_line", "Line purpose unknown, no identifier (missing a `:` or a `-`?).");
                    continue;
                }
                if (startofline.Length == 0)
                {
                    Warn(Warnings, i, "key_line_no_content", "key line missing contents (misplaced a `:`)?");
                    continue;
                }
                if (spaces > pspaces)
                {
                    if (secwaiting == null)
                    {
                        Warn(Warnings, i, "spacing_grew_weird", "Spacing grew for no reason (missing a ':', or accidental over-spacing?).");
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
            Warn(Infos, -1, "stat_structural", $"(Statistics) Total structural lines: {StructureLines}");
            Warn(Infos, -1, "stat_livecode", $"(Statistics) Total live code lines: {CodeLines}");
            Warn(Infos, -1, "stat_comment", $"(Statistics) Total comment lines: {CommentLines}");
            Warn(Infos, -1, "stat_blank", $"(Statistics) Total blank lines: {BlankLines}");
        }

        /// <summary>
        /// Runs the full script check.
        /// </summary>
        public void Run()
        {
            ClearCommentsFromLines();
            CheckYAML();
            LoadInjects();
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
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Script Check Results").WithThumbnailUrl((Errors.Count + Warnings.Count + MinorWarnings.Count > 0) ? Constants.WARNING_ICON : Constants.INFO_ICON);
            int linesMissing = 0;
            int shortened = 0;
            void embedList(List<ScriptWarning> list, string title)
            {
                if (list.Count > 0)
                {
                    HashSet<string> usedKeys = new HashSet<string>();
                    StringBuilder thisListResult = new StringBuilder(list.Count * 200);
                    foreach (ScriptWarning entry in list)
                    {
                        if (usedKeys.Contains(entry.WarningUniqueKey))
                        {
                            continue;
                        }
                        usedKeys.Add(entry.WarningUniqueKey);
                        StringBuilder lines = new StringBuilder(50);
                        if (entry.Line != -1)
                        {
                            lines.Append(entry.Line + 1);
                        }
                        foreach (ScriptWarning subEntry in list.SkipWhile(s => s != entry).Skip(1).Where(s => s.WarningUniqueKey == entry.WarningUniqueKey))
                        {
                            shortened++;
                            if (lines.Length < 40)
                            {
                                lines.Append(", ").Append(subEntry.Line + 1);
                                if (lines.Length >= 40)
                                {
                                    lines.Append(", ...");
                                }
                            }
                        }
                        string message = $"On line {lines}: {entry.CustomMessageForm}";
                        if (thisListResult.Length + message.Length < 1000 && embed.Length + thisListResult.Length + message.Length < 1800)
                        {
                            thisListResult.Append($"{message}\n");
                        }
                        else
                        {
                            linesMissing++;
                        }
                    }
                    if (thisListResult.Length > 0)
                    {
                        embed.AddField(title, thisListResult.ToString());
                    }
                    Console.WriteLine($"Script Checker {title}: {string.Join('\n', list.Select(s => $"{s.Line + 1}: {s.CustomMessageForm}"))}");
                }
            }
            embedList(Errors, "Encountered Critical Errors");
            embedList(Warnings, "Script Warnings");
            embedList(MinorWarnings, "Minor Warnings");
            embedList(Infos, "Other Script Information");
            if (linesMissing > 0)
            {
                embed.AddField("Missing Lines", $"There are {linesMissing} lines not able to fit in this result. Fix the listed errors to see the rest.");
            }
            if (shortened > 0)
            {
                embed.AddField("Shortened Lines", $"There are {shortened} lines that were merged into other lines.");
            }
            foreach (string debug in Debugs)
            {
                Console.WriteLine($"Script checker debug: {debug}");
            }
            return embed.Build();
        }
    }
}
