using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace SoundcloudDLHelper
{
    /// <summary>
    /// Can only search for one page, and it is prompted to be deprecated should soundcloud change their website.
    /// </summary>
    public class Search
    {
        private const string Identifier = "<li><a href=\"/search/people\">Search for People</a></li>";
        private static readonly char[] IdentifierArr = Identifier.ToCharArray();
        public const int MaxEntriesPerPage = 10;
        private static readonly Dictionary<string, string> replacementDict = new Dictionary<string, string>()
        {
            { "&quot;","\"" },
            {"&apos;","\'" },
            {"&gt;",">" },
            {"&lt;","<" },
            {"&amp;","&" },
            {"&#x27;","'" },
        };
        public static async Task<SearchEntry[]> GetSearchEntriesAsync(string input)
        {
            string path = ValidFileName(input);
            string url = "https://soundcloud.com/search?q=" + input.Replace(" ", "%20");
            await WebSocket.DownloadBytesTo(path, url);
            //By now, the program had downloaded info of Soundcloud page to path^
            using (StreamReader sr = File.OpenText(path))
            {
                SearchEntry[] entries = new SearchEntry[MaxEntriesPerPage];
                for(; ;)
                {
                    string str = await sr.ReadLineAsync();
                    if(str.Trim() == Identifier)
                    {
                        await sr.ReadLineAsync();
                        await sr.ReadLineAsync();
                        //Hard coded actions ^^^
                        //More incoming
                        for(int i =0; i< MaxEntriesPerPage;i++)
                        {
                            SearchEntry? se = GetSearchEntry((await sr.ReadLineAsync()).Trim());
                            if(!se.HasValue)
                            {
                                break;
                            }
                            entries[i] = se.Value;
                        }
                        sr.Close();
                        File.Delete(path);
                        return entries;
                    }
                }
            }
        }
        public static async Task<string> GetFirstMatchingAudio(string input)
        {
            string path = ValidFileName(input);
            string url = "https://soundcloud.com/search?q=" + input.Replace(" ", "%20");
#if DEBUG
            Console.WriteLine($"Soundcloud url: \"{url}\"");
#endif
            await WebSocket.DownloadBytesTo(path, url);
            using (StreamReader sr = File.OpenText(path))
            {
                for (; ; )
                {
                    string str = await sr.ReadLineAsync();
                    if (str.Trim() == Identifier)
                    {
                        await sr.ReadLineAsync();
                        await sr.ReadLineAsync();
                        //Hard coded actions ^^^
                        //More incoming
                        for (int i = 0; i < MaxEntriesPerPage; i++)
                        {
                            SearchEntry? se = GetSearchEntry((await sr.ReadLineAsync()).Trim());
                            if(!se.HasValue)
                            {
                                break;
                            }
                            if(se.Value.isSong)
                            {
                                sr.Close();
                                File.Delete(path);
                                return se.Value.url;
                            }
                        }
                        break;
                    }
                }
            }
            File.Delete(path);
            return null;
        }
        [Obsolete]
        public static async Task<string[]> GetMatchingUrlSerialize(string input)
        {
            string path = ValidFileName(input);
            string url = "https://soundcloud.com/search?q=" + input.Replace(" ","%20");
            await WebSocket.DownloadBytesTo(path+".txt", url);
            using (var sr = File.OpenText(path))
            {
                List<string> stringList = new List<string>(20);
                while (!sr.EndOfStream)
                {
                    string str = await sr.ReadLineAsync();
//#if DEBUG
//                    Console.WriteLine(str);
//                    Console.ReadLine();
//#endif
                    if (str.Trim() == Identifier)
                    {
//#if DEBUG
//                        Console.WriteLine("Identifier detected");
//                        Console.ReadLine();
//#endif
                        await sr.ReadLineAsync(); //</ul>
                        await sr.ReadLineAsync(); //<ul>
                        string nextLine;
                        using (var sre = File.CreateText(path + "entries.txt"))
                        {
                            while ((nextLine = (await sr.ReadLineAsync()).Trim()) != "</ul>")
                            {
                                SearchEntry se = GetSearchEntry(nextLine).Value;
                                string sestr = $"{se.title}»{se.url}";
                                await sre.WriteLineAsync(sestr);
                                stringList.Add(sestr);
                            }
                        }
                    }
                }
                return stringList.ToArray();
            }
        }
        [Obsolete]
        public static async Task<string[]> GetMatchingUrlPure(string input)
        {
            string path = ValidFileName(input);
            string url = "https://soundcloud.com/search?q=" + input.Replace(" ", "%20");
            string htmlString = await WebSocket.GetStringFrom(url);
            //TODO: fix this vvv
            int startIndex = htmlString.IndexOf(Identifier) + Identifier.Length;
            using (var sr = File.OpenText(path + "txt"))
            {
                List<string> stringList = new List<string>(20);
                while (!sr.EndOfStream)
                {
                    string str = await sr.ReadLineAsync();
                    if (str.Trim() == Identifier)
                    {
                        await sr.ReadLineAsync(); //</ul>
                        await sr.ReadLineAsync(); //<ul>
                        string nextLine;
                        using (var sre = File.CreateText(path + "entries.txt"))
                        {
                            while ((nextLine = (await sr.ReadLineAsync()).Trim()) != "</ul>")
                            {
                                SearchEntry se = GetSearchEntry(nextLine).Value;
                                string sestr = $"{se.title}»{se.url}";
                                await sre.WriteLineAsync(sestr);
                                stringList.Add(sestr);
                            }
                        }
                    }
                }
                return stringList.ToArray();
            }
        }
        private static string ValidFileName(string songTitle)
        {
            char[] songTitleArr = songTitle.ToCharArray();
            for (int i = 0; i < songTitleArr.Length; i++)
            {
                switch (songTitleArr[i])
                {
                    case '*':
                    case '.':
                    case '\"':
                    case '\\':
                    case '/':
                    case ':':
                    case ';':
                    case '=':
                    case ',':
                    case '|':
                    case '^':
                        songTitleArr[i] = '_';
                        break;
                    case '[':
                    case '<':
                        songTitleArr[i] = '(';
                        break;
                    case ']':
                    case '>':
                        songTitleArr[i] = ')';
                        break;
                }
            }
            return new string(songTitleArr);
        }
        private static SearchEntry? GetSearchEntry(string htmlLine)
        {
#if DEBUG
            Console.WriteLine(htmlLine);
#endif
            SearchEntry entry = new SearchEntry();
            for (int i = 17; i <htmlLine.Length;i++)
            {
                if(htmlLine[i] != '"')
                {
                    entry.AddToUrl(htmlLine[i].ToString());
                }
                else
                {
                    ++i;
                    for(;i<htmlLine.Length;++i)
                    {
                        if(htmlLine[i] !='<' || htmlLine[i+1] != '/' || htmlLine[i+2] != 'a' || htmlLine[i+3] != '>')
                        {
                            entry.title += htmlLine[i];
                        }
                        else
                        {
                            entry.title = CorrectTitle(entry.title);
                            return entry;
                        }
                    }
                }
            }
            return null;
        }
        private static string CorrectTitle(string input)
        {
            StringBuilder sb = new StringBuilder(input, input.Length);
            foreach(string k in replacementDict.Keys)
            {
                sb.Replace(k, replacementDict[k]);
            }
            return sb.ToString();
        }
    }
}
