using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Net.Http;
using SharpDenizenTools.MetaHandlers;

namespace DenizenBot
{
    /// <summary>
    /// General program entry and handler.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Reusable HTTP(S) web client.
        /// </summary>
        public static HttpClient ReusableWebClient = new HttpClient();

        /// <summary>
        /// Software entry point - starts the bot.
        /// </summary>
        static void Main(string[] args)
        {
            MetaDocs.CurrentMeta = new MetaDocs();
            MetaDocs.CurrentMeta.DownloadAll();
            LaunchBotThread(args);
        }

        /// <summary>
        /// Launches a bot thread.
        /// </summary>
        public static void LaunchBotThread(string[] args)
        {
            Thread thr = new Thread(new ParameterizedThreadStart(BotThread)) { Name = "denizendiscordbot" };
            thr.Start(args);
        }

        /// <summary>
        /// The bot thread rootmost method, takes a string array object as input.
        /// </summary>
        public static void BotThread(Object obj)
        {
            try
            {
                new DenizenMetaBot().InitAndRun(obj as string[]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Discord crash: " + ex.ToString());
                Thread.Sleep(10 * 1000);
                LaunchBotThread(new string[0]);
            }
        }
    }
}
