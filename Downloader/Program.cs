﻿using System;
using System.Data;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Downloader
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

//            await CrawlerBuilder<EagerDescendingCrawler>
//                .Create()
//                .WithUrl("http://delta-skola.cz")
//                .WithTargetDepth(2)
//                .OnPageCrawlEnded(PrintSiteEagerDescending)
//                .Run();

            await CrawlerBuilder<LevelDescendingCrawler>
                .Create()
                .WithUrl("https://delta-skola.cz")
                .WithTargetDepth(4)
                .OnPageCrawlEnded(PrintSiteLevelDescending)
                .Run();

//                .WithUrl("http://faltvi.maweb.eu")
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