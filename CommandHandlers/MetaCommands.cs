using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using DenizenBot.UtilityProcessors;
using DiscordBotBase;
using DiscordBotBase.CommandHandlers;
using SharpDenizenTools.MetaHandlers;
using SharpDenizenTools.MetaObjects;

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Commands to look up meta documentation.
    /// </summary>
    public class MetaCommands : UserCommands
    {
        /// <summary>
        /// Checks whether meta commands are denied in the relevant channel. If denied, will return 'true' and show a rejection message.
        /// </summary>
        /// <param name="message">The message being replied to.</param>
        /// <returns>True if they are denied.</returns>
        public static bool CheckMetaDenied(IUserMessage message)
        {
            if (!DenizenMetaBot.MetaCommandsAllowed(message.Channel))
            {
                SendErrorMessageReply(message, "Command Not Allowed Here",
                    "Meta documentation commands are not allowed in this channel. Please switch to a bot spam channel, or a Denizen channel.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Matcher for A-Z only.
        /// </summary>
        public static readonly AsciiMatcher AlphabetMatcher = new AsciiMatcher(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

        /// <summary>
        /// Automatically processes a meta search command.
        /// </summary>
        /// <typeparam name="T">The meta object type.</typeparam>
        /// <param name="docs">The docs mapping.</param>
        /// <param name="type">The meta type.</param>
        /// <param name="cmds">The command args.</param>
        /// <param name="message">The Discord message object.</param>
        /// <param name="secondarySearches">A list of secondary search strings if the first fails.</param>
        /// <param name="secondaryMatcher">A secondary matching function if needed.</param>
        /// <param name="altSingleOutput">An alternate method of processing the single-item-result.</param>
        /// <param name="altFindClosest">Alternate method to find the closest result.</param>
        /// <returns>How close of an answer was gotten (0 = perfect, -1 = no match needed, 1000 = none).</returns>
        public int AutoMetaCommand<T>(Dictionary<string, T> docs, MetaType type, string[] cmds, IUserMessage message,
            List<string> secondarySearches = null, Func<T, bool> secondaryMatcher = null, Action<T> altSingleOutput = null,
            Func<string> altFindClosest = null, Func<List<T>, List<T>> altMatchOrderer = null) where T: MetaObject
        {
            if (CheckMetaDenied(message))
            {
                return -1;
            }
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, $"Need input for '{type.Name}' command",
                    $"Please specify a {type.Name} to search, like `!{type.Name} Some{type.Name}Here`. Or, use `!{type.Name} all` to view all documented {type.Name.ToLowerFast()}s.");
                return -1;
            }
            string search = cmds[0].ToLowerFast();
            if (search == "all")
            {
                SendGenericPositiveMessageReply(message, $"All {type.Name}s", $"Find all {type.Name}s at {DenizenMetaBotConstants.DOCS_URL_BASE}{type.WebPath}/");
                return -1;
            }
            if (altSingleOutput == null)
            {
                altSingleOutput = (singleObj) => SendReply(message, singleObj.GetEmbed().Build());
            }
            if (altFindClosest == null)
            {
                altFindClosest = () =>
                {
                    string initialPossibleResult = StringConversionHelper.FindClosestString(docs.Keys, search, out int lowestDistance, 20);
                    Console.WriteLine($"Initial closest match to '{search}' is '{initialPossibleResult}' at distance {lowestDistance}.");
                    string lowestStr = initialPossibleResult;
                    foreach (string possibleName in docs.Values.Where(o => o.HasMultipleNames || o.Synonyms.Any()).SelectMany(o => o.MultiNames.Concat(o.Synonyms)))
                    {
                        int currentDistance = StringConversionHelper.GetLevenshteinDistance(search, possibleName);
                        if (currentDistance < lowestDistance)
                        {
                            lowestDistance = currentDistance;
                            lowestStr = possibleName;
                        }
                    }
                    string[] words = search.Split(' ');
                    if (words.Length > 1)
                    {
                        Console.WriteLine($"Pre-multi-word closest match is '{lowestStr}' at distance {lowestDistance}.");
                        foreach (string possibleName in docs.Values.SelectMany(o => o.MultiNames.Concat(o.Synonyms)))
                        {
                            int currentDistance = 0;
                            string[] nameWords = possibleName.Split(' ');
                            foreach (string searchWord in words)
                            {
                                int lowestWordDistance = 9999;
                                foreach (string nameWord in nameWords)
                                {
                                    int currentWordDistance = StringConversionHelper.GetLevenshteinDistance(searchWord, nameWord);
                                    if (currentWordDistance < lowestWordDistance)
                                    {
                                        lowestWordDistance = currentWordDistance;
                                    }
                                }
                                currentDistance += lowestWordDistance;
                            }
                            if (currentDistance < lowestDistance)
                            {
                                lowestDistance = currentDistance;
                                lowestStr = possibleName;
                            }
                        }
                    }
                    Console.WriteLine($"Final closest match is '{lowestStr}' at distance {lowestDistance}.");
                    return lowestStr;
                };
            }
            if (altMatchOrderer == null)
            {
                altMatchOrderer = (list) => list.OrderBy((mat) => StringConversionHelper.GetLevenshteinDistance(search, mat.CleanName)).ToList();
            }
            if (docs.TryGetValue(search, out T obj))
            {
                string multiNameData = string.Join("', '", obj.MultiNames);
                Console.WriteLine($"Meta-Command for '{type.Name}' found perfect match for search '{search}': '{obj.CleanName}', multi={obj.HasMultipleNames}='{multiNameData}'");
                altSingleOutput(obj);
                return 0;
            }
            if (secondarySearches != null)
            {
                secondarySearches = secondarySearches.Select(s => s.ToLowerFast()).ToList();
                foreach (string secondSearch in secondarySearches)
                {
                    if (docs.TryGetValue(secondSearch, out obj))
                    {
                        Console.WriteLine($"Meta-Command for '{type.Name}' found perfect match for secondary search '{secondSearch}': '{obj.CleanName}', multi={obj.HasMultipleNames}");
                        altSingleOutput(obj);
                        return 0;
                    }
                }
            }
            List<T> matched = new List<T>();
            List<T> strongMatched = new List<T>();
            string searchAZTrim = AlphabetMatcher.TrimToMatches(search);
            int tryProcesSingleMatch(T objVal, string objName, int min)
            {
                if (objName.Contains(search))
                {
                    Console.WriteLine($"Meta-Command for '{type.Name}' found a strong match (main contains) for search '{search}': '{objName}'");
                    strongMatched.Add(objVal);
                    return 2;
                }
                if (secondarySearches != null)
                {
                    foreach (string secondSearch in secondarySearches)
                    {
                        if (objName.Contains(secondSearch))
                        {
                            Console.WriteLine($"Meta-Command for '{type.Name}' found a strong match (secondary contains) for search '{secondSearch}': '{objName}'");
                            strongMatched.Add(objVal);
                            return 2;
                        }
                    }
                }
                if (min < 1 && secondaryMatcher != null && secondaryMatcher(objVal))
                {
                    Console.WriteLine($"Meta-Command for '{type.Name}' found a weak match (secondaryMatcher) for search '{search}': '{objName}'");
                    if (AlphabetMatcher.TrimToMatches(objName).Contains(searchAZTrim))
                    {
                        Console.WriteLine($"Escalated last match to strong.");
                        strongMatched.Add(objVal);
                        return 2;
                    }
                    matched.Add(objVal);
                    return 1;
                }
                return min;
            }
            foreach (KeyValuePair<string, T> objPair in docs)
            {
                int matchQuality = 0;
                foreach (string name in objPair.Value.Synonyms)
                {
                    matchQuality = tryProcesSingleMatch(objPair.Value, name, matchQuality);
                    if (matchQuality == 2)
                    {
                        break;
                    }
                }
                if (objPair.Value.HasMultipleNames)
                {
                    foreach (string name in objPair.Value.MultiNames)
                    {
                        matchQuality = tryProcesSingleMatch(objPair.Value, name, matchQuality);
                        if (matchQuality == 2)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    tryProcesSingleMatch(objPair.Value, objPair.Key, 0);
                }
            }
            if (strongMatched.Count > 0)
            {
                matched = strongMatched;
            }
            if (matched.Count == 0)
            {
                SendErrorMessageReply(message, $"Cannot Find Searched {type.Name}", $"Unknown {type.Name.ToLowerFast()}.");
                string closeName = altFindClosest();
                if (closeName != null)
                {
                    SendDidYouMeanReply(message, "Possible Confusion", $"Did you mean to search for `{closeName}`?", $"{DenizenMetaBotConstants.COMMAND_PREFIX}{type.Name} {closeName}");
                }
                return closeName == null ? 1000 : StringConversionHelper.GetLevenshteinDistance(search, closeName);
            }
            if (matched.Count > 1)
            {
                matched = altMatchOrderer(matched);
            }
            if (matched.Count > 1)
            {
                string suffix = ".";
                if (matched.Count > 20)
                {
                    matched = matched.GetRange(0, 20);
                    suffix = ", ...";
                }
                string listText = string.Join("`, `", matched.Select((m) => m.Name));
                SendErrorMessageReply(message, $"Cannot Specify Searched {type.Name}", $"Multiple possible {type.Name.ToLowerFast()}s: `{listText}`{suffix}");
                return StringConversionHelper.GetLevenshteinDistance(search, matched[0].CleanName);
            }
            else // Count == 1
            {
                obj = matched[0];
                Console.WriteLine($"Meta-Command for '{type.Name}' found imperfect single match for search '{search}': '{obj.CleanName}', multi={obj.HasMultipleNames}");
                altSingleOutput(obj);
                return 0;
            }
        }

        /// <summary>
        /// Command meta docs user command.
        /// </summary>
        public void CMD_Command(CommandData command)
        {
            void singleReply(MetaCommand cmd)
            {
                if (command.CleanedArguments.Length >= 2)
                {
                    string outputType = command.CleanedArguments[1].ToLowerFast();
                    if (outputType.StartsWith("u"))
                    {
                        SendReply(command.Message, cmd.GetCommandUsagesEmbed().Build());
                    }
                    else if (outputType.StartsWith("t"))
                    {
                        SendReply(command.Message, cmd.GetCommandTagsEmbed().Build());
                    }
                    else
                    {
                        SendErrorMessageReply(command.Message, "Bad Command Syntax", "Second argument is unknown.\n\nUsage: `command [name] [usage/tags]`.");
                    }
                }
                else
                {
                    SendReply(command.Message, cmd.GetEmbed().Build());
                }
            }
            int closeness = AutoMetaCommand(MetaDocs.CurrentMeta.Commands, MetaDocs.META_TYPE_COMMAND, command.CleanedArguments, command.Message, altSingleOutput: singleReply);
            if (closeness > 0)
            {
                string closeMech = StringConversionHelper.FindClosestString(MetaDocs.CurrentMeta.Mechanisms.Keys.Select(s => s.After('.')), command.CleanedArguments[0].ToLowerFast(), 10);
                if (closeMech != null)
                {
                    SendDidYouMeanReply(command.Message, "Possible Confusion", $"Did you mean to search for `mechanism {closeMech}`?", $"{DenizenMetaBotConstants.COMMAND_PREFIX}mechanism {closeMech}");
                }
            }
        }

        /// <summary>
        /// Mechanism meta docs user command.
        /// </summary>
        public void CMD_Mechanism(CommandData command)
        {
            List<string> secondarySearches = new List<string>();
            if (command.CleanedArguments.Length > 0)
            {
                int dotIndex = command.CleanedArguments[0].IndexOf('.');
                if (dotIndex > 0)
                {
                    secondarySearches.Add(command.CleanedArguments[0].Substring(0, dotIndex) + "tag" + command.CleanedArguments[0][dotIndex..]);
                }
            }
            int closeness = AutoMetaCommand(MetaDocs.CurrentMeta.Mechanisms, MetaDocs.META_TYPE_MECHANISM, command.CleanedArguments, command.Message, secondarySearches);
            if (closeness > 0)
            {
                string closeCmd = StringConversionHelper.FindClosestString(MetaDocs.CurrentMeta.Commands.Keys, command.CleanedArguments[0].ToLowerFast(), 7);
                if (closeCmd != null)
                {
                    SendDidYouMeanReply(command.Message, "Possible Confusion", $"Did you mean to search for `command {closeCmd}`?", $"{DenizenMetaBotConstants.COMMAND_PREFIX}command {closeCmd}");
                }
            }
        }

        /// <summary>
        /// Tag meta docs user command.
        /// </summary>
        public void CMD_Tag(CommandData command)
        {
            List<string> secondarySearches = new List<string>();
            string[] cmds = command.CleanedArguments;
            if (cmds.Length > 0)
            {
                cmds[0] = MetaTag.CleanTag(cmds[0]);
                int dotIndex = cmds[0].IndexOf('.');
                if (dotIndex > 0)
                {
                    string tagBase = cmds[0].Substring(0, dotIndex);
                    string tagSuffix = cmds[0][dotIndex..];
                    MetaObjectType type = MetaDocs.CurrentMeta.ObjectTypes.GetValueOrDefault(tagBase.ToLowerFast());
                    if (!tagBase.EndsWith("tag"))
                    {
                        secondarySearches.Add(tagBase + "tag" + tagSuffix);
                        if (type == null)
                        {
                            type = MetaDocs.CurrentMeta.ObjectTypes.GetValueOrDefault(tagBase.ToLowerFast() + "tag");
                        }
                    }
                    if (type != null && type.BaseTypeName != null)
                    {
                        foreach (MetaObjectType subType in type.Implements)
                        {
                            secondarySearches.Add(subType.CleanName + tagSuffix);
                        }
                        secondarySearches.Add(type.BaseTypeName.ToLowerFast() + tagSuffix);
                    }
                }
            }
            int getDistanceTo(MetaTag tag)
            {
                int dist1 = StringConversionHelper.GetLevenshteinDistance(cmds[0], tag.CleanedName);
                int dist2 = StringConversionHelper.GetLevenshteinDistance(cmds[0], tag.AfterDotCleaned);
                int dist = Math.Min(dist1, dist2);
                foreach (string secondSearch in secondarySearches)
                {
                    int dist3 = StringConversionHelper.GetLevenshteinDistance(secondSearch, tag.CleanedName);
                    dist = Math.Min(dist, dist3);
                }
                return dist;
            }
            string findClosestTag()
            {
                int lowestDistance = 20;
                string lowestStr = null;
                foreach (MetaTag tag in MetaDocs.CurrentMeta.Tags.Values)
                {
                    int currentDistance = getDistanceTo(tag);
                    if (currentDistance < lowestDistance)
                    {
                        lowestDistance = currentDistance;
                        lowestStr = tag.CleanedName;
                    }
                }
                return lowestStr;
            }
            AutoMetaCommand(MetaDocs.CurrentMeta.Tags, MetaDocs.META_TYPE_TAG, cmds, command.Message, secondarySearches, altFindClosest: findClosestTag,
                altMatchOrderer: (list) => list.OrderBy(getDistanceTo).ToList());
        }

        /// <summary>
        /// ObjectTypes meta docs user command.
        /// </summary>
        public void CMD_ObjectTypes(CommandData command)
        {
            int closeness = AutoMetaCommand(MetaDocs.CurrentMeta.ObjectTypes, MetaDocs.META_TYPE_OBJECT, command.CleanedArguments, command.Message);
            if (closeness > 0)
            {
                string closeCmd = StringConversionHelper.FindClosestString(MetaDocs.CurrentMeta.ObjectTypes.Keys, command.CleanedArguments[0].ToLowerFast(), 7);
                if (closeCmd != null)
                {
                    SendDidYouMeanReply(command.Message, "Possible Confusion", $"Did you mean to search for `objecttype {closeCmd}`?", $"{DenizenMetaBotConstants.COMMAND_PREFIX}objecttype {closeCmd}");
                }
            }
        }

        /// <summary>
        /// Event meta docs user command.
        /// </summary>
        public void CMD_Event(CommandData command)
        {
            string[] cmds = command.CleanedArguments;
            string onSearch = string.Join(" ", cmds).ToLowerFast();
            string secondarySearch = onSearch.StartsWith("on ") ? onSearch["on ".Length..] : onSearch;
            onSearch = "on " + secondarySearch;
            if (cmds.Length > 0)
            {
                cmds[0] = secondarySearch;
            }
            string[] parts = secondarySearch.Split(' ');
            AutoMetaCommand(MetaDocs.CurrentMeta.Events, MetaDocs.META_TYPE_EVENT, cmds, command.Message, secondaryMatcher: (e) => e.CouldMatchers.Any(c => c.DoesMatch(parts)));
        }

        /// <summary>
        /// Action meta docs user command.
        /// </summary>
        public void CMD_Action(CommandData command)
        {
            string[] cmds = command.CleanedArguments;
            string secondarySearch = string.Join(" ", cmds).ToLowerFast();
            secondarySearch = secondarySearch.StartsWith("on ") ? secondarySearch["on ".Length..] : secondarySearch;
            if (cmds.Length > 0)
            {
                cmds[0] = secondarySearch;
            }
            AutoMetaCommand(MetaDocs.CurrentMeta.Actions, MetaDocs.META_TYPE_ACTION, cmds, command.Message);
        }

        /// <summary>
        /// Language meta docs user command.
        /// </summary>
        public void CMD_Language(CommandData command)
        {
            string[] cmds = command.CleanedArguments;
            string secondarySearch = string.Join(" ", cmds).ToLowerFast();
            if (cmds.Length > 0)
            {
                cmds[0] = secondarySearch;
            }
            AutoMetaCommand(MetaDocs.CurrentMeta.Languages, MetaDocs.META_TYPE_LANGUAGE, cmds, command.Message);
        }

        /// <summary>
        /// Guide page search user command.
        /// </summary>
        public void CMD_Guide(CommandData command)
        {
            string[] cmds = command.CleanedArguments;
            if (cmds.Length == 0 || cmds[0].ToLowerFast() == "all")
            {
                SendGenericPositiveMessageReply(command.Message, "Guides", $"View the Denizen Beginner's Guide at {MetaDocsLoader.DENIZEN_GUIDE_SOURCE}");
                return;
            }
            string secondarySearch = string.Join(" ", cmds).ToLowerFast();
            if (cmds.Length > 0)
            {
                cmds[0] = secondarySearch;
            }
            string search = cmds[0].ToLowerFast();
            List<MetaGuidePage> altMatchOrderer(List<MetaGuidePage> list)
            {
                IEnumerable<MetaGuidePage> betterMatches = list.Where(page => !page.IsSubPage);
                if (!betterMatches.IsEmpty())
                {
                    list = betterMatches.ToList();
                }
                return list.OrderBy((mat) => StringConversionHelper.GetLevenshteinDistance(search, mat.CleanName)).ToList();
            }
            AutoMetaCommand(MetaDocs.CurrentMeta.GuidePages, MetaDocs.META_TYPE_GUIDEPAGE, cmds, command.Message, altMatchOrderer: altMatchOrderer);
        }

        /// <summary>
        /// Meta docs total search command.
        /// </summary>
        public void CMD_Search(CommandData command)
        {
            if (CheckMetaDenied(command.Message))
            {
                return;
            }
            string[] cmds = command.CleanedArguments;
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(command.Message, "Need input for Search command", "Please specify some text to search, like `!search someobjecthere`.");
                return;
            }
            for (int i = 0; i < cmds.Length; i++)
            {
                cmds[i] = cmds[i].ToLowerFast();
            }
            string fullSearch = string.Join(' ', cmds);
            List<(int, MetaObject)> results = new List<(int, MetaObject)>();
            foreach (MetaObject obj in MetaDocs.CurrentMeta.AllMetaObjects())
            {
                int quality = obj.SearchHelper.GetMatchQuality(fullSearch);
                if (quality > 0)
                {
                    results.Add((quality, obj));
                }
            }
            void backupMatchCheck()
            {
                string possible = StringConversionHelper.FindClosestString(MetaDocs.CurrentMeta.AllMetaObjects().SelectMany(obj => obj.MultiNames), fullSearch, 10);
                if (!string.IsNullOrWhiteSpace(possible))
                {
                    SendDidYouMeanReply(command.Message, "Possible Confusion", $"Did you mean to search for `{possible}`?", $"{DenizenMetaBotConstants.COMMAND_PREFIX}search {possible}");
                }
            }
            if (results.IsEmpty())
            {
                SendErrorMessageReply(command.Message, "Search Command Has No Results", "Input search text could not be found.");
                backupMatchCheck();
                return;
            }
            string suffix = ".";
            results = results.OrderBy(pair => -pair.Item1).ThenBy(pair => StringConversionHelper.GetLevenshteinDistance(fullSearch, pair.Item2.CleanName)).ToList();
            suffix = ".";
            if (results.Count > 50)
            {
                results = results.GetRange(0, 50);
                suffix = ", ...";
            }
            StringBuilder combined = new StringBuilder();
            int bestQuality = results.First().Item1;
            int lastQuality = 0;
            foreach ((int quality, MetaObject obj) in results)
            {
                if (quality != lastQuality)
                {
                    combined.Append("\n[").Append(MetaObject.SearchableHelpers.SearchQualityName[quality]).Append("] ");
                    lastQuality = quality;
                }
                combined.Append('`').Append(DenizenMetaBotConstants.COMMAND_PREFIX).Append(obj.Type.Name).Append(' ').Append(obj.CleanName).Append("`, ");
                if (combined.Length > 1800)
                {
                    break;
                }
            }
            string listText = combined.ToString()[..^2] + suffix;
            SendGenericPositiveMessageReply(command.Message, "Search Results", listText);
            if (bestQuality < 5)
            {
                backupMatchCheck();
            }
        }
    }
}
