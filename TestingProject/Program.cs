using System;
using System.IO;
using System.Threading.Tasks;

namespace TestingProject
{
    class Program
    {
        const string url = "https://soundcloud.com/search?q=rules%20of%20nature%20acoustic&query_urn=soundcloud%3Asearch-autocomplete%3A256d9e91fd8d4099b0af28902c694c18";
        static void Main(string[] args)
            => MainAsync().GetAwaiter().GetResult();
        static async Task MainAsync()
        {
            Directory.CreateDirectory("Temp");
            using (var fs = File.Create("Temp/bytes.txt"))
            {
                await SoundcloudDLHelper.WebSocket.DownloadBytesTo(fs, url);
            }
        }
    }
}
