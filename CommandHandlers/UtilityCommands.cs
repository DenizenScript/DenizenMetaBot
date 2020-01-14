using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Discord.Net;
using Discord;
using Discord.WebSocket;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticToolkit;
using DenizenBot.UtilityProcessors;

namespace DenizenBot.CommandHandlers
{
    /// <summary>
    /// Commands to perform utility functions.
    /// </summary>
    public class UtilityCommands : UserCommands
    {
        /// <summary>
        /// Base URL for paste sites.
        /// </summary>
        public const string PASTEBIN_URL_BASE = "https://pastebin.com/",
            DENIZEN_PASTE_URL_BASE = "https://one.denizenscript.com/paste/",
            DENIZEN_HASTE_URL_BASE = "https://one.denizenscript.com/haste/";

        /// <summary>
        /// ASCII validator for a pastebin ID.
        /// </summary>
        public static AsciiMatcher PASTEBIN_CODE_VALIDATOR = new AsciiMatcher((c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));

        /// <summary>
        /// ASCII validator for a Denizen haste code.
        /// </summary>
        public static AsciiMatcher HASTE_CODE_VALIDATOR = new AsciiMatcher((c) => c >= '0' && c <= '9');

        /// <summary>
        /// The max wait time for a web-link download in a command.
        /// </summary>
        public static TimeSpan WebLinkDownloadTimeout = new TimeSpan(hours: 0, minutes: 0, seconds: 15);

        /// <summary>
        /// For a web-link command like '!logcheck', gets the data from the paste link.
        /// </summary>
        public string GetWebLinkDataForCommand(string inputUrl, SocketMessage message)
        {
            string rawUrl;
            if (inputUrl.StartsWith(PASTEBIN_URL_BASE))
            {
                string pastebinCode = inputUrl.Substring(PASTEBIN_URL_BASE.Length);
                if (!PASTEBIN_CODE_VALIDATOR.IsOnlyMatches(pastebinCode))
                {
                    SendErrorMessageReply(message, "Command Syntax Incorrect", "Pastebin URL given does not conform to expected format.");
                    return null;
                }
                rawUrl = $"{PASTEBIN_URL_BASE}raw/{pastebinCode}";
            }
            else if (inputUrl.StartsWith(DENIZEN_PASTE_URL_BASE) || inputUrl.StartsWith(DENIZEN_HASTE_URL_BASE))
            {
                string pasteCode = inputUrl.Substring(DENIZEN_HASTE_URL_BASE.Length).Before('/');
                if (!HASTE_CODE_VALIDATOR.IsOnlyMatches(pasteCode))
                {
                    SendErrorMessageReply(message, "Command Syntax Incorrect", "Denizen haste URL given does not conform to expected format.");
                    return null;
                }
                rawUrl = $"{DENIZEN_HASTE_URL_BASE}{pasteCode}.txt";
            }
            else
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "Input argument must be a link to pastebin or <https://one.denizenscript.com/haste>.");
                return null;
            }
            try
            {
                Task<string> downloadTask = Program.ReusableWebClient.GetStringAsync(rawUrl);
                downloadTask.Wait(WebLinkDownloadTimeout);
                if (!downloadTask.IsCompleted)
                {
                    SendErrorMessageReply(message, "Error", "Download did not complete in time.");
                    return null;
                }
                return downloadTask.Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (ex is HttpRequestException)
                {
                    SendErrorMessageReply(message, "Error", $"Exception thrown while downloading raw data from link. HttpRequestException: `{EscapeUserInput(ex.Message)}`");
                }
                else
                {
                    SendErrorMessageReply(message, "Error", "Exception thrown while downloading raw data from link (see console for details).");
                }
                return null;
            }
        }

        /// <summary>
        /// Command to check for common issues in server logs.
        /// </summary>
        public void CMD_LogCheck(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "`!logcheck <link>`");
                return;
            }
            string data = GetWebLinkDataForCommand(cmds[0], message);
            if (data == null)
            {
                return;
            }
            LogChecker checker = new LogChecker(data);
            checker.Run();
            SendReply(message, checker.GetResult());
        }

        /// <summary>
        /// Command to check the updatedness of a version string.
        /// </summary>
        public void CMD_VersionCheck(string[] cmds, SocketMessage message)
        {
            if (cmds.Length == 0)
            {
                SendErrorMessageReply(message, "Command Syntax Incorrect", "`!versioncheck <version text>`");
                return;
            }
            string combined = string.Join(" ", cmds).Trim();
            if (combined.ToLowerFast().StartsWith("loading "))
            {
                combined = combined.Substring("loading ".Length).Trim();
            }
            if (combined.IsEmpty())
            {
                SendErrorMessageReply(message, "Bad Input", "Input text doesn't look like a version string (blank input?).");
                return;
            }
            string projectName = BuildNumberTracker.SplitToNameAndVersion(combined, out string versionText).Replace(":", "").Trim();
            versionText = versionText.Trim();
            if (projectName.IsEmpty() || versionText.IsEmpty())
            {
                SendErrorMessageReply(message, "Bad Input", "Input text doesn't look like a version string (single word input?).");
                return;
            }
            string nameLower = projectName.ToLowerFast();
            if (nameLower == "paper" || nameLower == "spigot" || nameLower == "craftbukkit")
            {
                string output = LogChecker.ServerVersionStatusOutput(combined, out bool isGood);
                if (string.IsNullOrWhiteSpace(output))
                {
                    SendErrorMessageReply(message, "Bad Input", $"Input text looks like a {nameLower} version, but doesn't fit the expected {nameLower} server version format. Should start with '{nameLower} version git-{nameLower}-...'");
                    return;
                }
                if (isGood)
                {
                    SendGenericPositiveMessageReply(message, "Running Current Build", $"That version is the current {nameLower} build for an acceptable server version.");
                }
                else
                {
                    SendGenericNegativeMessageReply(message, "Build Outdated", $"{output}.");
                }
                return;
            }
            if (BuildNumberTracker.TryGetBuildFor(projectName, versionText, out BuildNumberTracker.BuildNumber build, out int buildNum))
            {
                if (build.IsCurrent(buildNum, out int behindBy))
                {
                    SendGenericPositiveMessageReply(message, "Running Current Build", $"That version is the current {build.Name} build.");
                }
                else
                {
                    SendGenericNegativeMessageReply(message, "Build Outdated", $"That version is an outdated {build.Name} build.\nThe current {build.Name} build is {build.Value}.\nYou are behind by {behindBy} builds.");
                }
                return;
            }
            SendErrorMessageReply(message, "Bad Input", $"Input project name (`{EscapeUserInput(projectName)}`) doesn't look like any tracked project (or the version text is formatted incorrectly).");
            return;
        }
    }
}
