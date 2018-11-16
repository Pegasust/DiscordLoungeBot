#if !ML_INITIALIZED
#define DELETE_SONGS
#endif
#if !WINDOWS && ! MAC
#define RASPBERRY_PI
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
namespace DiscordMusicBot.LoungeBot
{
    internal struct Song
    {
        internal readonly string FilePath;
        internal readonly string Title;
        internal readonly string Duration;
        internal readonly string Url;
        internal Song(string path, string title, string duration, string url)
        {
            FilePath = path;
            Title = title;
            Duration = duration;
            Url = url;
        }
        internal static readonly Song Null =
            new Song("", "", "!NULL!", "");
        internal bool isNull
        {
            get
            {
                return Duration == "!NULL!";
            }
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
            LogSeverity.Debug
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
            10000
#else
            5000
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
        #region Audio Service
        private Queue<Song> songQueue;
        #endregion
        internal LoungeBot()
        {
            Init();
        }
        protected virtual async void Init()
        {
            //Initialize values
            //ClientConfig automatically handles first time launch
            songQueue = new Queue<Song>();
            disposeToken = new CancellationTokenSource();
            threadSafePause = new TaskCompletionSource<bool>(); //From audio service

            //Initialize client
            client = new DiscordSocketClient(dsconfig);
            client.Log += OnClientLog;
            client.Disconnected += Disconnected;
            client.Connected += Connected;
            client.Ready += OnClientReady;
            client.MessageReceived += OnMessageReceived;

            LOutput.Logln("Client is starting.", LogType.Success);
            await Connect();

            //Threaded
            new Thread(MusicPlay).Start();
            //Handle Disconnection
            try
            {
                while (disposeToken.IsCancellationRequested)
                {
                    ConnectionState state = client.ConnectionState;
                    if (state == ConnectionState.Disconnected)
                    {
                        await Task.Delay(2000, disposeToken.Token);
                        if (state == ConnectionState.Disconnected)
                        {
                            await Connect();
                        }
                    }
                    await Task.Delay(2000, disposeToken.Token);
                }
            }
            catch (TaskCanceledException)
            {

            }
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
                Song song = songQueue.Dequeue();
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
            if (sMsg.Author.Id == client.CurrentUser.Id)
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
                await sMsg.DeleteAsync();
            }
            #region IsCommand
            LOutput.Logln(" " + isCommandSuffix);
            await CommandService.ExecuteAsync(sMsg.Content, sMsg, startIndex);
            #endregion
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
                LOutput.Logln($"Cannot find {ClientConfig.serverName.value}!", LogType.Warning);
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
        private async Task OutputAsync(string msg)
        {
            if (textChannel == null)
            {
                await CommandService.ReplyAsync(msg);
            }
            else
            {
                await textChannel.SendMessageAsync(msg);
            }
        }        
        #endregion
        #region Audio service
        partial void MusicPlay();
        #endregion
    }
}
