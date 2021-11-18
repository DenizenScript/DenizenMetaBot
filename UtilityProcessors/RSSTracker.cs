using DiscordBotBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Discord;
using Discord.WebSocket;

namespace DenizenBot.UtilityProcessors
{
    /// <summary>Tracks and announces RSS feeds.</summary>
    public class RSSTracker
    {
        /// <summary>The URL to track.</summary>
        public string URL;

        /// <summary>The channel(s) to post messages into.</summary>
        public ulong[] Channels;

        /// <summary>How often to check for updates.</summary>
        public TimeSpan CheckRate = new(hours: 0, minutes: 5, seconds: 0);

        /// <summary>Used to cancel the tracker.</summary>
        public CancellationTokenSource CancelToken = new();

        /// <summary>Associated monitor object.</summary>
        public ConnectionMonitor Monitor;

        /// <summary>Constructs the tracker.</summary>
        public RSSTracker(string _url, ulong[] _channels, ConnectionMonitor _monitor, TimeSpan _checkRate)
        {
            URL = _url;
            Channels = _channels;
            CheckRate = _checkRate;
            Monitor = _monitor;
        }

        /// <summary>Starts the tracker.</summary>
        public void Start()
        {
            Task.Factory.StartNew(InternalLoop);
        }

        /// <summary>Seen RSS feed 'pubDates' value. The publish date is the easiest way to reliably differentiate most feeds.</summary>
        public HashSet<string> SeenDates = new();

        /// <summary>Does the actual scan and update.</summary>
        public void ScanNow(bool doPosts)
        {
            try
            {
                string content = Program.ReusableWebClient.GetStringAsync(URL, CancelToken.Token).Result;
                XElement root = XDocument.Parse(content).Root;
                XElement channel = root.Nodes().Cast<XElement>().First(e => e.Name == "channel");
                string siteTitle = channel.Nodes().Cast<XElement>().First(e => e.Name == "title").Value;
                IEnumerable<XElement> items = channel.Nodes().Cast<XElement>().Where(e => e.Name == "item");
                foreach (XElement item in items)
                {
                    IEnumerable<XElement> itemData = item.Nodes().Cast<XElement>();
                    string pubDate = itemData.First(e => e.Name == "pubDate").Value;
                    if (!SeenDates.Add(pubDate))
                    {
                        continue;
                    }
                    string title = itemData.First(e => e.Name == "title").Value;
                    string link = itemData.First(e => e.Name == "link").Value;
                    string creator = itemData.First(e => e.Name.LocalName == "creator").Value;
                    Console.WriteLine($"RSS feed {URL} found new post {link}");
                    DateTimeOffset date = DateTimeOffset.Parse(pubDate);
                    if (date.AddDays(7) < DateTimeOffset.UtcNow)
                    {
                        Console.WriteLine($"Ignore due to being very outdated");
                        continue;
                    }
                    if (doPosts)
                    {
                        EmbedBuilder embed = new()
                        {
                            Title = $"Feed: {siteTitle}",
                            Author = new EmbedAuthorBuilder() { Name = creator },
                            Url = link,
                            Timestamp = date,
                            Color = new Color(255, 150, 15),
                            Description = $"New post in thread `{title.Replace('`', '\'')}`"
                        };
                        foreach (ulong chanId in Channels)
                        {
                            SocketChannel chan = DiscordBotBaseHelper.CurrentBot.Client.GetChannel(chanId);
                            if (chan is not SocketTextChannel textChan)
                            {
                                Console.WriteLine($"While updating RSS feed {URL} had problem: channel ID {chanId} is invalid");
                            }
                            else
                            {
                                textChan.SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None).Wait();
                            }
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"While updating RSS feed {URL} had exception: {ex}");
            }
        }

        /// <summary>The actual internal loop thread for this tracker.</summary>
        public void InternalLoop()
        {
            CancellationToken token = CancelToken.Token;
            Task.Delay(TimeSpan.FromMinutes(1));
            ScanNow(false);
            while (true)
            {
                try
                {
                    Task.Delay(CheckRate, token).Wait();
                    ScanNow(true);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                if (token.IsCancellationRequested || Monitor.ShouldStopAllLogic())
                {
                    return;
                }
            }
        }
    }
}
