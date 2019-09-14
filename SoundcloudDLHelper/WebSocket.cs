using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;

namespace SoundcloudDLHelper
{
    internal class WebSocket
    {
        [Obsolete]
        const string clientID = "4dd97a35cf647de595b918944aa6915d"; //I'm sorry whoever I got from this
        internal static async Task DownloadBytesTo(string filePath, string url)
        {
            using (HttpClient client = new HttpClient())
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml");
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0");
                    using (var response = await client.SendAsync(request).ConfigureAwait(false))
                    {
                        using (HttpContent content = response.Content)
                        {
                            using (var stream = File.Create(filePath))
                            {
                                byte[] bytes = await content.ReadAsByteArrayAsync();
                                await stream.WriteAsync(bytes, 0, bytes.Length);
                            }
                        }
                    }
                }
            }
        }
        internal static async Task<string> GetStringFrom(string url)
        {
            string result=null;
            using (HttpClient client = new HttpClient())
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml");
                    request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0");
                    using (var response = await client.SendAsync(request).ConfigureAwait(false))
                    {
                        using (HttpContent content = response.Content)
                        {
                            result= await content.ReadAsStringAsync();
                            Console.WriteLine(result);
                        }
                    }
                }
            }
            return result;
        }
        [Obsolete]
        internal static string GetApiUrl(string originalUrl)
        {
            return "http://api.soundcloud.com/resolve.json?url=" + originalUrl + "&client_id=" + clientID;
        }
        internal static string GetOEmbedUrl(string url)
        {
            return "http://soundcloud.com/oembed?format=json&url=" + url;
        }
    }
}
