#if !ML_INITIALIZED
#define DELETE_SONGS
#endif
#if !WINDOWS && ! MAC
#define RASPBERRY_PI
#endif
#if !RASPBERRY_PI
#define ACCESS_TO_TIME_ON_SONG
#endif
using Discord.Audio;
using DiscordMusicBot.LoungeBot;
using Google.Apis.YouTube.v3.Data;
using SoundcloudDLHelper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace DiscordMusicBot.LoungeBot
{
    internal partial class LoungeBot
    {
        private Song playingSong;
        internal bool noSongPlaying = true;
#if ACCESS_TO_TIME_ON_SONG
        private DateTime startingTime;
        private TimeSpan timeBeforePause;
        internal TimeSpan timeOnSong
        {
            get
            {
                if(pause)
                {
                    return timeBeforePause;
                }
                return timeBeforePause+(DateTime.Now - startingTime);
            }
        }
#endif
        private TaskCompletionSource<bool> threadSafePause;
        internal bool pause
        {
            get
            {
                return internalPause;
            }
            set
            {
                new Thread(() => threadSafePause.SetResult(value)).Start();
                internalPause = value;
            }
        }
        private bool internalPause;
        private const string FrequencySample =
#if !WINDOWS
            "44100"
#else
            "48000"
#endif
            ;
        private const int FrequencySampleInt
            =
#if !WINDOWS
            44100
#else
            48000
#endif
            ;
        /// <summary>
        /// Actual pausing state
        /// </summary>
        internal async Task<bool> RenewPause()
        {
#if DEBUG
            LogHelper.Logln("InThreadSafePause", LogType.Debug);
#endif
            bool val = await threadSafePause.Task;
#if DEBUG
            LogHelper.Logln("Finished awaiting for value", LogType.Debug);
#endif
            threadSafePause = new TaskCompletionSource<bool>();
            return val;
        }
        internal async Task<bool> RenewPause(bool newPauseState)
        {
#if DEBUG
            LogHelper.Logln("InThreadSafePause", LogType.Debug);
#endif
            bool val = await threadSafePause.Task;
#if DEBUG
            LogHelper.Logln("Finished awaiting for value", LogType.Debug);
#endif
            threadSafePause = new TaskCompletionSource<bool>(newPauseState);
            return val;
        }

        private static Process DecodeUsingFFMPEG(string path)
        {
            ProcessStartInfo ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-xerror -i \"{path}\" -ac 2 -f s16le -ar {FrequencySample} pipe:1",
                UseShellExecute = false,
#if DEBUG
                RedirectStandardOutput = true,
#else
#endif
            };
            return Process.Start(ffmpeg);
        }
        private static Process GetFFPlay(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffplay",
                Arguments = $"-i \"{path}\" -ac 2 -f s16le -ar {FrequencySample} pipe:1 -autoexit",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
        }
        //ENTRY POINT
        async void XMusicPlay()
        {
            threadSafePause = new TaskCompletionSource<bool>();
            //This thread will wait until user puts in a song
            pause = true;
            noSongPlaying = true;
            bool pauseAgain;
            do
            {
                pauseAgain = await RenewPause();
            } while (pauseAgain);
            for (; ;)
            {
                //Enters the infinite loop after user puts a song
                while(songQueue.Count>0)
                {
                    if (!songQueue.TryDequeue(out playingSong))
                    {
                        LogHelper.Logln("Unable to dequeue songQueue!", LogType.Error);
                        this.pause = true;
                    }
                    noSongPlaying = false;
                    await SetSongStatus(playingSong);
                    string nowPlayingStr = $"Now Playing {playingSong.Title}";
                    LogHelper.Logln(nowPlayingStr, LogType.Info);
                    await OutputAsync(nowPlayingStr);
                    await SendAudio(playingSong.FilePath);
#if DELETE_SONGS
                            try
                            {
                                File.Delete(nextSong.FilePath);
                            }
                            catch
                            {
                                LogHelper.Logln($"Cannot delete file at {nextSong.FilePath}.");
                            }
                            finally
                            {
                                songQueue.Dequeue();
                            }
#endif
                }
                LogHelper.Logln("Playlist ended.", LogType.Success);
                await StatusNotPlaying();
                noSongPlaying = true;
                pause = true;
                do
                {
                    pauseAgain = await RenewPause();
                } while (pauseAgain);
            }
        }
        /// <summary>
        /// Separate thread running.
        /// Plays from the song queue.
        /// </summary>
        async partial void MusicPlay()
        {
            for (; ; )
            {
                bool pause = await RenewPause();
                try
                {
                    if (songQueue.Count == 0)
                    {
                        LogHelper.Logln("Playlist ended.", LogType.Success);
                        await StatusNotPlaying();
                    }
                    else
                    {
                        if (!pause)
                        {
                            if (!songQueue.TryDequeue(out playingSong))
                            {
                                LogHelper.Logln("Unable to peek songQueue!", LogType.Error);
                                this.pause = true;
                            }
                            await SetSongStatus(playingSong);
                            string nowPlayingStr = $"Now Playing {playingSong.Title}";
                            LogHelper.Logln(nowPlayingStr, LogType.Info);
                            await OutputAsync(nowPlayingStr);
                            await SendAudio(playingSong.FilePath);
#if DELETE_SONGS
                            try
                            {
                                File.Delete(nextSong.FilePath);
                            }
                            catch
                            {
                                LogHelper.Logln($"Cannot delete file at {nextSong.FilePath}.");
                            }
                            finally
                            {
                                songQueue.Dequeue();
                            }
#endif
                        }
                    }
                }
                catch                
                #if DEBUG
                (Exception e)
                #endif
                {
                    //Maybe async programming is not good at all huh.
#if DEBUG
                    LogHelper.Logln($"Caught exception: {e.Message}");
#endif
                }
            }
        }
        internal async Task SendAudio(string path)
        {
            Process ffmpeg = DecodeUsingFFMPEG(path);
            using (Stream decodedOutput = ffmpeg.StandardOutput.BaseStream)
            {
#if DEBUG
                LogHelper.Logln($"Channel's bitrate: {voiceChannel.Bitrate}", LogType.Info);
#endif
                using (AudioOutStream audioOutput = audio.CreatePCMStream(AudioApplication.Music, Math.Min(voiceChannel.Bitrate * 2, 128) * 1024))
                {
                    int bufferSize = 1024;
                    //int bytesSent = 0;
                    byte[] buffer = new byte[bufferSize];
#if ACCESS_TO_TIME_ON_SONG
                    timeBeforePause = TimeSpan.Zero;
                    startingTime = DateTime.Now;
#endif
                    while (true)
                    {
                        int bufferRead = await decodedOutput.ReadAsync(buffer, 0, bufferSize, disposeToken.Token);
                        if (bufferRead == 0)
                        {
                            //Song ended
                            break;
                        }
                        await audioOutput.WriteAsync(buffer, 0, bufferRead, disposeToken.Token);
                        if (pause)
                        {
#if ACCESS_TO_TIME_ON_SONG
                            timeBeforePause += DateTime.Now - startingTime;
#endif
                            LogHelper.Logln("Paused.");
#if DEBUG
                            await OutputAsync("From music thread: Paused");
#endif
                            bool pauseAgain;
                            do
                            {
                                pauseAgain = await RenewPause();
                            } while (pauseAgain);
#if ACCESS_TO_TIME_ON_SONG
                            startingTime = DateTime.Now;
#endif
                        }
                        //bytesSent += bufferRead;
                    }
                }
                LogHelper.Logln("AudioOutStream Flushed!", LogType.Success);
            }
        }
        /// <summary>
        /// Returns the full path of the downloaded audio file.
        /// </summary>
        /// <param name="song">I WILL CRASH IF YOU PASS IN Song.Null</param>
        /// <returns></returns>
        internal async Task<bool> DownloadSongAsync(Song song)
        {
            TaskCompletionSource<bool> downloaded = new TaskCompletionSource<bool>();
            new Thread(
               () =>
               {
                   LogHelper.Logln($"Downloading {song.Title} from {song.Url}.", LogType.Info);
                   Process youtubedl;
                   string args = $"-x --audio-format mp3 --audio-quality 0 " +
#if DEBUG
                   "--console-title "+
#endif
                   $"-o \"{song.FilePath.Replace(".mp3", ".%(ext)s")}\" {song.Url}";
#if DEBUG || TRACE
                   LogHelper.Logln($"Launching command youtube-dl {args}");
#endif
                   ProcessStartInfo downloader = new ProcessStartInfo()
                   {
                       FileName = "youtube-dl",
                       Arguments = args,
                       //set to true if silent
                       CreateNoWindow = true,
                       RedirectStandardError = true,
                       UseShellExecute = false,
                   };
                   youtubedl = Process.Start(downloader);
#if DEBUG || TRACE
                   LogHelper.Logln("Downloading", LogType.Debug);
#endif
                   youtubedl.WaitForExit();
                   if (File.Exists(song.FilePath))
                   {
                       LogHelper.Logln("Download complete.", LogType.Success);
                       downloaded.SetResult(true);
                   }
                   else
                   {
                       downloaded.SetResult(false);
                       string outp = $"Couldn't download song. {youtubedl.StandardOutput.ReadToEnd()}";
                       LogHelper.Logln(outp, LogType.Error);
                   }
               }).Start();
            return await downloaded.Task;
        }
        /// <summary>
        /// Returns Song.Null if info not found
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        internal async Task<Song> GetSongFromURLAsync(string url)
        {
            string responseUrl;
            if (IsShortenedUrl(url, out responseUrl))
            {
                url = responseUrl;
                await OutputAsync("Detected a shortened URL. Your location URL is " + url);
            }
            TaskCompletionSource<Song> songTask = new TaskCompletionSource<Song>();
            TaskCompletionSource<bool> songInfoFound = new TaskCompletionSource<bool>();
#if DEBUG || TRACE
            LogHelper.Logln("Getting song info from url", LogType.Debug);
#endif
            new Thread(() =>
            {
                Process youtubedl;
                do
                {
                    //CUSTOMIZABLE
                    string args = $"-s -e --get-duration {url}";//-s: don't download, -e: get title
#if DEBUG || TRACE
                    LogHelper.Logln($"Launching command youtube-dl {args}");
#endif
                    ProcessStartInfo youtubeGetInfo = new ProcessStartInfo()
                    {
                        FileName = "youtube-dl",
                        Arguments = args,
                        RedirectStandardOutput = true,
                        //If silient, set this to true
                        CreateNoWindow = false,
                        UseShellExecute = false,
                    };
                    youtubedl = Process.Start(youtubeGetInfo);
#if DEBUG || TRACE
                    LogHelper.Logln("youtube-dl started.", LogType.Debug);
#endif
                    youtubedl.WaitForExit();
                    LogHelper.Logln("youtube-dl ended.", LogType.Success);
                    string[] output = youtubedl.StandardOutput.ReadToEnd().Split('\n');
                    //Line:
                    // 0: title
                    // 1: duration
                    if (output.Length >= 2)
                    {
                        songTask.SetResult(new Song(
                            path: Path.GetFullPath($"{ValidFileName(output[0])}.mp3"),
                            title: output[0],
                            duration: output[1],
                            url: url));
                        songInfoFound.SetResult(true);
                        break; //sooner or later
                    }
                    else
                    {
                        songInfoFound.SetResult(false);
                        break;
                    }
                } while (true);
            }).Start();
            if (await songInfoFound.Task)
            {
#if DEBUG
                LogHelper.Logln("Finished awaiting for songInfoFound.Task.", LogType.Debug);
#endif
                return await songTask.Task;
            }
            else
            {
                LogHelper.Logln("youtube-dl failed to retrieve info.", LogType.Error);
                //await OutputAsync("My apologies, I failed in retrieving info from url.");
                return Song.Null;
            }
        }

        private string ValidFileName(string songTitle)
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
            return Path.Join(loungeSongs.SongCollectionPath, new string(songTitleArr));
        }
        private static bool IsShortenedUrl(string url, out string responseUrl)
        {
#if DEBUG
            LogHelper.Logln($"Trying to find out if \"{url}\" is a shortened url.", LogType.Debug);
#endif
            bool result = false;
            responseUrl = null;
            try
            {
                HttpWebRequest web = (HttpWebRequest)WebRequest.Create(url);
                web.AllowAutoRedirect = false;
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)web.GetResponse())
                    {
                        responseUrl = response.Headers["Location"];
                        result = !string.IsNullOrWhiteSpace(responseUrl);
                    }
                }
                catch (WebException)
                {
                    return false;
                }
                catch (HttpRequestException)
                {
                    return false;
                }
            }
            catch (UriFormatException e)
            {
                LogHelper.Logln($"\"{url}\" might not be a url!", LogType.Warning);
            }
            return result;
        }
    }
}
namespace DiscordMusicBot.Commands
{
    internal static partial class CommandService
    {
        enum AudioCommand
        {
            Pause,
            Skip,
            Playsong,
            Resume,
            SmartPlay, //Consists of play and resume
            DownloadSong,
            ShowQueue,
            LazyPlay,
        }
        enum PlaySong
        {
            OnlineSearch,
            OfflineSearch,
            OfflinePath,
        }
        /*
         * There is a specific difference between a field (int a = 0); and a property
         * (int a { get {return...}; set {...};})
         * A field marked readonly would be cached into memory (RAM),
         * while only get property is defined at compile time as a result of
         * a specific calculation (no caching to memory).
         * Hence, for the below code, I use readonly field to make it faster
         * if it's ran by a high-end machine,
         * and for the worse ones (raspberry pi in this case), 
         * I use property to make it hogs less memory.
         */
        static
#if !RASPBERRY_PI
            readonly
#endif
            Dictionary<string, AudioCommand> audioCmdLookup
#if RASPBERRY_PI
        {
            get
            =>
#else
            =
#endif
             new Dictionary<string, AudioCommand>
            {
                { "play", AudioCommand.SmartPlay },
                { "-p", AudioCommand.Playsong },
                { "resume", AudioCommand.Resume },
                { "-r", AudioCommand.Resume },
                { "skip", AudioCommand.Skip },
                { "-s", AudioCommand.Skip },
                { "pause", AudioCommand.Pause },
                { "download", AudioCommand.DownloadSong },//Download song without play
                { "queue", AudioCommand.ShowQueue },
                 {"-q", AudioCommand.ShowQueue },
                 { "xplay", AudioCommand.LazyPlay }
            };
#if RASPBERRY_PI        
        }
#endif
        static
#if !RASPBERRY_PI
            readonly
#endif
            Dictionary<string, PlaySong> playsongLookup
#if RASPBERRY_PI
            {
                get =>
#else
            =
#endif
            new Dictionary<string, PlaySong>
            {
                {"-onl", PlaySong.OnlineSearch },
                {"-offl", PlaySong.OfflineSearch },
                {"-offp", PlaySong.OfflinePath },
            };
#if RASPBERRY_PI
            }
#endif
        internal static async Task AudioServiceCommand(string[] param, bool isMainModule = false)
        {
            int i = arrayStartIndex - (isMainModule ? 1 : 0);
            for (; i < param.Length; i++)
            {
                AudioCommand cmd;
#if RASPBERRY_PI
                Dictionary<string, AudioCommand> audioCmdLookup = CommandService.audioCmdLookup;
#endif
                if (audioCmdLookup.TryGetValue(param[i].ToLower(), out cmd))
                {
                    switch (cmd)
                    {
                        case AudioCommand.LazyPlay:
#if DEBUG
                            LogHelper.Logln("This command is a one-liner play. Hence, no multi-command.", LogType.Warning);
#endif
                            //TODO: Implement this
                            if (i + 1 < param.Length)
                            {
                                await LazyPlay(param, i + 1);
                            }
                            else
                            {
                                await ResumeStream();
                            }
                            break;
                        case AudioCommand.SmartPlay:
                            if (i + 1 >= param.Length)
                            {
                                //Should resume stream
                                await ResumeStream();
                            }
                            else
                            {
                                //Playsong command
                                try
                                {
                                    i = await PlaysongCommand(param, i);
                                }
                                catch (Exception e)
                                {
                                    LogHelper.Logln($"{e.Message} at {e.StackTrace}", LogType.Error);
                                }
                                if (i == int.MinValue)
                                {
                                    return;
                                }
                            }
                            break;
                        case AudioCommand.Resume:
                            await ResumeStream();
                            break;
                        case AudioCommand.Playsong:
                            try
                            {
                                i = await PlaysongCommand(param, i);
                            }
                            catch (Exception e)
                            {
                                LogHelper.Logln($"{e.Message} at {e.StackTrace}", LogType.Error);
                            }
                            if (i == int.MinValue)
                            {
#if DEBUG
                                LogHelper.Logln("User wants to escape.", LogType.Debug);
#endif
                                return;
                            }
                            break;
                        case AudioCommand.Skip:
                            //TODO: Add skip
                            LogHelper.Logln("Not implemented the skip function.", LogType.Warning);
                            await Program.Bot.OutputAsync("Not implemented.");
                            break;
                        case AudioCommand.Pause:
                            await PauseStream();
                            break;
                        case AudioCommand.DownloadSong:
                            //TODO: Add this ability please
                            break;
                        case AudioCommand.ShowQueue:
                            if(Program.Bot.noSongPlaying)
                            {
                                await Program.Bot.OutputAsync("It's empty in here. Add some songs :)");
                            }
                            else
                            {
                                Song nowPlaying;
                                Song[] queueArray = Program.Bot.queueArray(out nowPlaying);
                                string txt = $"Now playing: **{nowPlaying.Title}** (" +
#if ACCESS_TO_TIME_ON_SONG
                                (Program.Bot.timeOnSong.Hours > 0 ? Program.Bot.timeOnSong.Hours + ":" : "")
                                    + (Program.Bot.timeOnSong.ToString(@"mm\:ss")) + "/" +
#endif
                                $"{nowPlaying.Duration})\n";
                                for (int k = 0; k < queueArray.Length; k++)
                                {
                                    txt += $"{k + 1}. {queueArray[k].Title} ({queueArray[k].Duration})\n";
                                }
                                Discord.EmbedBuilder builder = new Discord.EmbedBuilder()
                                {
                                    Title = "Queue",
                                    Color = Discord.Color.DarkMagenta,
                                    Description = txt
                                };
                                await Program.Bot.OutputAsync("Showing playing queue", builder.Build());
                            }                            
                            break;
                        default:
                            LogHelper.Logln("Unknown parameter: " + param[i], LogType.Warning);
                            await Program.Bot.OutputAsync($"Unknown parameter: {param[i]}.");
                            break;
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="param"></param>
        /// <param name="i"></param>
        /// <returns>minvalue if user prompts to escape</returns>
        private static async Task<int> PlaysongCommand(string[] param, int i)
        {
            ++i;
            string nextParam = param[i];
#if DEBUG
            LogHelper.Logln($"Identifying if {nextParam} is an absolute url.", LogType.Debug);
#endif
            //If parameter is a url
            if (Uri.IsWellFormedUriString(nextParam, UriKind.Absolute))
            {
#if DEBUG
                LogHelper.Logln($"{nextParam} is an absoluteURL.", LogType.Debug);
#endif
                if(!await TryEnqueue(nextParam))
                {
                    return int.MinValue;
                }
            }
            else
            {
                PlaySong cmd;
                if (playsongLookup.TryGetValue(nextParam.ToLower(), out cmd))
                {
                    if (++i == param.Length)
                    {
                        return int.MinValue;
                    }
                    string identifier = param[i];
                    OfflineSongInfo? songInfo = null;
                    switch (cmd)
                    {
                        case PlaySong.OfflinePath:
                            songInfo =
                                await
                                Program.Bot.loungeSongs.songCollection.GetSongByFilePathAsync(identifier);
                            break;
                        case PlaySong.OfflineSearch:
                            songInfo = await
                                Program.Bot.loungeSongs.songCollection.GetSongByTitleAsync(identifier);
                            break;
                        case PlaySong.OnlineSearch:
#if DEBUG
                            LogHelper.Logln($"Running search on keyword \"{identifier}\".", LogType.Debug);
#endif
                            //Search on youtube. If unavailable, search on soundcloud
#region //_TODO: REFACTOR PLEASE ;=;
                            if(!await TryEnqueueOnlineSong(identifier))
                            {
                                return int.MinValue;
                            }
#endregion
                            break;
                        default:
                            return --i;
                    }
                    if (songInfo.HasValue)
                    {
                        await Program.Bot.loungeSongs.songCollection.TryChangeTimesPlayed
                            (songInfo.Value.lineNumber, new OfflineSong(songInfo.Value.offSong.song, songInfo.Value.offSong.timesPlayed + 1));

                        await Program.Bot.AddSongToQueueAsync(songInfo.Value.offSong.song);
                        LogHelper.Logln($"Added song \"{songInfo.Value.offSong.song.Title}\" to queue.", LogType.Success);
                        await Program.Bot.OutputAsync("Added " + songInfo.Value.offSong.song.Title + ".");
                        if(Program.Bot.pause)
                        {
                            Program.Bot.pause = false;
                        }
                    }
                }
                else
                {
                    --i;
                }
            }
            return i;
        }
        enum SearchSite
        {
            YT = 1,
            SC = 2,
            Both = YT|SC,
            Default = 0,
        }
        private static async Task<bool> TryEnqueueOnlineSong(string query)
        {
            string url = await YoutubeHelper.YoutubeSearch.GetFirstMatchingVideo
                                (query);
            if (string.IsNullOrEmpty(url))
            {
                LogHelper.Logln("No matching search result found!", LogType.Error);
                await Program.Bot.OutputAsync("No matching search available from youtube. Searching in Soundcloud.");
                SearchEntry[] se = await Search.GetSearchEntriesAsync(query);
                for (int j = 0; j < se.Length; j++)
                {
                    if (se[j].isSong)
                    {
                        if (!await TryEnqueue(se[j].url))
                        {
                            return false;
                        }
                        LogHelper.Logln("Found result!", LogType.Success);
                        break;
                    }
                    else if (j == se.Length - 1)
                    {
                        LogHelper.Logln("No matching search result found!", LogType.Error);
                        await Program.Bot.OutputAsync("Cannot find it on soundcloud either. Try playing with url.");
                    }
                }
            }
            else
            {
                LogHelper.Logln($"Matching Url: {url}", LogType.Success);
                if (!await TryEnqueue(url))
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// If there is "soundcloud" or "youtube", search on the webpage.
        /// If sc/ or yt/ present as the first chars of the first param, search on that site
        /// 
        /// </summary>
        /// <param name="param"></param>
        /// <param name="i">index in which this command starts</param>
        /// <returns></returns>
        private static async Task LazyPlay(string[] param, int i)
        {
            string properUrl = null;
            SearchSite site;
            string query;
            if(param[i].Length>=3) //If first chars might contain sc/ or yt/
            {
                query = param[i].Substring(0, 3);
                //_TODO: We can optimize it by ourselves by making the cases const string
                //and check for each char.
                switch(query.ToLower())
                {
                    case "sc/":
                        query = param[i].Substring(3);
                        site = SearchSite.SC;
                        break;
                    case "yt/":
                        query = param[i].Substring(3);
                        site = SearchSite.YT;
                        break;
                    default:
                        query = param[i];
                        site = SearchSite.Default;
                        break;
                }
                i += 1;
                query +=" "+string.Join(' ', param, i, param.Length - i);
            }
            else
            {
                site = SearchSite.Default;
                query = "";
                for(;i<param.Length;i++)
                {
                    if(site != SearchSite.Both)
                    {
                        switch (param[i].ToLower())
                        {
                            case "youtube":
                                site |= SearchSite.YT;
                                break;
                            case "soundcloud":
                                site |= SearchSite.SC;
                                break;
                        }
                    }
                    query += param[i];
                }
            }
            //Query and site should now be assigned correctly^^^
            switch(site)
            {
                case SearchSite.YT:
                    properUrl = await YoutubeHelper.YoutubeSearch.GetFirstMatchingVideo(query);
                    break;
                case SearchSite.SC:
                    properUrl = await SoundcloudDLHelper.Search.GetFirstMatchingAudio(query);
                    break;
                case SearchSite.Both:
                    //Prepare to output a message to user
                    Discord.EmbedBuilder builder = new Discord.EmbedBuilder()
                    {
                        Title = "Song List",
                        Color = Discord.Color.Purple
                    };
                    YoutubeHelper.SearchEntry[] ytEntries =
                        await YoutubeHelper.YoutubeSearch.GetEntries(query, maxResult: 10);
                    AddSearchEntriesToEmbed(ref builder, "Youtube", ytEntries);

                    SoundcloudDLHelper.SearchEntry[] scEntries =
                        await SoundcloudDLHelper.Search.GetSearchEntriesAsync(query);
                    AddSearchEntriesToEmbed(ref builder, "Soundcloud", scEntries);
                    //Prompting starts
                    await Program.Bot.OutputAsync($"Please address [yt|sc]:[index] to point to the song you want to play.", builder.Build());
                    //maxResult = 10
                    await AwaitForUserInput(GetAcceptableOutput());
                    if(!Program.Bot.botPrompt.output.HasValue)
                    {
                        return;
                    }
                    if(Program.Bot.botPrompt.output.Value%2 == 0)
                    {
                        //If yt
                        properUrl = ytEntries[Program.Bot.botPrompt.output.Value / 2].url;
                    }
                    else
                    {
                        //if sc
                        properUrl = scEntries[Program.Bot.botPrompt.output.Value / 2].url;
                    }
                    break;
                case SearchSite.Default:
                    if(!await TryEnqueueOnlineSong(query))
                    {
                        LogHelper.Logln("Cannot find the corresponding item", LogType.Error);
                        await Program.Bot.OutputAsync($"Unable to find ``{query}``");
                    }
                    return;
            }
            if(!await TryEnqueue(properUrl))
            {
                LogHelper.Logln($"Unable to enqueue {properUrl}");
                await Program.Bot.OutputAsync($"``{query}`` not found.");
            }
        }
        private static Dictionary<string,sbyte> GetAcceptableOutput(sbyte maxResult=10)
        {
            Dictionary<string, sbyte> dict = new Dictionary<string, sbyte>(maxResult*2);
            for(sbyte i =0;i<maxResult;i++)
            {
                dict[$"yt:{i}"] = (sbyte) (2*i);
                dict[$"sc:{i}"] = (sbyte) (2*i + 1);
            }
            return dict;
        }
        private static async Task AwaitForUserInput(Dictionary<string,sbyte> acceptableDict)
        {
            Program.Bot.botPrompt.prompting = true;
            Program.Bot.botPrompt.acceptedDict = acceptableDict;
            do
            {
                await Task.Delay(1000);
            } while (await Program.Bot.botPrompt.getPromptingState());
        }
        private static void AddSearchEntriesToEmbed(ref Discord.EmbedBuilder builder, string site, SoundcloudDLHelper.SearchEntry[] scEntry)
        {
            string field = "";
            for (int i = 0; i < scEntry.Length; i++)
            {
                field += $"**{i}.**{scEntry[i].title}\n";
            }
            builder.AddField($"**{site}**", field);
        }
        private static void AddSearchEntriesToEmbed(ref Discord.EmbedBuilder builder, string site, YoutubeHelper.SearchEntry[] ytEntry)
        {
            string field = "";
            for (int i = 0; i < ytEntry.Length; i++)
            {
                field += $"**{i}.**{ytEntry[i].title}\n";
            }
            builder.AddField($"**{site}**", field);
        }
        private static async Task ResumeStream()
        {
            Program.Bot.pause = false;
            LogHelper.Logln("Playback resumed.", LogType.Info);
            await Program.Bot.OutputAsync("Playback resumed.");
        }
        private static async Task PauseStream()
        {
            Program.Bot.pause = true;
            LogHelper.Logln("Playback paused.", LogType.Info);
            await Program.Bot.OutputAsync("Playback paused");
        }
        //private static async Task SongEnqueue(string url)
        //{
        //    Song song = await Program.Bot.GetSongFromURLAsync(url);
        //    bool downloadedSong = await Program.Bot.DownloadSongAsync(song);
        //    if (downloadedSong)
        //    {
        //        Program.Bot.AddSongToQueue(song);
        //        await Program.Bot.loungeSongs.songCollection.TryAddSongAsync(new OfflineSong(song));
        //        LogHelper.Logln($"Added song \"{song.Title}\" to queue.", LogType.Success);
        //        await Program.Bot.OutputAsync("Added " + song.Title + ".");
        //        //TODO: Implement lounge service
        //    }
        //}
        private static async Task DownloadAndEnqueueNewSong(Song song)
        {
            bool downloadedSong = await Program.Bot.DownloadSongAsync(song);
            if (downloadedSong)
            {
                Program.Bot.AddSongToQueue(song);
                if(!await Program.Bot.loungeSongs.songCollection.TryAddSongAsync(new OfflineSong(song)))
                {
                    LogHelper.Logln($"Cannot add {song.Title} to offline list! There might exists one identical song in the song list!", LogType.Warning);
                }
                LogHelper.Logln($"Added song \"{song.Title}\" to queue.", LogType.Success);
                await Program.Bot.OutputAsync("Added " + song.Title + ".");
                //TODO: Implement lounge service
            }
            else
            {
                LogHelper.Logln($"Error downloading \"{song.Url}\"", LogType.Error);
                await Program.Bot.OutputAsync($"Unable to download {song.Title} at {song.Url}");
            }
        }
        /// <summary>
        /// Checks if there is a url on offline, if not, try add a new song to collection
        /// </summary>
        /// <param name="url"></param>
        /// <returns>Task completed</returns>
        private static async Task<bool> TryEnqueue(string url)
        {
#if DEBUG
            LogHelper.Logln("In SongEnqueue(string url)", LogType.Debug);
#endif
            OfflineSongInfo? song = await Program.Bot.loungeSongs.songCollection.GetSongByURLAsync(url);
#if DEBUG
            string stringValue = song.HasValue ? (SongCollectionFormatter.SongFormatter(song.Value.offSong) +", "+ song.Value.lineNumber):"null";
            LogHelper.Logln($"OfflineSongInfo? song = {stringValue}", LogType.Debug);
#endif
            Song comparer = await Program.Bot.GetSongFromURLAsync(url);
#if DEBUG
            LogHelper.Logln($"Song comparer = {comparer.Title}»{comparer.Duration}»{comparer.FilePath}»{comparer.Url}", LogType.Debug);
#endif
            if (song.HasValue)
            {
                if (song.Value.offSong == comparer)
                {
#if DEBUG
                    LogHelper.Logln($"comparer == song.Value.offSong. No need to download.", LogType.Debug);
#endif
                    //No need to download
                    //Add to Queue
                    await Program.Bot.AddSongToQueueAsync(song.Value.offSong.song);
                    //Change times played
                    await Program.Bot.loungeSongs.songCollection.TryChangeTimesPlayed(
                        song.Value.lineNumber, 
                        new OfflineSong(song.Value.offSong.song, song.Value.offSong.timesPlayed + 1));
                    LogHelper.Logln($"Added song \"{comparer.Title}\" to queue.", LogType.Success);
                    await Program.Bot.OutputAsync("Added " + comparer.Title + ".");
                }
                else
                {
                    await Program.Bot.OutputAsync(
                        $"The URL exists two audios. Do you want to overwrite {song.Value.offSong} with {comparer}? (~y/~n/~esc)");
                    Program.Bot.botPrompt.prompting = true;
                    await AwaitForUserInput(acceptableDict: new Dictionary<string, sbyte>()
                    {
                        { "~y",0 }, {"~n", 1 }
                    });
                    if(!Program.Bot.botPrompt.output.HasValue)
                    {
                        return true;
                    }
                    switch (Program.Bot.botPrompt.output.Value)
                    {
                        //_TODO: Fix these if needed
                        case 0: //If yes
                            await Program.Bot.loungeSongs.songCollection.
                                TryReplaceSongAsync(new OfflineSong(comparer),
                                song.Value.lineNumber);
                            if (await Program.Bot.DownloadSongAsync(comparer))
                            {
                                await Program.Bot.AddSongToQueueAsync(comparer);
                                LogHelper.Logln($"Added song \"{comparer.Title}\" to queue.", LogType.Success);
                                await Program.Bot.OutputAsync("Added " + comparer.Title + ".");
                            }
                            break;
                        case 1: //If no
                            await Program.Bot.AddSongToQueueAsync(song.Value.offSong.song);
                            await Program.Bot.loungeSongs.songCollection.TryChangeTimesPlayed
                                (song.Value.lineNumber, 
                                new OfflineSong(song.Value.offSong.song, song.Value.offSong.timesPlayed + 1));
                            LogHelper.Logln($"Added song \"{comparer.Title}\" to queue.", LogType.Success);
                            await Program.Bot.OutputAsync("Added " + comparer.Title + ".");
                            break;
                        default:
                            //Don't worry, this will never occur
                            return false;
                    }
                }
            }
            else
            {
                //If it is not available offline
                await DownloadAndEnqueueNewSong(comparer);
            }
            if(Program.Bot.pause)
            {
#if DEBUG
                LogHelper.Logln("Since music is paused, resume playback.");
#endif
                Program.Bot.pause = false;
            }
            return true;
        }
    }
}
