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


           
            Task craw = CrawlerBuilder<LevelDescendingCrawler>
                .Create()
                .WithUrl("https://delta-skola.cz")
                .WithTargetDepth(4)
                .OnPageCrawlEnded(PrintSiteLevelDescending)
                .Run();

            Task.WhenAll(craw);
        
            Console.ReadKey();
            
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