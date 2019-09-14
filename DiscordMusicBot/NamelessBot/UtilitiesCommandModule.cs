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
using System.Collections.Concurrent;

namespace DiscordMusicBot.LoungeBot
{
    partial class LoungeBot
    {
        internal async Task<SocketUser> GetSocketUserAsync(string mentionedString)
        {
            ulong id;
            if(!ulong.TryParse(mentionedString,out id))
            {
                if(!ulong.TryParse(mentionedString.Substring(2,mentionedString.Length-3),out id))
                {
                    LogHelper.Logln($"\"{mentionedString}\" does not contain id!", LogType.Warning);
                    await OutputAsync($"Unable to get id from {mentionedString}");
                    return null;
                }
            }
            return client.GetUser(id);
        }
    }
}
namespace DiscordMusicBot.Commands
{
    readonly struct UserStatusPair
    {
        internal readonly SocketUser user;
        internal readonly UserStatus status;
        internal readonly Action task;
        internal UserStatusPair(SocketUser usr, UserStatus stt, Action action)
        {
            user = usr;
            status = stt;
            task = action;
        }
        internal static async Task DMUser(SocketUser user, string msg)
        {
#if DEBUG
            LogHelper.Logln($"Trying to DM {user}: \"{msg}\"", LogType.Debug);
#endif
            var DMChannel = await user.GetOrCreateDMChannelAsync
                (new RequestOptions() { RetryMode = RetryMode.AlwaysRetry });
            await DMChannel.SendMessageAsync(msg, options: new RequestOptions()
            {
                RetryMode = RetryMode.AlwaysRetry
            });
#if DEBUG
            LogHelper.Logln($"Successfully DM {user}", LogType.Success);
#endif
        }
    }
    partial class CommandService
    {
        private static ConcurrentQueue<UserStatusPair> userAwaitQ = new ConcurrentQueue<UserStatusPair>();
        [Flags()]
        private enum ServiceCommand
        {
            AwaitUserStatus = 1,
            AwaitUserGame = 4,
            AwaitGeneral = AwaitUserStatus|AwaitTime|AwaitUserGame,
            AwaitTime = 2,
            ShowAwaitList=8,
        }
        private static readonly Dictionary<string, UserStatus> userStatusLookUp = new Dictionary<string, UserStatus>()
        {
            { "online", UserStatus.Online },
            { "idle", UserStatus.Idle },
            { "offline", UserStatus.Offline },
            {"onl", UserStatus.Online },
            {"off", UserStatus.Offline },
        };
        private static readonly Dictionary<string, ServiceCommand> serviceCommandLookUp = new Dictionary<string, ServiceCommand>()
        {
            {"await", ServiceCommand.AwaitGeneral },
            {"-a/usr", ServiceCommand.AwaitUserStatus },
            {"-a/usrg", ServiceCommand.AwaitUserGame },
            {"-a/t", ServiceCommand.AwaitTime },
            {"q", ServiceCommand.ShowAwaitList },
            {"queue", ServiceCommand.ShowAwaitList },
            {"list", ServiceCommand.ShowAwaitList },

        };
        private static async Task UtilitiesServiceCommand(string[] param, bool isMain = false)
        {
            int i = arrayStartIndex - (isMain ? 1 : 0);
            for(;i<param.Length;i++)
            {
                ServiceCommand cmd;
                if(serviceCommandLookUp.TryGetValue(param[i].ToLower(), out cmd))
                {
                    sbyte flagChecker = 1<<3;
                    while(flagChecker!=0)
                    {
                        switch(cmd & (ServiceCommand) flagChecker)
                        {
                            case ServiceCommand.AwaitUserStatus:
                                if(i+1==param.Length)
                                {
                                    await Program.Bot.OutputAsync("Please mention the user you want to await.");
                                }
                                else
                                {
                                    string userMentionString = param[++i];
                                    SocketUser usr = await Program.Bot.GetSocketUserAsync(userMentionString);
                                    UserStatus status = usr.Status;
                                    Action actionToEnqueue;
                                    if(i+1 == param.Length)
                                    {
                                        //If user do not specify awaiting status nor action
                                        switch(status)
                                        {
                                            case UserStatus.Online:
                                                if(usr.Game.HasValue)
                                                {
                                                    //TODO: await till user out of game
                                                }
                                                break;
                                            default:
                                                status = UserStatus.Online;
                                                break;
                                        }
                                        actionToEnqueue = async()=>await UserStatusPair.DMUser(usr,
                                                    $"{usr} is now {status}!");
                                    }
                                    else
                                    {
                                        string awaitingStatus = param[++i];
                                        if(!userStatusLookUp.TryGetValue(awaitingStatus.ToLower(), out status))
                                        {
                                            await ReplyAsync($"Unable to identify UserStatus from \"{awaitingStatus}\". Default is Online.");
                                            status = UserStatus.Online;
                                        }
                                        if(i+1 == param.Length)
                                        {
                                            //if user do not specify awaiting action
                                            actionToEnqueue = async()=> await UserStatusPair.DMUser(umsg.Author,
                                                    $"{usr} is now {status}!");
                                        }
                                        else
                                        {
                                            string command = param[++i];
                                            actionToEnqueue = async()=>await ExecuteAnonymouslyAsync(command, 0);
                                        }
                                    }
                                    userAwaitQ.Enqueue(new UserStatusPair(usr, status, actionToEnqueue));
                                    await ReplyAsync($"Successfully enqueue command a/usr {usr.Username} {status}");
                                }
                                break;
                            case ServiceCommand.AwaitTime:
                                //TODO: Implement this
                                break;
                            case ServiceCommand.ShowAwaitList:
                                Program.Bot.promptedToShowAwaitingQueue = true;
                                break;
                        }
                        flagChecker >>= 1;
                    }
                }
            }
        }        
        internal static async Task AwaitUser()
        {
            if(Program.Bot.promptedToShowAwaitingQueue)
            {
                string queueStr = "";
                for (int i = 0; i < userAwaitQ.Count; i++)
                {
                    UserStatusPair pair;
                    if (userAwaitQ.TryDequeue(out pair))
                    {
                        queueStr += $"{i}.{pair.user}; {pair.status};\n";
                        if (pair.user.Status != pair.status)
                        {
                            //Try again
                            userAwaitQ.Enqueue(pair);
                        }
                        else
                        {
#if DEBUG
                            LogHelper.Logln($"Doing task since {pair.user} is {pair.status}", LogType.Success);
#endif
                            await Task.Run(pair.task);
                        }
                    }
                }
                Program.Bot.promptedToShowAwaitingQueue = false;
                EmbedBuilder builder = new EmbedBuilder()
                {
                    Color = Color.Green,
                    Title = "AwaitingQ",
                };
                builder.AddField("Users", (string.IsNullOrWhiteSpace(queueStr) ? "Nothing in Q" : queueStr));
                await Program.Bot.OutputAsync("Showing AwaitingQ", builder.Build());
            }
            else
            {
                for (int i = 0; i < userAwaitQ.Count; i++)
                {
                    UserStatusPair pair;
                    if (userAwaitQ.TryDequeue(out pair))
                    {
                        if (pair.user.Status != pair.status)
                        {
                            //Try again
                            userAwaitQ.Enqueue(pair);
                        }
                        else
                        {
#if DEBUG
                            LogHelper.Logln($"Doing task since {pair.user} is {pair.status}", LogType.Success);
#endif
                            await Task.Run(pair.task);
                        }
                    }
                }
            }
        }
    }
}
