#if !ML_INITIALIZED
#define DELETE_SONGS
#endif
#if !WINDOWS && ! MAC
#define RASPBERRY_PI
#endif
#if !RASPBERRY_PI
#define ACCESS_TO_TIME_ON_SONG
#endif
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DiscordMusicBot.Commands;
using LOutput = DiscordMusicBot.LoungeBot.LogHelper;
using System.Collections.Concurrent;

namespace DiscordMusicBot.LoungeBot
{
    [Serializable]
    internal struct Song
    {
        internal readonly string FilePath;
        internal readonly string Title;
        internal readonly string Duration;
        internal readonly string Url;
        internal const string nullDuration = "»";
        internal const string offlineUrl = "»";
        internal Song(string path, string title, string duration, string url)
        {
            FilePath = path;
            Title = title;
            Duration = duration;
            Url = url;
        }
        internal static readonly Song Null =
            new Song("", "", nullDuration, "");
        internal bool isNull
        {
            get
            {
                return Duration == nullDuration;
            }
        }
        internal bool isOffline
        {
            get
            {
                return Url == offlineUrl;
            }
        }
    }
    internal class BotPrompt
    {
        internal Dictionary<string, sbyte> acceptedDict;
        internal sbyte? output = null;
        private TaskCompletionSource<bool> botPromptedReply;
        internal bool prompting
        {
            get
            {
                return internalPrompt;
            }
            set
            {
                new Thread(() => botPromptedReply.SetResult(value)).Start();
                internalPrompt = value;
            }
        }
        internal async Task<bool> getPromptingState()
        {
            bool val = await botPromptedReply.Task;
            botPromptedReply = new TaskCompletionSource<bool>();
            return val;
        }
        private bool internalPrompt;
        internal BotPrompt()
        {
            botPromptedReply = new TaskCompletionSource<bool>(false);
            internalPrompt = false;
        }
    }
    internal partial class LoungeBot : IDisposable
    {
        const string isCommandSuffix = "<!>";
        private DiscordSocketClient client;
        private DiscordSocketConfig dsconfig = new DiscordSocketConfig
        {
            LogLevel =
#if RASPBERRY_PI
            LogSeverity.Warning
#else
            LogSeverity.Verbose
#endif
            ,
            ConnectionTimeout =
#if RASPBERRY_PI
            30000
#else
            10000
#endif
            ,
            HandlerTimeout =
#if RASPBERRY_PI
            null
#else
            null
#endif
            
            ,MessageCacheSize =
#if RASPBERRY_PI
            50
#else
            300
#endif

        };
        private IVoiceChannel voiceChannel;
        private ITextChannel textChannel;
        private CancellationTokenSource disposeToken;
        private IAudioClient audio;
        public bool IsDisposed;
        internal BotPrompt botPrompt;
        internal Func<Task> WhileOnline;
        internal bool promptedToShowAwaitingQueue = false;
        #region Audio Service
        private ConcurrentQueue<Song> songQueue;
        internal Song[] queueArray(out Song nowPlaying)
        {
            nowPlaying = playingSong;
            return songQueue.ToArray();
        }
        internal LoungeSongs loungeSongs;
        #endregion
        internal LoungeBot()
        {
            Init();
        }
        protected virtual async void Init()
        {
            //Initialize values
            //ClientConfig automatically handles first time launch
            songQueue = new ConcurrentQueue<Song>();
            disposeToken = new CancellationTokenSource();
            botPrompt = new BotPrompt();
            //Initialize client
            client = new DiscordSocketClient(dsconfig);
            client.Log += OnClientLog;
            client.Disconnected += Disconnected;
            client.Connected += Connected;
            client.Ready += OnClientReady;
            client.MessageReceived += OnMessageReceived;
            WhileOnline += CommandService.AwaitUser;

            LOutput.Logln("Client is starting.", LogType.Success);
            await Connect();

            //Threaded
            loungeSongs = new LoungeSongs("LoungeSongs","songinf");
            new Thread(XMusicPlay).Start();
            //Handle Disconnection
            try
            {
                while (!disposeToken.IsCancellationRequested)
                {
                    //Main function
                    await MainThreadLoop();
                }
            }
            catch (TaskCanceledException)
            {
                pause = true;
            }
        }

        private async Task MainThreadLoop()
        {
            ConnectionState state = client.ConnectionState;
            const int MillisecondsDelay = 2000;
            if (state == ConnectionState.Disconnected)
            {
                await Task.Delay(MillisecondsDelay, disposeToken.Token);
                if (state == ConnectionState.Disconnected)
                {
                    await Connect();
                }
            }
            else
            {
                await WhileOnline();
            }
            await Task.Delay(MillisecondsDelay, disposeToken.Token);
        }

