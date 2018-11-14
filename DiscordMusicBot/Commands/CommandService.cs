#if !FORCE_NO_MENTION_PREFIX
#if !MENTION_INVOKE_COMMAND
#if !PREFIX_INVOKE_COMMAND
#define MENTION_INVOKE_COMMAND
#define PREFIX_INVOKE_COMMAND
#endif
#endif
#endif
using DiscordMusicBot.LoungeBot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Discord;
using Discord.WebSocket;

namespace DiscordMusicBot.Commands
{
    internal static partial class CommandService
    {
#if PREFIX_INVOKE_COMMAND
        internal const char prefix = '~';
#endif
        internal const int childStartIndex = 1;
        #region Invokers
        const string changeLConfigField = "changelconfig";
        #endregion
        private static SocketUserMessage umsg;
        private static ISocketMessageChannel m_MsgChannel
        {
            get
            {
                if (mChannelInit)
                    return msgChannel;
                else
                    return (msgChannel = umsg.Channel);
            }
        }
        private static ISocketMessageChannel msgChannel;
        private static bool mChannelInit = false;
        private static IDMChannel m_DMChannel
        {
            get
            {
                if (dmChannelInit)
                    return dmChannel;
                else
                {
                    AssignDMChannel();
                    return dmChannel;
                }
            }
        }
        private static IDMChannel dmChannel;
        private static bool dmChannelInit = false;
        private static async Task DeleteUMsgAsync()
        {
            await umsg.DeleteAsync();
        }
        internal static async Task ExecuteAsync(string command, SocketMessage uMsg)
        {
            umsg = uMsg as SocketUserMessage;
            string[] arr = await CommandServiceHelper.CommandSplit(command);
            switch (arr[0])
            {
                case changeLConfigField:
                    await ChangeSerializableFieldCmd(arr);
                    break;
            }
        }
        internal static async Task ReplyAsync(string msg)
        {
            await umsg.Channel.SendMessageAsync(msg);
        }
        private static async void AssignDMChannel()
        {
            dmChannel = await GetDMAsync();
        }
        private static async Task<IDMChannel> GetDMAsync()
        {
            return await umsg.Author.GetOrCreateDMChannelAsync();
        }
        internal static async Task DMAsync(string msg)
        {
            await (await umsg.Author.GetOrCreateDMChannelAsync()).SendMessageAsync(msg);
        }
    }
    internal static class CommandServiceHelper
    {
        static readonly char[] splitter = { ' ' };
        static readonly char[] nester = { '\'', '\"' };
        internal static async Task<string[]> CommandSplit(string command)
        {
            List<string> result = new List<string>();
            int a = 0;
            string str = "";
            bool openedNester = false;
            for (int i = 0; i < command.Length; i++)
            {
                if (!openedNester)
                {
                    if (nester.Contains(command[i]))
                    {
                        openedNester = true;
                        continue;
                    }
                    if (!splitter.Contains(command[i]))
                    {
                        str += command[i];
                    }
                    else
                    {
                        if (str != "")
                        {
                            result[a++] = str;
                            str = "";
                        }
                    }
                }
                else
                {
                    if (nester.Contains(command[i]))
                    {
                        openedNester = false;
                        result[a++] = str;
                        str = "";
                        continue;
                    }
                    else
                    {
                        str += command[i];
                    }
                }
            }
            if (openedNester)
            {
                LogHelper.Logln("Nester was opened, but was never closed :(", LogType.Warning);
                await CommandService.ReplyAsync("Nester was never closed.");
                return new string[0];
            }
            if (str != "")
            {
                result[a++] = str;
            }
            //Done with the processing
            string[] resultArr = result.ToArray();
            return resultArr;
        }
        /// <summary>
        /// Out:
        /// -1: Not command
        /// 0 or up: starting index
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        internal static async Task<int> IsCommand(string msg)
        {
#if MENTION_INVOKE_COMMAND
            
#endif
        }
    }

}
