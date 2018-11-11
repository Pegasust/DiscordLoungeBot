#if !ML_INITIALIZED
#define DELETE_SONGS
#endif

using Discord;
using Discord.Audio;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LOutput = DiscordMusicBot.LoungeBot.LogHelper;
namespace DiscordMusicBot.LoungeBot
{
    internal struct Song
    {
        internal readonly string FilePath;
        internal readonly string Title;
        internal readonly string Duration;
    }
    internal class LoungeBot : IDisposable
    {
        private DiscordSocketClient client;
        private IVoiceChannel voiceChannel;
        private ITextChannel textChannel;
        private TaskCompletionSource<bool> tcs;
        private CancellationTokenSource disposeToken;
        private IAudioClient audio;
        public bool IsDisposed;
        #region Audio Service
        private Queue<Song> songQueue;
        //Async pause
        private bool pause
        {
            get
            {
                return internalPause;
            }
            set
            {
                new Thread(() => tcs.TrySetResult(value)).Start();
                internalPause = value;
            }
        }
        private bool internalPause;
        #endregion
        internal LoungeBot()
        {
            Init();
        }
        protected virtual void Init()
        {

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
                    LOutput.Logln("Song " + song.Title + " is not found in " + song.FilePath+".",LogType.Warning);
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
    }
}