        private async Task Connect()
        {
            await client.StartAsync();
            await client.LoginAsync(TokenType.Bot, ClientConfig.token);
        }
        #region Disposal
        public void Dispose()
        {
            IsDisposed = true;
            disposeToken.Cancel();
            LOutput.Logln("Shutting down.", LogType.Warning);
#if DELETE_SONGS
            LOutput.Logln("Deleting songs in queue.", LogType.Warning);
            new Thread(DeleteAllSongsInQueue).Start();
            LOutput.Logln("All songs deleted successfully.", LogType.Success);
#endif
            LOutput.Logln("Disposing LoungeBot.", LogType.Warning);
            DisposeAsync().GetAwaiter().GetResult();
        }
        private void DeleteAllSongsInQueue()
        {
            for (int i = 0; i < songQueue.Count; i++)
            {
                Song song;
                if(!songQueue.TryDequeue(out song))
                {
                    LOutput.Logln("Unable to dequeue from songQueue!", LogType.Error);
                    return;
                }
                try
                {
                    File.Delete(song.FilePath);
                }
                catch
                {
                    LOutput.Logln("Song " + song.Title + " is not found in " + song.FilePath + ".", LogType.Warning);
                }
            }
        }
        private async Task DisposeAsync()
        {
            try
            {
                await audio.StopAsync();
                await client.StopAsync();
                await client.LogoutAsync();
            }
            catch
            {
            }
            audio.Dispose();
            client.Dispose();
        }
        #endregion
        #region EVENT: USERS SENT MESSAGE
        internal Task OnMessageReceived(SocketMessage sMsg)
        {
            CheckForCommand(sMsg);
            return Task.CompletedTask;
        }
        protected virtual async void CheckForCommand(SocketMessage sMsg)
        {
            if (sMsg.Author.Id == client.CurrentUser.Id
                //It's my own msg, nvm
                || sMsg.Content.Length == 0
                || string.IsNullOrWhiteSpace(sMsg.Content))
            {
                return;
            }
            string identifier = sMsg.Author.IsBot ? "[BOT]" : "";
            LOutput.Log($"{identifier} [{sMsg.Author}]: {sMsg.Content}");
            int startIndex = await CommandServiceHelper.CommandSignCheck(sMsg.Content);
            if (startIndex == CommandServiceHelper.NOT_COMMAND)
            {
                //Not command
                LOutput.Log("\n");
                return;
            }
            #region IsCommand
            LOutput.Logln(" " + isCommandSuffix);
            await ExecuteUserInput(sMsg, startIndex);
            #endregion
        }

        private async Task ExecuteUserInput(SocketMessage sMsg, int startIndex)
        {
            if (botPrompt.prompting)
            {
                sbyte outp;
                if (botPrompt.acceptedDict.TryGetValue(sMsg.Content, out outp))
                {
                    botPrompt.output = outp;
                    botPrompt.prompting = false;
                }
                else if (sMsg.Content == "~esc")
                {
                    botPrompt.output = null;
                    botPrompt.prompting = false;
                }
                else
                {
                    string acceptableValues = "[";
                    int c = 0;
                    foreach (string s in botPrompt.acceptedDict.Keys)
                    {
                        if (++c == botPrompt.acceptedDict.Keys.Count)
                        {
                            acceptableValues += s + "]";
                        }
                        else
                        {
                            acceptableValues += s + "|";
                        }
                    }
                    await OutputAsync($"Please output {acceptableValues} to finish the prompt or \"~esc\" to abort the process.");
                }
            }
            else
            {
                await CommandService.ExecuteAsync(sMsg.Content, sMsg, startIndex);
            }
        }
        #endregion
        #region EVENT: CLIENT LOG
        internal Task OnClientLog(LogMessage msg)
        {
            string strMsg = $"[{msg.Source}] {msg.Message}";
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    LOutput.Logln(strMsg, LogType.Error);
                    break;
                case LogSeverity.Debug:
                case LogSeverity.Verbose:
                    LOutput.Logln(strMsg, LogType.Debug);
                    break;
                case LogSeverity.Warning:
                    LOutput.Logln(strMsg, LogType.Warning);
                    break;
                case LogSeverity.Info:
                    LOutput.Logln(strMsg, LogType.Info);
                    break;
                default:
                    break;
            }
            return Task.CompletedTask;
        }
        #endregion
        #region EVENT: DISCONNECTED
        internal Task Disconnected(Exception arg)
        {
            LOutput.Logln($"Connection lost! {arg.Message}", LogType.Warning);
            return Task.CompletedTask;
        }
        #endregion
        #region EVENT: CONNECTED
        internal Task Connected()
        {
            LOutput.Logln("Connected.", LogType.Success);
            return Task.CompletedTask;
        }
        #endregion
        #region EVENT: CLIENT READY
        internal async Task OnClientReady()
        {
            LOutput.Logln("Client is ready!", LogType.Success);
            await StatusNotPlaying();
            SocketGuild connectingGuild = null;
            foreach (SocketGuild guild in client.Guilds)
            {
                if (guild.Name == ClientConfig.serverName.value)
                {
                    LOutput.LogTemp("[x]");
                    connectingGuild = guild;
                }
                else
                {
                    LOutput.LogTemp("[ ]");
                }
                LOutput.ChangeTempMsg(" " + guild.Name, EditMethod.AddSuffix);
                LOutput.SendTempMsg();
            }
            if (connectingGuild == null)
            {
                LOutput.Logln($"Cannot find Guild {ClientConfig.serverName.value}!", LogType.Warning);
                return;
            }
            foreach (ITextChannel t in connectingGuild.TextChannels)
            {
                if (t.Name == ClientConfig.textChannel.value)
                {
                    textChannel = t;
                    LOutput.Logln($"Main text channel output is {ClientConfig.textChannel.value}.", LogType.Success);
                    break;
                }
            }
            if (textChannel is null)
            {
                LOutput.Logln($"Cannot find text channel \"{ClientConfig.textChannel.value}" +
                    $"\" in guild/server \"{ClientConfig.serverName.value}\"", LogType.Warning);
            }
            foreach (IVoiceChannel v in connectingGuild.VoiceChannels)
            {
                if (v.Name == ClientConfig.voiceChannel.value)
                {
                    voiceChannel = v;
                    LOutput.Logln($"Connecting to {ClientConfig.voiceChannel.value}.", LogType.Info);
                    audio = await voiceChannel.ConnectAsync();
                    LOutput.Logln($"Connected to {ClientConfig.voiceChannel.value}.", LogType.Success);
                    break;
                }
            }
            if (voiceChannel is null)
            {
                LOutput.Logln($"Cannot find voice channel \"{ClientConfig.voiceChannel.value}\"" +
                    $"in guild/server \"{ClientConfig.serverName.value}\"", LogType.Warning);
            }
        }
        #endregion

