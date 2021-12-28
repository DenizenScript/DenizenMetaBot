using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Discord;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using DenizenBot.UtilityProcessors;
using DiscordBotBase.CommandHandlers;
using DiscordBotBase;
using SharpDenizenTools.ScriptAnalysis;
using System.Web;
using System.Net.Http.Headers;

namespace DenizenBot.CommandHandlers
{
    /// <summary>Commands to perform utility functions.</summary>
    public class UtilityCommands : UserCommands
    {
        /// <summary>Base URL for paste sites.</summary>
        public const string PASTEBIN_URL_BASE = "https://pastebin.com/",
            DENIZEN_PASTE_URL_BASE = "https://paste.denizenscript.com/view/";

        /// <summary>ASCII validator for a pastebin ID.</summary>
        public static AsciiMatcher PASTEBIN_CODE_VALIDATOR = new((c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

        /// <summary>ASCII validator for a Denizen haste code.</summary>
        public static AsciiMatcher HASTE_CODE_VALIDATOR = new((c) => c >= '0' && c <= '9');

        /// <summary>The max wait time for a web-link download in a command.</summary>
        public static TimeSpan WebLinkDownloadTimeout = new(hours: 0, minutes: 0, seconds: 15);

        /// <summary>File extensions allowed in command attachment links.</summary>
        public static HashSet<string> AllowedLinkFileExtensions = new() { "log", "txt", "dsc", "yml" };

        /// <summary>For a web-link command like '!logcheck', gets the data from the paste link.</summary>
        public string GetWebLinkDataForCommand(string cmdName, string type, CommandData command, out string url)
        {
            string inputUrl = null;
            try
            {
                if (command.CleanedArguments.Length > 0)
                {
                    inputUrl = command.RawArguments[0];
                }
                else if (command.Message.Reference is not null && command.Message.Reference.MessageId.IsSpecified)
                {
                    IMessage referenced = command.Message.Channel.GetMessageAsync(command.Message.Reference.MessageId.Value).Result;
                    if (referenced != null && referenced.Attachments.Any())
                    {
                        IAttachment attachment = referenced.Attachments.First();
                        if (attachment.Size < 100 || attachment.Size > 1024 * 1024 * 5)
                        {
                            SendErrorMessageReply(command.Message, "Cannot Scan Attached File", $"Attached file has size {attachment.Size} - file must be non-empty, and less than 5 MiB.");
                            url = null;
                            return null;
                        }
                        if (!AllowedLinkFileExtensions.Contains(attachment.Filename.AfterLast('.')))
                        {
                            SendErrorMessageReply(command.Message, "Cannot Scan Attached File", $"Attached file has unrecognized or unsupported file extension. Use `.log` for log files, and `.dsc` for Denizen scripts.");
                            url = null;
                            return null;
                        }
                        string data = Program.ReusableWebClient.GetStringAsync(attachment.Url).Result;
                        if (data != null && data.Length > 100 && data.Length < 1024 * 1024 * 5)
                        {
                            data = data.Replace('\0', ' ');
                            HttpRequestMessage request = new(HttpMethod.Post, "https://" + $"paste.denizenscript.com/New/{type}");
                            request.Content = new ByteArrayContent(StringConversionHelper.UTF8Encoding.GetBytes($"pastetype={type}&response=micro&v=200&"
                                + $"pastetitle=DenizenMetaBot Auto-Repaste Of {type} From {HttpUtility.UrlEncode(referenced.Author.Username)}&pastecontents={HttpUtility.UrlEncode(data)}\n\n"));
                            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                            HttpResponseMessage response = Program.ReusableWebClient.Send(request);
                            inputUrl = response.Content.ReadAsStringAsync().Result;
                            if (!string.IsNullOrWhiteSpace(inputUrl))
                            {
                                inputUrl = inputUrl.Trim();
                                Console.WriteLine($"Message {type} auto-repaste to {inputUrl}");
                                SendGenericPositiveMessageReply(command.Message, "Repasted", $"Direct Discord-upload of {type} reuploaded to pastebin at <{inputUrl}>");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (ex is HttpRequestException)
                {
                    SendErrorMessageReply(command.Message, "Error", $"Exception thrown while downloading or reuploading raw attachment data. HttpRequestException: `{EscapeUserInput(ex.Message)}`");
                }
                else
                {
                    SendErrorMessageReply(command.Message, "Error", "Exception thrown while handling special file scan command (see console for details).");
                }
                url = null;
                return null;
            }
            if (string.IsNullOrWhiteSpace(inputUrl))
            {
                SendErrorMessageReply(command.Message, "Command Syntax Incorrect", $"`{DenizenMetaBotConstants.COMMAND_PREFIX}{cmdName} <link>`");
                url = null;
                return null;
            }
            string rawUrl;
            if (inputUrl.StartsWith(PASTEBIN_URL_BASE))
            {
                string pastebinCode = inputUrl[PASTEBIN_URL_BASE.Length..];
                if (!PASTEBIN_CODE_VALIDATOR.IsOnlyMatches(pastebinCode))
                {
                    SendErrorMessageReply(command.Message, "Command Syntax Incorrect", "Pastebin URL given does not conform to expected format.");
                    url = null;
                    return null;
                }
                rawUrl = $"{PASTEBIN_URL_BASE}raw/{pastebinCode}";
            }
            else if (inputUrl.ToLowerFast().StartsWith(DENIZEN_PASTE_URL_BASE))
            {
                string pasteCode = inputUrl[DENIZEN_PASTE_URL_BASE.Length..];
                if (!HASTE_CODE_VALIDATOR.IsOnlyMatches(pasteCode))
                {
                    SendErrorMessageReply(command.Message, "Command Syntax Incorrect", "Denizen paste URL given does not conform to expected format.");
                    url = null;
                    return null;
                }
                rawUrl = $"{DENIZEN_PASTE_URL_BASE}{pasteCode}.txt";
            }
            else
            {
                SendErrorMessageReply(command.Message, "Command Syntax Incorrect", "Input argument must be a link to <https://one.denizenscript.com/haste>.");
                url = null;
                return null;
            }
            try
            {
                Task<string> downloadTask = Program.ReusableWebClient.GetStringAsync(rawUrl);
                downloadTask.Wait(WebLinkDownloadTimeout);
                if (!downloadTask.IsCompleted)
                {
                    SendErrorMessageReply(command.Message, "Error", "Download did not complete in time.");
                    url = null;
                    return null;
                }
                url = inputUrl;
                return downloadTask.Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (ex is HttpRequestException)
                {
                    SendErrorMessageReply(command.Message, "Error", $"Exception thrown while downloading raw data from link. HttpRequestException: `{EscapeUserInput(ex.Message)}`");
                }
                else
                {
                    SendErrorMessageReply(command.Message, "Error", "Exception thrown while downloading raw data from link (see console for details).");
                }
                url = null;
                return null;
            }
        }

        /// <summary>Command to check for common issues in server logs.</summary>
        public void CMD_LogCheck(CommandData command)
        {
            string data = GetWebLinkDataForCommand("logcheck", "log", command, out string url);
            if (data == null)
            {
                return;
            }
            if (data.CountCharacter('\n') < 100 && !data.Contains("Denizen Version: ") && !data.Contains("Starting minecraft server version"))
            {
                SendErrorMessageReply(command.Message, "Invalid Log", "Log file given looks like a snippet or not a valid log. Please post your full `logs/latest.log`, not just the snippet you think is relevant.\n\n"
                    + "All information is needed - especially the full startup output, which contains server/plugin versions, and usually is where important error messages are found.");
                return;
            }
            LogChecker checker = new(data);
            checker.Run();
            EmbedBuilder result = checker.GetResult().WithUrl(url).AddField("Checked For", $"<@{command.Message.Author.Id}>");
            SendReply(command.Message, result.Build());
        }

        /// <summary>Command to check the updatedness of a version string.</summary>
        public void CMD_VersionCheck(CommandData command)
        {
            if (command.CleanedArguments.Length == 0)
            {
                SendErrorMessageReply(command.Message, "Command Syntax Incorrect", $"`{DenizenMetaBotConstants.COMMAND_PREFIX}versioncheck <version text>`");
                return;
            }
            string combined = string.Join(" ", command.CleanedArguments).Trim();
            if (combined.ToLowerFast().StartsWith("loading "))
            {
                combined = combined["loading ".Length..].Trim();
            }
            if (combined.IsEmpty())
            {
                SendErrorMessageReply(command.Message, "Bad Input", "Input text doesn't look like a version string (blank input?).");
                return;
            }
            string projectName = BuildNumberTracker.SplitToNameAndVersion(combined, out string versionText).Replace(":", "").Trim();
            versionText = versionText.Trim();
            if (projectName.IsEmpty() || versionText.IsEmpty())
            {
                SendErrorMessageReply(command.Message, "Bad Input", "Input text doesn't look like a version string (single word input?).");
                return;
            }
            string nameLower = projectName.ToLowerFast();
            if (nameLower == "paper" || nameLower == "spigot" || nameLower == "craftbukkit")
            {
                string output = LogChecker.ServerVersionStatusOutput(combined, out bool isGood);
                if (string.IsNullOrWhiteSpace(output))
                {
                    SendErrorMessageReply(command.Message, "Bad Input", $"Input text looks like a {nameLower} version, but doesn't fit the expected {nameLower} server version format. Should start with '{nameLower} version git-{nameLower}-...'");
                    return;
                }
                if (isGood)
                {
                    SendGenericPositiveMessageReply(command.Message, "Running Current Build", $"That version is the current {nameLower} build for an acceptable server version.");
                }
                else
                {
                    SendGenericNegativeMessageReply(command.Message, "Build Outdated", $"{output}.");
                }
                return;
            }
            if (BuildNumberTracker.TryGetBuildFor(projectName, versionText, out BuildNumberTracker.BuildNumber build, out int buildNum))
            {
                if (build.IsCurrent(buildNum, out int behindBy))
                {
                    SendGenericPositiveMessageReply(command.Message, "Running Current Build", $"That version is the current {build.Name} build.");
                }
                else
                {
                    SendGenericNegativeMessageReply(command.Message, "Build Outdated", $"That version is an outdated {build.Name} build.\nThe current {build.Name} build is {build.Value}.\nYou are behind by {behindBy} builds.");
                }
                return;
            }
            SendErrorMessageReply(command.Message, "Bad Input", $"Input project name (`{EscapeUserInput(projectName)}`) doesn't look like any tracked project (or the version text is formatted incorrectly).");
            return;
        }


        /// <summary>Gets the result Discord embed for the script check.</summary>
        /// <returns>The embed to send.</returns>
        public Embed GetResult(ScriptChecker checker)
        {
            int totalWarns = checker.Errors.Count + checker.Warnings.Count + checker.MinorWarnings.Count;
            EmbedBuilder embed = new EmbedBuilder().WithTitle("Script Check Results").WithThumbnailUrl((totalWarns > 0) ? Constants.WARNING_ICON : Constants.INFO_ICON);
            int linesMissing = 0;
            int shortened = 0;
            void embedList(List<ScriptChecker.ScriptWarning> list, string title)
            {
                if (list.Count > 0)
                {
                    HashSet<string> usedKeys = new();
                    StringBuilder thisListResult = new(list.Count * 200);
                    foreach (ScriptChecker.ScriptWarning entry in list)
                    {
                        if (usedKeys.Contains(entry.WarningUniqueKey))
                        {
                            continue;
                        }
                        usedKeys.Add(entry.WarningUniqueKey);
                        StringBuilder lines = new(50);
                        if (entry.Line != -1)
                        {
                            lines.Append(entry.Line + 1);
                        }
                        foreach (ScriptChecker.ScriptWarning subEntry in list.SkipWhile(s => s != entry).Skip(1).Where(s => s.WarningUniqueKey == entry.WarningUniqueKey))
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
            embedList(checker.Errors, "Encountered Critical Errors");
            embedList(checker.Warnings, "Script Warnings");
            embedList(checker.MinorWarnings, "Minor Warnings");
            embedList(checker.Infos, "Other Script Information");
            if (linesMissing > 0)
            {
                embed.AddField("Missing Lines", $"There are {linesMissing} lines not able to fit in this result. Fix the listed errors to see the rest.");
            }
            if (shortened > 0)
            {
                embed.AddField("Shortened Lines", $"There are {shortened} lines that were merged into other lines.");
            }
            foreach (string debug in checker.Debugs)
            {
                Console.WriteLine($"Script checker debug: {debug}");
            }
            return embed.Build();
        }

        /// <summary>Command to check for common issues in script pastes.</summary>
        public void CMD_ScriptCheck(CommandData command)
        {
            if (MetaCommands.CheckMetaDenied(command.Message))
            {
                return;
            }
            string data = GetWebLinkDataForCommand("script", "script", command, out _);
            if (data == null)
            {
                return;
            }
            ScriptChecker checker = new(data);
            checker.Run();
            SendReply(command.Message, GetResult(checker));
        }
    }
}
