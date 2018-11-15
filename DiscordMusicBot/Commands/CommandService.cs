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
    /// <summary>
    /// Upon creating an instance of this class, always go to Commands/CommandService and add
    /// new const string in region invokers nad new async void that gets called by the invoker
    /// </summary>
    internal static partial class CommandService
    {
#if PREFIX_INVOKE_COMMAND
        internal const char prefix = '~';
#endif
        internal const int arrayStartIndex = 1;
        #region Invokers
        const string changeLConfigField = "changelconfig";
        static async void ChangeLConfigField(string[] param)
        {
            BenchTime.begin();
            await ChangeSerializableFieldCmd(param);
            BenchTime.SendResult("ChangeLConfigField took ", "ms.");
        }

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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="command">Must not include signs of command</param>
        /// <param name="uMsg"></param>
        /// <returns></returns>
        internal static async Task ExecuteAsync(string command, SocketMessage uMsg, int startIndex = 0)
        {
            umsg = uMsg as SocketUserMessage;
            string[] arr = await CommandServiceHelper.CommandSplit(command, startIndex);
            switch (arr[0])
            {
                case changeLConfigField:
                    ChangeLConfigField(arr);
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
        internal const int NOT_COMMAND = -2;
        static readonly char[] splitter = { ' ' };
        static readonly char[] nester = { '\'', '\"' };
        internal static string MentionFormat(string id)
        {
            return $"<@{id}>";
        }
        internal static string MentionFormat(ulong id)
        {
            return $"<@{id}>";
        }
        internal static async Task<string[]> CommandSplit(string command, int startIndex = 0)
        {
            BenchTime.begin();
            List<string> result = new List<string>((command.Length-startIndex)/3);
            int a = 0;
            string str = "";
            bool openedNester = false;
            for (int i = startIndex; i < command.Length; i++)
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
            BenchTime.SendResult("Splitting command took ", "ms.");
            return resultArr;
        }
        /// <summary>
        /// Out: startIndex
        /// -2: not command
        /// -1 or up: starting index
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        internal static async Task<int> CommandSignCheck(string msg)
        {
            int startIndex = NOT_COMMAND;
#if PREFIX_INVOKE_COMMAND
            if (msg[0] == CommandService.prefix)
            {
                return 1;
            }
#endif
#if MENTION_INVOKE_COMMAND
            startIndex = await MentionedCheck(msg);
#endif
            return startIndex;
        }
        /// <summary>
        /// Out:
        /// -2: Not command
        /// -1 or up: starting index
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        internal static async Task<int> MentionedCheck(string msg)
        {
#if MENTION_INVOKE_COMMAND
            return await Task.Run(() => SMentionedCheck(msg));
#else
            Print("Mentioned Check should not be called!", LogType.Warning);
            return NOT_COMMAND;
#endif
        }
        private static int SMentionedCheck(string msg)
        {
            #region NEED OPTIMIZATION
            string mentionString = MentionFormat(ClientConfig.clientID);
            int i = 0;
            for (; i < mentionString.Length; i++)
            {
                if (msg[i] != mentionString[i])
                {
                    return NOT_COMMAND;
                }
            }
            return ++i;
            #endregion
        }

        internal static async Task<string> CommandFromMessage(string msg)
        {
            int startIndex = await CommandSignCheck(msg);
            return msg.Substring(startIndex);
        }
    }

}
