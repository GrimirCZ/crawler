using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Downloader
{
    /*
     * Traverses nodes, level by level
     */
    internal class LevelDescendingCrawler : ICrawler
    {
        private class LevelManager
        {
            public class LevelNode
            {
                public string Url;
                public string PrevUrl;

                private LevelNode()
                {
                }

                public static LevelNode Create(string url, string prevUrl)
                {
                    return new LevelNode()
                    {
                        Url = url,
                        PrevUrl = prevUrl
                    };
                }
            }

            private ImmutableDictionary<uint, ImmutableList<LevelNode>> Levels { get; set; }

            private LevelManager()
            {
                Levels = ImmutableDictionary.Create<uint, ImmutableList<LevelNode>>();
            }

            public static LevelManager Create()
            {
                return new LevelManager();
            }

            public void Add(LevelNode url, uint level)
            {
                if (!Levels.ContainsKey(level)) // if it does not contain the key, create it
                {
                    Levels = Levels.Add(level, ImmutableList<LevelNode>.Empty);
                }

                Levels = Levels.SetItem(level, Levels[level].Add(url));
            }

            public IEnumerable<LevelNode> GetLevel(uint level)
            {
                if (!Levels.ContainsKey(level))
                {
                    throw new InvalidOperationException($"Requested level {level} not found!");
                }

                return Levels[level].ToList();
            }

            public bool Contains(string url)
            {
                return Levels.Values.Any(list => list.Any(node => node.Url == url));
            }
        }

        private string _baseUrl;
        private LevelManager _levelManager;

        /*--PROPERTIES-START--*/
        private ImmutableHashSet<string> _crawledUrls;

        public string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = Helper.NormalizeUrl(value);
        }

        public bool IgnoreHttpErrors { get; set; } = true;

        public int TargetDepth { get; set; } = 5;

        public IImmutableList<Action<PageCrawlStartedData>> OnPageCrawlStartedHandlers { get; set; }

        public IImmutableList<Action<PageCrawlEndedData>> OnPageCrawlEndedHandlers { get; set; }

        /*--PROPERTIES-END--*/

        /*--PRIVATE-METHODS-START--*/
        private LevelDescendingCrawler(ICrawler prev)
        {
            BaseUrl = prev.BaseUrl;
            IgnoreHttpErrors = prev.IgnoreHttpErrors;
            TargetDepth = prev.TargetDepth;
            OnPageCrawlStartedHandlers = prev.OnPageCrawlStartedHandlers;
            OnPageCrawlEndedHandlers = prev.OnPageCrawlEndedHandlers;
        }

        private void _notifyPageCrawlStarted(uint depth, string url, string prevUrl)
        {
            var data = new PageCrawlStartedData
            {
                Depth = depth,
                URL = url,
                PreviousUrl = prevUrl
            };

            foreach (var handler in OnPageCrawlStartedHandlers)
            {
                handler(data);
            }
        }

        private void _notifyPageCrawlEnded(uint depth, string url, string prevUrl, string title, HtmlDocument doc)
        {
            var data = new PageCrawlEndedData()
            {
                Depth = depth,
                URL = url,
                PreviousUrl = prevUrl,
                Title = title,
                Doc = doc
            };

            foreach (var handler in OnPageCrawlEndedHandlers)
            {
                handler(data);
            }
        }

        private static async Task<HtmlDocument> _getPageDoc(string url)
        {
            using (var wc = new WebClient())
            {
                var ret = new HtmlDocument();

                var htmlPage = await wc.DownloadDataTaskAsync(url);

                ret.LoadHtml(Encoding.UTF8.GetString(htmlPage, 0, htmlPage.Length));

                return ret;
            }
        }


        private async Task _crawlPage(string url, string prevUrl, uint depth)
        {
            if (depth > TargetDepth || _crawledUrls.Contains(url))
            {
                return;
            }

            _notifyPageCrawlStarted(depth, url, prevUrl);

            HtmlDocument doc;

            try // ignore HTTP errors handler
            {
                doc = await _getPageDoc(url);
            }
            catch (WebException)
            {
                if (!IgnoreHttpErrors)
                {
                    throw;
                }

                return;
            }

            _crawledUrls = _crawledUrls.Add(url);

            // Get all unseen links on page
            var links = doc
                .DocumentNode
                .Descendants("a")
                .GetUnseenUrls(_levelManager.Contains)
                .ToList();

            // Get title of page
            var title = doc.GetPageTitle().DecodeHtmlSpecialChars();

            _notifyPageCrawlEnded(depth, url, prevUrl, title, doc); // notify page crawled

            foreach (var link in links)
            {
                _levelManager.Add(LevelManager.LevelNode.Create(link, url), depth + 1);
            }
        }

        /*--PRIVATE-METHODS-END--*/
        /*--PUBLIC-INTERFACE-START--*/
        public LevelDescendingCrawler()
        {
            OnPageCrawlStartedHandlers = ImmutableArray.Create<Action<PageCrawlStartedData>>();
            OnPageCrawlEndedHandlers = ImmutableArray.Create<Action<PageCrawlEndedData>>();
        }


        public ICrawler Clone(ICrawler prev)
        {
            return new EagerDescendingCrawler
            {
                BaseUrl = prev.BaseUrl,
                IgnoreHttpErrors = prev.IgnoreHttpErrors,
                TargetDepth = prev.TargetDepth,

                OnPageCrawlStartedHandlers = prev.OnPageCrawlStartedHandlers,
                OnPageCrawlEndedHandlers = prev.OnPageCrawlEndedHandlers,
            };
        }

        public async Task Run()
        {
            _levelManager = LevelManager.Create();
            _crawledUrls = ImmutableHashSet<string>.Empty;

            await _crawlPage(_baseUrl, "Root", depth: 0);

            for (uint level = 1; level <= TargetDepth; level++)
            {
                var runningCrawlers = _levelManager
                    .GetLevel(level)
                    .Select(node => _crawlPage(node.Url, node.PrevUrl, level))
                    .ToArray();

                Task.WaitAll(runningCrawlers);
            }
        }


        public ICrawler WithUrl(string url)
        {
            return new LevelDescendingCrawler()
            {
                BaseUrl = url
            };
        }

        public ICrawler WithTargetDepth(int targetDepth)
        {
            return new LevelDescendingCrawler(this) {TargetDepth = targetDepth};
        }

        public ICrawler SetIgnoreHttpErrors(bool ignore)
        {
            return new LevelDescendingCrawler(this) {IgnoreHttpErrors = ignore};
        }

        public ICrawler OnPageCrawlStarted(Action<PageCrawlStartedData> handler)
        {
            var ret = new LevelDescendingCrawler(this)
            {
                OnPageCrawlStartedHandlers = OnPageCrawlStartedHandlers.Add(handler)
            };

            return ret;
        }

        public ICrawler OnPageCrawlEnded(Action<PageCrawlEndedData> handler)
        {
            var ret = new LevelDescendingCrawler(this)
            {
                OnPageCrawlEndedHandlers = OnPageCrawlEndedHandlers.Add(handler)
            };

            return ret;
        }

        public bool HaveCrawledUrl(string url)
        {
            return _crawledUrls.Contains(url);
        }

        public bool HaveSeenUrl(string url)
        {
            return _levelManager.Contains(url);
        }

        /*--PUBLIC-INTERFACE-END--*/
    }
}