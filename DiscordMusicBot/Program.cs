﻿using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordMusicBot {
    internal class Program {
        public static
#if FALLBACK
            MusicBot 
#else
            LoungeBot.LoungeBot
#endif
            Bot;
        private static CancellationTokenSource _cts;

        private static void Main(string[] args) {
            ConsoleHelper.Set();
            Console.Title = "Music Bot (Loading...)";
            Console.WriteLine("(Press Ctrl + C or close this Window to exit Bot)");

            try {
#region JSON.NET
                Config cfg;
                //Create the config.json on first run
                if (!File.Exists("config.json"))
                {
                    FileStream newConfig = File.Create("config.json");
                    cfg = new Config();
                    JsonSerializer serializer = new JsonSerializer();
                    using (StreamWriter sw = new StreamWriter(newConfig))
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, cfg);
                    }
                }
                else
                {
                    string json = File.ReadAllText("config.json");
                    cfg = JsonConvert.DeserializeObject<Config>(json);
                }

                //if (cfg == new Config())
                //    throw new Exception("Please insert values into Config.json!");
#endregion

#region TXT Reading
                //string[] config = File.ReadAllLines("config.txt");
                //Config cfg = new Config() {
                //    BotName = config[0].Split(':')[1],
                //    ChannelName = config[1].Split(':')[1],
                //    ClientId = config[2].Split(':')[1],
                //    ClientSecret = config[3].Split(':')[1],
                //    ServerName = config[4].Split(':')[1],
                //    Token = config[5].Split(':')[1],
                //};
#endregion
            } catch (Exception e) {
                MusicBot.Print("Your config.json has incorrect formatting, or is not readable!", ConsoleColor.Red);
                MusicBot.Print(e.Message, ConsoleColor.Red);

                try {
                    //Open up for editing
                    Process.Start("config.json");
                } catch {
                    // file not found, process not started, etc.
                }

                Console.ReadKey();
                return;
            }

            Do().GetAwaiter().GetResult();

            //Thread Block
            //Thread.Sleep(-1);
        }

        private static async Task Do() {
            try {
                _cts = new CancellationTokenSource();
                Bot = new
#if FALLBACK
                    MusicBot
#else
                    LoungeBot.LoungeBot
#endif
                    ();
                
                //Async Thread Block
                await Task.Delay(-1, _cts.Token);
            } catch (TaskCanceledException) {
                // Task Canceled
            }
        }
    }
}