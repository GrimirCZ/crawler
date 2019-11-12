using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Downloader
{
    /*
     * Eagerly traverses the node tree (as they appear)
     * => can lead to not exploring nodes at the higher levels in a later stage, if they appear as descendants of one of the earlier nodes
     */
    internal class EagerDescendingCrawler : ICrawler
    {
        private string _baseUrl;

        /*--PROPERTIES-START--*/
        public string BaseUrl
        {
            get => _baseUrl;
            set => _baseUrl = Helper.NormalizeUrl(value);
        }

        public bool IgnoreHttpErrors { get; set; } = true;

        public int TargetDepth { get; set; } = 5;

        private IImmutableList<string> SeenUrls { get; set; }
        private IImmutableList<string> CrawledUrls { get; set; }

        public IImmutableList<Action<PageCrawlStartedData>> OnPageCrawlStartedHandlers { get; set; }

        public IImmutableList<Action<PageCrawlEndedData>> OnPageCrawlEndedHandlers { get; set; }

        /*--PROPERTIES-END--*/

        /*--PRIVATE-METHODS-START--*/


        private EagerDescendingCrawler(ICrawler prev)
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

        private async Task<HtmlDocument> _getPageDoc(string url)
        {
            using (var wc = new WebClient())
            {
                var ret = new HtmlDocument();

                var htmlPage = await wc.DownloadDataTaskAsync(url);

                ret.LoadHtml(Encoding.UTF8.GetString(htmlPage, 0, htmlPage.Length));

                return ret;
            }
        }

        private static string _getPageTitle(HtmlDocument doc)
        {
            var titleTag = doc.DocumentNode.Descendants("title").FirstOrDefault();

            return titleTag is null ? "No title" : titleTag.InnerText;
        }

        private async Task _crawlPage(string url, string prevUrl, uint depth)
        {
            if (depth > TargetDepth || CrawledUrls.Contains(url))
            {
                return;
            }

            _notifyPageCrawlStarted(depth, url, prevUrl);


            HtmlDocument doc;

            try // ignore HTTP errors handler
            {
                doc = await _getPageDoc(url);
            }
            catch (WebException e)
            {
                if (!IgnoreHttpErrors)
                {
                    throw;
                }

                return;
            }

            var links = doc.DocumentNode
                .Descendants("a")
                .GetUnseenUrls(SeenUrls.Contains)
                .ToList();

            var title = _getPageTitle(doc).DecodeHtmlSpecialChars();

            _notifyPageCrawlEnded(depth, url, prevUrl, title, doc); // notify page crawled

            CrawledUrls = CrawledUrls.Add(url);

            foreach (var link in links)
            {
                SeenUrls = SeenUrls.Add(link);

                await _crawlPage(link, url, depth + 1);
            }
        }

        /*--PRIVATE-METHODS-END--*/
        /*--PUBLIC-INTERFACE-START--*/
        public EagerDescendingCrawler()
        {
            SeenUrls = ImmutableArray.Create<string>();
            OnPageCrawlStartedHandlers = ImmutableArray.Create<Action<PageCrawlStartedData>>();
            OnPageCrawlEndedHandlers = ImmutableArray.Create<Action<PageCrawlEndedData>>();
        }


        public ICrawler Clone(ICrawler prev)
        {
            return new EagerDescendingCrawler()
            {
                BaseUrl = prev.BaseUrl,
                IgnoreHttpErrors = prev.IgnoreHttpErrors,
                TargetDepth = prev.TargetDepth,

                OnPageCrawlStartedHandlers = prev.OnPageCrawlStartedHandlers,
                OnPageCrawlEndedHandlers = prev.OnPageCrawlEndedHandlers,

                SeenUrls = ImmutableList.Create<string>()
            };
        }

        public async Task Run()
        {
            SeenUrls = ImmutableArray.Create<string>();
            CrawledUrls = ImmutableList.Create<string>();

            await _crawlPage(_baseUrl, "Root", depth: 0);
        }


        public ICrawler WithUrl(string url)
        {
            return new EagerDescendingCrawler()
            {
                BaseUrl = url
            };
        }

        public ICrawler WithTargetDepth(int targetDepth)
        {
            return new EagerDescendingCrawler(this) {TargetDepth = targetDepth};
        }

        public ICrawler SetIgnoreHttpErrors(bool ignore)
        {
            return new EagerDescendingCrawler(this) {IgnoreHttpErrors = ignore};
        }

        public ICrawler OnPageCrawlStarted(Action<PageCrawlStartedData> handler)
        {
            var ret = new EagerDescendingCrawler(this)
            {
                OnPageCrawlStartedHandlers = OnPageCrawlStartedHandlers.Add(handler)
            };

            return ret;
        }

        public ICrawler OnPageCrawlEnded(Action<PageCrawlEndedData> handler)
        {
            var ret = new EagerDescendingCrawler(this)
            {
                OnPageCrawlEndedHandlers = OnPageCrawlEndedHandlers.Add(handler)
            };

            return ret;
        }

        public bool HaveSeenUrl(string url)
        {
            return SeenUrls.Contains(url);
        }

        public bool HaveCrawledUrl(string url)
        {
            return CrawledUrls.Contains(url);
        }

        /*--PUBLIC-INTERFACE-END--*/
    }
}