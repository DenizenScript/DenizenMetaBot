using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using Discord;
using YamlDotNet.RepresentationModel;
using FreneticUtilities.FreneticExtensions;

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
            // TODO: Check each argument's tags
            // TODO: check for object notation usage
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
            /// Constructs the <see cref="KnownScriptType"/> instance.
            /// </summary>
            public KnownScriptType(string[] required = null, string[] bad = null, string[] valueKeys = null, string[] listKeys = null, string[] scriptKeys = null, bool strict = false)
            {
                RequiredKeys = required ?? RequiredKeys;
                LikelyBadKeys = bad ?? LikelyBadKeys;
                ValueKeys = valueKeys ?? ValueKeys;
                ListKeys = listKeys ?? ListKeys;
                ScriptKeys = scriptKeys ?? ScriptKeys;
                Strict = strict;
            }
        }

        /// <summary>
        /// A set of all known script type names.
        /// </summary>
        public static readonly Dictionary<string, KnownScriptType> KnownScriptTypes = new Dictionary<string, KnownScriptType>()
        {
            // Denizen Core
            { "custom", new KnownScriptType(bad: new[] { "script", "actions", "events", "steps" }, valueKeys: new[] { "inherit", "*" }, scriptKeys: new[] { "tags.*", "mechanisms.*" }, strict: false) },
            { "procedure", new KnownScriptType(required: new[] { "script" }, bad: new[] { "events", "actions", "steps" }, valueKeys: new[] { "definitions" }, scriptKeys: new[] { "script" }, strict: true) },
            { "task", new KnownScriptType(required: new[] { "script" }, bad: new[] { "events", "actions", "steps" }, valueKeys: new[] { "definitions" }, scriptKeys: new[] { "script" }, strict: false) },
            { "world", new KnownScriptType(required: new[] { "events" }, bad: new[] { "script", "actions", "steps" }, scriptKeys: new[] { "events.*" }, strict: false) },
            { "yaml data", new KnownScriptType(bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "*" }, listKeys: new[] { "*" }, strict: false) },
            // Denizen-Bukkit
            { "assignment", new KnownScriptType(required: new[] { "actions", "interact scripts" }, bad: new[] { "script", "steps", "events" }, valueKeys: new[] { "default constants.*", "constants.*" }, listKeys: new[] { "interact scripts" }, scriptKeys: new[] { "actions.*" }, strict: true) },
            { "book", new KnownScriptType(required: new[] { "title", "author", "text" }, bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "title", "author", "signed" }, listKeys: new[] { "text" }, strict: true) },
            { "command", new KnownScriptType(required: new[] { "name", "description", "usage", "script" }, bad: new[] { "steps", "actions", "events" }, valueKeys: new[] { "name", "description", "usage", "permission", "permission message" }, listKeys: new[] { "aliases" }, scriptKeys: new[] { "allowed help", "tab complete", "script" }, strict: false) },
            { "economy", new KnownScriptType(required: new[] { "priority", "name single", "name plural", "digits", "format", "balance", "has", "withdraw", "deposit" }, bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "priority", "name single", "name plural", "digits", "format", "balance", "has" }, scriptKeys: new[] { "withdraw", "deposit" }, strict: true) },
            { "entity", new KnownScriptType(required: new[] { "entity_type" }, bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "*" }, strict: false) },
            { "format", new KnownScriptType(required: new[] { "format" }, bad: new[] { "script", "actions", "steps", "events" }, valueKeys: new[] { "format" }, strict: true) },
            { "interact", new KnownScriptType(required: new[] { "steps" }, bad: new[] { "script", "actions", "events" }, scriptKeys: new[] { "steps.*" }, strict: true) },
            { "inventory", new KnownScriptType(required: new[] { "inventory" }, bad: new[] { "script", "steps", "actions", "events" }, valueKeys: new[] { "inventory", "title", "size", "definitions.*" }, scriptKeys: new[] { "procedural items" }, listKeys: new[] { "slots" }, strict: true) },
            { "item", new KnownScriptType(required: new[] { "material" }, bad: new[] { "script", "steps", "actions", "events" }, valueKeys: new[] { "material", "mechanisms.*", "display name", "durability", "recipes.*", "no_id", "color", "book" }, listKeys: new[] { "mechanisms.*", "lore", "enchantments", "recipes.*" }, strict: false) },
            { "map", new KnownScriptType(bad: new[] { "script", "steps", "actions", "events" }, valueKeys: new[] { "original", "display name", "auto update", "objects.*" }, strict: true) }
        };

        /// <summary>
        /// Checks all findable script containers, their keys, and the keys within.
        /// </summary>
        public void CheckContainerTypes()
        {
            int scriptStartLine = -1;
            string type = null;
            KnownScriptType actualType = null;
            for (int i = 0; i < Lines.Length; i++)
            {
                string line = Lines[i];
                string cleaned = CleanedLines[i];
                if (line.Length > 0 && cleaned.EndsWith(":") && char.ToLowerInvariant(line[0]) == cleaned[0])
                {
                    scriptStartLine = i;
                    type = null;
                    actualType = null;
                }
                else if (scriptStartLine == -1 && line.Length > 0)
                {
                    Warn(Errors, i, "Script didn't start with a container name (Possible spacing mishap?).");
                }
                else if (cleaned.StartsWith("type:"))
                {
                    type = cleaned.Substring("type:".Length).Trim();
                    if (!KnownScriptTypes.TryGetValue(type, out actualType))
                    {
                        Warn(Errors, i, "Unknown script type (possible typo?).");
                    }
                }
                else if (cleaned.StartsWith("debug:"))
                {
                    string debugMode = cleaned.Substring("debug:".Length).Trim();
                    if (debugMode != "false")
                    {
                        if (debugMode == "true")
                        {
                            Warn(MinorWarnings, i, "Debug mode 'true' is default, and thus the line should simply be removed.");
                        }
                        else
                        {
                            Warn(MinorWarnings, i, "Debug mode specified is unrecognized. The only valid debug mode setting is 'false' (or leave the line entirely off).");
                        }
                    }
                }
                else if (cleaned.StartsWith("speed:"))
                {
                    // Ignore this line, anything's fine really.
                }
            }
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
            CheckContainerTypes();
            // TODO: Check events for existence
            // TODO: line growth oddities check
            // TODO: line type statistic gathering
            // TODO: Tag existent-name per-piece check
            // TODO: script type validity check
            // TODO: script required keys check
            // Check that command script name == alias
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
