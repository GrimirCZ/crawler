using System;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Downloader
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var userUrl = GetUserUrl();

            Task crawl = CrawlerBuilder<LevelDescendingCrawler>
                .Create()
                .WithUrl(userUrl)
                .WithTargetDepth(4)
                .OnPageCrawlEnded(PrintSiteLevelDescending)
                .Run();


            Task.WaitAll(crawl);


            Console.WriteLine("Press key to end this world...");
            Console.ReadKey();
        }

        private static string GetUserUrl()
        {
            var url = "";
            while (!url.IsValidUrl())
            {
                Console.Write("Zadejte url: ");
                url = Console.ReadLine();

                if (!url.IsValidUrl())
                {
                    Console.WriteLine("Vámi zadaný text není validní url! (opakujte pokus)\n\n");
                }
            }

            return url;
        }

        private static void PrintSiteEagerDescending(PageCrawlEndedData data)
        {
            var str = (data.Title.Replace('\n', ' ').Trim() + " " + data.URL.Trim()).Trim();

            var padding = "";

            if (data.Depth > 0)
            {
                for (var i = 0; i < data.Depth - 1; i++)
                {
                    padding += "|";
                }

                padding += "┕ ";
            }

            Console.WriteLine(padding + str);
        }

        private static void PrintSiteLevelDescending(PageCrawlEndedData data)
        {
            Console.WriteLine((data.Depth + " " + data.Title.Replace('\n', ' ').Trim() + " " + data.URL.Trim()).Trim());
        }
    }
}