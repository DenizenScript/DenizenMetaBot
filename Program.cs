using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord.Net;
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticDataSyntax;
using DenizenBot;

namespace DenizenBot
{
    /// <summary>
    /// General program entry and handler.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The current bot object (the instance will change if the bot is restarted).
        /// </summary>
        public static DenizenMetaBot CurrentBot = null;

        /// <summary>
        /// The current meta documentation (the instance which change if meta is reloaded).
        /// </summary>
        public static MetaDocs CurrentMeta = null;

        /// <summary>
        /// Software entry point - starts the bot.
        /// </summary>
        static void Main(string[] args)
        {
            CurrentMeta = new MetaDocs();
            CurrentMeta.DownloadAll();
            CurrentBot = new DenizenMetaBot();
            LaunchBotThread(args);
        }

        /// <summary>
        /// Launches a bot thread.
        /// </summary>
        public static void LaunchBotThread(string[] args)
        {
            Thread thr = new Thread(new ParameterizedThreadStart(BotThread));
            thr.Name = "denizendiscordbot";
            thr.Start(args);
        }

        /// <summary>
        /// The bot thread rootmost method, takes a string array object as input.
        /// </summary>
        public static void BotThread(Object obj)
        {
            try
            {
                CurrentBot.InitAndRun(obj as string[]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Discord crash: " + ex.ToString());
                Thread.Sleep(10 * 1000);
                try
                {
                    Thread.CurrentThread.Name = "discordbotthread_dead" + new Random().Next(5000);
                }
                catch (InvalidOperationException)
                {
                    // Ignore
                }
                LaunchBotThread(new string[0]);
            }
        }
    }
}
