using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        const string url = "https://soundcloud.com/search?q=there%20are%20spaces";
        static readonly string[] entries = new string[]
        {
            "there are spaces", "dion timmer", "monstercat cotw", "trust on you", "tinh dang nhu ly cafe",
            "careless whisper", "happy valentine day", "justatee", "rhymastic", "hoaprox", "touliver"
        };
        static void Main(string[] args)
        {
            GetEntriesTest().GetAwaiter().GetResult();
            Task.Delay(-1).GetAwaiter().GetResult();
        }

        static async Task GetEntriesTest()
        {
            for(int i =0;i<entries.Length;i++)
            {
                Console.WriteLine("================================");
                Console.WriteLine("ENTRY: " + entries[i]);
                string[] strs = await SoundcloudDLHelper.Search.GetMatchingUrlSerialize(entries[i]);
                if (strs.Length == 0)
                    Console.WriteLine("No entry");
                else
                    for (int j = 0; j < strs.Length; j++)
                    {
                        Console.WriteLine(strs[j]);
                    }

            }
        }
        static async Task DownloadTest()
        {
            Directory.CreateDirectory("Temp");
            Console.WriteLine("Created directory temp");
            var fs = File.Create("Temp/cache.txt");
            fs.Close();
            await SoundcloudDLHelper.WebSocket.DownloadBytesTo("Temp/cache.txt", url);
            Console.WriteLine(await SoundcloudDLHelper.WebSocket.GetStringFrom(url));
        }
        static async Task Test()
        {
            Console.WriteLine(await SoundcloudDLHelper.WebSocket.GetStringFrom(url));
        }
    }
}
