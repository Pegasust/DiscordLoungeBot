using Discord.Audio;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordMusicBot.LoungeBot
{
    internal partial class LoungeBot
    {
        private TaskCompletionSource<bool> threadSafePause;
        internal bool pause
        {
            get
            {
                return internalPause;
            }
            set
            {
                new Thread(() => threadSafePause.TrySetResult(value)).Start();
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
        /// <summary>
        /// Actual pausing state
        /// </summary>
        internal async Task<bool> m_ThreadSafePauseAsync()
        {
            bool val = await threadSafePause.Task;
            threadSafePause = new TaskCompletionSource<bool>();
            return val;
        }

        private static Process DecodeUsingFFMPEG(string path)
        {
            ProcessStartInfo ffmpeg = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-xerror -i \"{path}\" -ac 2 -f s16le -ar {FrequencySample} pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true //FFMPEG throws out progress of FFMPEG in std::err
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
        /// <summary>
        /// Separate thread running.
        /// Plays from the song queue.
        /// </summary>
        async partial void MusicPlay()
        {
            for (; ; )
            {
                bool pause = await m_ThreadSafePauseAsync();
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
                            Song nextSong = songQueue.Peek();
                            await SetSongStatus(nextSong);
                            LogHelper.Logln($"Now Playing {nextSong.Title}", LogType.Info);
#if DEBUG||TRACE
                            await OutputAsync($"Now Playing {nextSong.Title}");
#endif
                            await SendAudio(nextSong.FilePath);

#if !LOUNGE_AI
                            try
                            {
                                File.Delete(nextSong.FilePath);
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
                {
                    //Maybe async programming is not good at all huh.
                }
            }
        }
        private async Task SendAudio(string path)
        {
            Process ffmpeg = DecodeUsingFFMPEG(path);
            using (Stream decodedOutput = ffmpeg.StandardOutput.BaseStream)
            {
                using (AudioOutStream audioOutput = audio.CreatePCMStream(AudioApplication.Music, voiceChannel.Bitrate))
                {
                    int bufferSize = 1024;
                    //int bytesSent = 0;
                    byte[] buffer = new byte[bufferSize];
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
                            bool pauseAgain;
                            do
                            {
                                pauseAgain = await m_ThreadSafePauseAsync();
                            } while (pauseAgain);
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
        private async Task<bool> DownloadSongAsync(Song song)
        {
            TaskCompletionSource<bool> downloaded = new TaskCompletionSource<bool>();
            new Thread(
#if DEBUG || TRACE
               async
#endif
               () =>
               {
                   LogHelper.Logln($"Downloading {song.Title} from {song.Url}.", LogType.Info);
                   Process youtubedl;
                   string args = $"-x --audio-format mp3 -o \"{song.FilePath.Replace(".mp3", ".%(ext)s")}\" {song.Url}";
#if DEBUG || TRACE
                   LogHelper.Logln($"Launching command youtube-dl {args}");
#endif
                   ProcessStartInfo downloader = new ProcessStartInfo()
                   {
                       FileName = "youtube-dl",
                       Arguments = args,
                       //set to true if silent
                       CreateNoWindow = true,
                       RedirectStandardOutput = true,
                       UseShellExecute = false,
                   };
                   youtubedl = Process.Start(downloader);
#if DEBUG || TRACE
                   LogHelper.Logln("Downloading", LogType.Debug);
                   await OutputAsync("Downloading file.");
#endif
                   youtubedl.WaitForExit();
                   if (File.Exists(song.FilePath))
                   {
                       LogHelper.Logln("Download complete.", LogType.Success);
#if DEBUG || TRACE
                       await OutputAsync("Download complete.");
#endif
                       downloaded.SetResult(true);
                   }
                   else
                   {
                       downloaded.SetResult(false);
                       string outp = $"Couldn't download song. {youtubedl.StandardOutput.ReadToEnd()}";
                       LogHelper.Logln(outp, LogType.Error);
                       await OutputAsync(outp);
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
                        LogHelper.Logln("Not able to retrieve information from audio.", LogType.Warning);
                        LogHelper.Logln("Attempting to squeeze out information.", LogType.Info);
                        //Might be a shortened URL
                        WebRequest web = WebRequest.Create(url);
                        web.Method = "HEAD";
                        string actualURL;
                        if ((actualURL = web.GetResponse().ToString()) != url)
                        {
                            url = actualURL;
                        }
                        else
                        {
                            songInfoFound.SetResult(false);
                            break; //Sooner or later
                            //:(
                        }
                    }
                } while (true);
            }).Start();
            if (await songInfoFound.Task)
            {
                return await songTask.Task;
            }
            else
            {
                LogHelper.Logln("youtube-dl failed to retrieve info.", LogType.Error);
                await OutputAsync("My apologies, I failed in retrieving info from url.");
                return Song.Null;
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
            SmartPlay,

        }
        static readonly Dictionary<string, AudioCommand> audioCmdLookup
            = new Dictionary<string, AudioCommand>
            {
                { "play", AudioCommand.SmartPlay },
                { "-p", AudioCommand.SmartPlay },
                { "skip", AudioCommand.Skip },
                { "-s", AudioCommand.Skip },
                { "pause", AudioCommand.Pause },

            };
        internal static async Task AudioServiceCommand(string[] param, bool isMainModule = false)
        {
            Action x = () => { };
            int i = arrayStartIndex - (isMainModule ? 1 : 0);
            for (; i < param.Length; i++)
            {
                AudioCommand cmd;
                if (audioCmdLookup.TryGetValue(param[i], out cmd))
                {
                    switch (cmd)
                    {
                        case AudioCommand.SmartPlay:
                            if (i + 1 >= param.Length)
                            {
                                //Should resume stream
                                Program.Bot.pause = false;
                            }
                            else
                            {
                                //User prolly wants to add more playlist
                            }
                            break;
                    }
                }
            }
        }
    }
}