        #region HELPERS
        private async Task SetSongStatus(Song song)
        {
            await client.SetGameAsync($"{song.Title}",song.Url,StreamType.NotStreaming);
        }
        private async Task StatusNotPlaying()
        {
            await client.SetGameAsync("Nothing.");
        }
        internal async Task OutputSearchResultsFromSites(string[] ytEntries, string[] scEntries)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = "Song List",
                Color = Color.Purple
            };
            string field="";
            for(int i =0;i<ytEntries.Length;i++)
            {
                field += $"**{i}.**{ytEntries[i]}\n";
            }
            builder.AddField("**Youtube**", field);
            field = "";
            for(int i=0;i<scEntries.Length;i++)
            {
                field += $"**{i}.**{scEntries[i]}\n";
            }
            builder.AddField("**Soundcloud**", field);
            await OutputAsync("Please output ~[yt|sc]-[index]",builder.Build());
        }

        internal async Task OutputAsync(string msg, Embed embed)
        {
            if(textChannel == null)
            {
                try
                {
                    await CommandService.ReplyAsync(msg, embed: embed,options: new RequestOptions()
                    {
                        RetryMode = RetryMode.AlwaysRetry
                    });
                }
                catch (Exception e)
                {
                    LogHelper.Logln($"Error while executing ReplyAsync({msg}). Error: {e.Message}", LogType.Error);
                }
            }
            else
            {
                try
                {
                    await textChannel.SendMessageAsync(msg, false, embed, new RequestOptions()
                    {
                        RetryMode = RetryMode.AlwaysRetry,
                    });
                }
                catch (Exception)
                {
                    LOutput.Logln($"Cannot do {textChannel.Name}.SendMessageAsync({msg}). ", LogType.Error);
                }
            }
        }
        internal async Task OutputAsync(string msg)
        {
            if (textChannel == null)
            {
                try
                {
                    await CommandService.ReplyAsync(msg);
                }
                catch(Exception e)
                {
                    LogHelper.Logln($"Error while executing ReplyAsync({msg}). Error: {e.Message}", LogType.Error);
                }
            }
            else
            {
                try
                {
                    await textChannel.SendMessageAsync(msg,false,null,new RequestOptions()
                    {
                        RetryMode = RetryMode.AlwaysRetry,
                    });
                }
                catch (Exception)
                {
                    LOutput.Logln($"Cannot do {textChannel.Name}.SendMessageAsync({msg}). ", LogType.Error);
                }
            }
        } 
        #endregion
        #region Audio service
        partial void MusicPlay();
        internal void AddSongToQueue(Song song)
        {
            songQueue.Enqueue(song);
        }
        internal async Task AddSongToQueueAsync(Song song)
        {
            await Task.Run(() => songQueue.Enqueue(song));
        }
        #endregion
    }
}
