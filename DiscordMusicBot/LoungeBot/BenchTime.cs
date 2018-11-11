using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordMusicBot.LoungeBot
{
    internal static class BenchTime
    {
#if TRACETIME
        private static readonly Benchmarker[] markers;
        private const int maxMarkers = 10;
        private static int endArray;
        public static int result(int index=0)
        {
            return markers[index].offset;
        }
        public static double exactResult(int index = 0)
        {
            return markers[index].exactOffset;
        }
        static BenchTime()
        {
            markers = new Benchmarker[maxMarkers];
            endArray = 0;
        }
#endif
        public static void begin()
        {
#if TRACETIME
            markers[endArray++] = new Benchmarker(DateTime.Now);
#endif
        }

        public static void SendResult(string prefix, string suffix, int index=0)
        {
#if TRACETIME
            Print($"{prefix}{markers[index].offset}{suffix}");
#endif
        }

        public static void SendExactResult(string prefix, string suffix, int index=0)
        {
#if TRACETIME
            Print($"{prefix}{markers[index].exactOffset}{suffix}");
#endif
        }

        private static void Print(string msg,ConsoleColor textColor = ConsoleColor.Yellow)
        {
            Console.ForegroundColor = textColor;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
    }
    struct Benchmarker
    {
#if TRACETIME
        internal DateTime start;
        internal Benchmarker(DateTime beginTime)
        {
            start = beginTime;
        }
        internal int offset
        {
            get
            {
                return (DateTime.Now - start).Milliseconds;
            }
        }
        internal double exactOffset
        {
            get
            {
                return (DateTime.Now - start).TotalMilliseconds;
            }
        }
#endif
    }
}
