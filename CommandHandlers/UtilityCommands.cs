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
        /// Reusable HTTP(S) web client.
        /// </summary>
        public static HttpClient ReusableWebClient = new HttpClient();

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
                Task<string> downloadTask = ReusableWebClient.GetStringAsync(rawUrl);
                downloadTask.Wait(WebLinkDownloadTimeout);
                return downloadTask.Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (ex is HttpRequestException)
                {
                    SendErrorMessageReply(message, "Error", $"Exception thrown while downloading raw data from link. HttpRequestException: {ex.Message}");
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
    }
}
