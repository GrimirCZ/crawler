using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Downloader
{
    public class PageCrawlStartedData
    {
        /**
         * Depth the Page was found on
         */
        public uint Depth { get; set; }

        /**
         * Url of the page
         */
        public string URL { get; set; }

        /**
         * Url where this page was found
         */
        public string PreviousUrl { get; set; }
    }

    public class PageCrawlEndedData
    {
        /**
         * Depth tha Page was found on
         */
        public uint Depth { get; set; }

        /**
         * Url of the Page
         */
        public string URL { get; set; }

        /**
         * Url where this Page was found
         */
        public string PreviousUrl { get; set; }

        /**
         * Title of the Page
         */
        public string Title { get; set; }

        /**
         * Page parsed as HtmlDocument
         */
        public HtmlDocument Doc { get; set; }
    }

    public interface ICrawler
    {
        /**
         * Executes the crawler
         */
        Task Run();

        /**
         * Creates new instance of crawler, that inherits all properties of the input crawler
         */
        ICrawler Clone(ICrawler crawler);

        /**
         * Sets URL to be crawled
         */
        ICrawler WithUrl(string url);

        /**
         * Maximum depth the crawler will go to 
         */
        ICrawler WithTargetDepth(int targetDepth);

        /**
         * Controls whether or not to throw exception on HTTP error codes
         */
        ICrawler SetIgnoreHttpErrors(bool ignore);

        /**
         * Add Handler for PageCrawlStarted event
         */
        ICrawler OnPageCrawlStarted(Action<PageCrawlStartedData> handler);

        /**
         * Add handler for PageCrawlEnded event
         */
        ICrawler OnPageCrawlEnded(Action<PageCrawlEndedData> handler);

        /**
         * Returns whether the url was encountered during crawl.
         * !!! The url is not guaranteed to be crawled yet.
         */
        bool HaveSeenUrl(string url);

        /**
         * Returns whether the url was crawled.
         */
        bool HaveCrawledUrl(string url);

        /**
         * Url to be crawled
         */
        string BaseUrl { get; set; }

        /**
         * Controls whether or not to throw exception on HTTP error codes
         */
        bool IgnoreHttpErrors { get; set; }

        /**
         * Maximum depth the crawler will go to 
         */
        int TargetDepth { get; set; }

        /**
         * List of event handlers for PageCrawlStarted event
         */
        IImmutableList<Action<PageCrawlStartedData>> OnPageCrawlStartedHandlers { get; set; }

        /**
         * List of event handlers for PageCrawlEnded event
         */
        IImmutableList<Action<PageCrawlEndedData>> OnPageCrawlEndedHandlers { get; set; }
    }

    public static class HtmlExtensions
    {
        /**
         * Method returns Unseen urls from Enumerable of HtmlNodes
         * It filters found urls against haveSeenUrl function
         */
        public static IEnumerable<string> GetUnseenUrls(this IEnumerable<HtmlNode> srcUrls,
            Func<string, bool> haveSeenUrl)
        {
            return srcUrls
                .Where(a => a.Attributes["href"] != null) // damn everyone who writes <a> without href
                .Select(a => a.Attributes["href"].Value)
                .Where(Helper.IsValidUrl)
                .Select(Helper.NormalizeUrl)
                .Where(a => !haveSeenUrl(a))
                .Distinct();
        }


        /**
         * Extracts Title of page from html doc
         * If no title found, returns string "No title"
         */
        public static string GetPageTitle(this HtmlDocument doc)
        {
            var titleTag = doc.DocumentNode.Descendants("title").FirstOrDefault();

            return titleTag is null ? "No title" : titleTag.InnerText;
        }
    }

    public static class Helper
    {
        /**
         * Takes string formatted with HTMLSpecialChars and returns UTF-8 representation
         */
        public static string DecodeHtmlSpecialChars(this string str)
        {
            return WebUtility.HtmlDecode(str);
        }

        /**
         * Normalizes input url
         * Removes trailing id (#)
         * Removes GET parameters
         * Adds trailing slash
         */
        public static string NormalizeUrl(string url)
        {
            url = url.Split('#')[0];
            url = url.Split('?')[0];

            if (!url.EndsWith("/"))
            {
                url += "/";
            }

            return url;
        }

        /**
         * Returns whether or not the input is a valid url
         */
        public static bool IsValidUrl(this string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }

    /**
     * Constructs new Crawler
     */
    public static class CrawlerBuilder<T> where T : ICrawler, new()
    {
        public static T Create()
        {
            return new T();
        }
    }

    /**
     * Exception, that is thrown if there is no internet connection
     */
    public class OfflineException : Exception
    {
        public OfflineException(string url) : base($"You are offline, failed to download {url}.")
        {
        }
    }
}