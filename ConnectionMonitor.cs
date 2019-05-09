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

namespace DenizenBot
{
    /// <summary>
    /// Helper to monitor a Discord bot's connectivity.
    /// </summary>
    public class ConnectionMonitor
    {
        /// <summary>
        /// The bot to monitor.
        /// </summary>
        public DenizenMetaBot DiscordBot;

        /// <summary>
        /// Initializes the connection monitor. Call <see cref="StartMonitorLoop"/> to start the monitor loop.
        /// </summary>
        public ConnectionMonitor(DenizenMetaBot bot)
        {
            DiscordBot = bot;
        }

        /// <summary>
        /// Whether the bot has ever connected to Discord (since this instance of the class was started).
        /// </summary>
        public bool ConnectedOnce = false;

        /// <summary>
        /// Whether the bot believes itself to be currently connected to Discord. (If false, the bot is preparing or running a reconnection cycle).
        /// </summary>
        public bool ConnectedCurrently = false;

        /// <summary>
        /// Timespan the monitor should delay between connectivity checks.
        /// </summary>
        public TimeSpan MonitorLoopTime = new TimeSpan(hours: 0, minutes: 1, seconds: 0);

        /// <summary>
        /// Whether the monitor has already detected a potential issue (but has not yet enforced a restart,
        /// and is allowing a one loop-time delay period to automatically reconnect before enforcing a monitor reconnect).
        /// </summary>
        public bool MonitorWasFailedAlready = false;

        /// <summary>
        /// Whether all new bot logic should be stopped (usually indicates that a new bot instance is taking over).
        /// Do not use directly outside of monitor, instead use <see cref="ShouldStopAllLogic"/>.
        /// </summary>
        public bool StopAllLogic = false;

        /// <summary>
        /// Whether all new bot logic should be stopped (usually indicates that a new bot instance is taking over).
        /// </summary>
        public bool ShouldStopAllLogic()
        {
            lock (MonitorLock)
            {
                return StopAllLogic;
            }
        }

        /// <summary>
        /// Restarts the bot.
        /// </summary>
        public void ForceRestartBot()
        {
            lock (MonitorLock)
            {
                StopAllLogic = true;
            }
            Task.Factory.StartNew(() =>
            {
                DiscordBot.Client.StopAsync().Wait();
            });
            Program.CurrentBot = new DenizenMetaBot();
            Program.LaunchBotThread(new String[0]);
        }

        /// <summary>
        /// Lock object for monitor variables.
        /// </summary>
        public Object MonitorLock = new Object();

        /// <summary>
        /// The number of monitor loops thus far that the bot has not received input.
        /// </summary>
        public long LoopsSilent = 0;

        /// <summary>
        /// The number of monitor loops thus far that the bot has been active for.
        /// </summary>
        public long LoopsTotal = 0;

        /// <summary>
        /// Starts the monitor loop thread.
        /// </summary>
        public void StartMonitorLoop()
        {
            Thread thr = new Thread(new ThreadStart(LoopUntilFail));
            thr.Name = "connectionmonitor";
            thr.Start();
        }

        /// <summary>
        /// Loops the monitor until the bot has disconnected.
        /// </summary>
        public void LoopUntilFail()
        {
            while (true)
            {
                Task.Delay(MonitorLoopTime).Wait();
                if (ShouldStopAllLogic())
                {
                    return;
                }
                try
                {
                    MonitorLoopOnce();
                }
                catch (Exception ex)
                {
                    if (ex is ThreadAbortException)
                    {
                        throw;
                    }
                    Console.WriteLine("Connection monitor loop had exception: " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Internal loop method (ran by a separate looping thread handler) for the monitor.
        /// </summary>
        public void MonitorLoopOnce()
        {
            bool isConnected;
            lock (MonitorLock)
            {
                LoopsSilent++;
                LoopsTotal++;
                isConnected = ConnectedCurrently && DiscordBot.Client.ConnectionState == ConnectionState.Connected;
            }
            if (!isConnected)
            {
                Console.WriteLine("Monitor detected disconnected state!");
            }
            if (LoopsSilent > 60)
            {
                Console.WriteLine("Monitor detected over an hour of silence, and is assuming a disconnected state!");
                isConnected = false;
            }
            if (LoopsTotal > 60 * 12)
            {
                Console.WriteLine("Monitor detected that the bot has been running for over 12 hours, and will restart soon!");
                isConnected = false;
            }
            if (isConnected)
            {
                MonitorWasFailedAlready = false;
            }
            else
            {
                if (MonitorWasFailedAlready)
                {
                    Console.WriteLine("Monitor is enforcing a restart!");
                    ForceRestartBot();
                }
                else
                {
                    MonitorWasFailedAlready = true;
                }
            }
        }
    }
}
