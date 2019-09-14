using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
namespace DiscordMusicBot.LoungeBot
{
    internal static class BenchTime
    {
#if TRACETIME
        private static readonly Benchmarker[] markers;
        private static ConcurrentQueue<int> availableIndexes;
        private static ConcurrentQueue<int> nestedIndexes;
        private const int maxActiveMarkers = 16;
        static BenchTime()
        {
            markers = new Benchmarker[maxActiveMarkers];
            availableIndexes = new ConcurrentQueue<int>();
            nestedIndexes = new ConcurrentQueue<int>();
            for (int i = 0; i < maxActiveMarkers; i++)
            {
                availableIndexes.Enqueue(i);
            }
        }
#endif
        public static bool begin()
        {
#if TRACETIME
            int index;
            if (!availableIndexes.TryDequeue(out index))
            {
                LogHelper.Logln($"Unable to retrieve available index. There might be more than {maxActiveMarkers} benchtime markers active.", LogType.Error);
                return false;
            }
            else
            {
                markers[index] = new Benchmarker(DateTime.Now);
                return true;
            }
#endif
        }
        public static bool beginNested()
        {
#if TRACETIME
            int index;
            if (!availableIndexes.TryDequeue(out index))
            {
                LogHelper.Logln($"Unable to retrieve available index. There might be more than {maxActiveMarkers} benchtime markers active.",LogType.Error);
                return false;
            }
            else
            {
                markers[index] = new Benchmarker(DateTime.Now);
                nestedIndexes.Enqueue(index);
                return true;
            }
#endif
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="_index">= -1 if cannot dequeue from available indexes</param>
        /// <returns></returns>
        public static bool begin(out int _index)
        {
            int index;
            if (!availableIndexes.TryDequeue(out index))
            {
                LogHelper.Logln($"Unable to retrieve available index. There might be more than {maxActiveMarkers} benchtime markers active.",LogType.Error);
                _index = -1;
                return false;
            }
            else
            {
                markers[index] = new Benchmarker(DateTime.Now);
                _index = index;
                return true;
            }
        }
        public static int result(int index = 0)
        {
            availableIndexes.Enqueue(index);
            return markers[index].offset;
        }
        public static double exactResult(int index = 0)
        {
            availableIndexes.Enqueue(index);
            return markers[index].exactOffset;
        }
        /// <summary>
        /// Returns -1 if cannot retrieve nestedIndex
        /// </summary>
        /// <returns></returns>
        public static double nestedExactResult()
        {
            int index;
            if (nestedIndexes.TryDequeue(out index))
            {
                availableIndexes.Enqueue(index);
                return markers[index].exactOffset;
            }
            else
            {
                return -1;
            }
        }
        /// <summary>
        /// Returns -1 if cannot retrieve nestedIndex
        /// </summary>
        /// <returns></returns>
        public static int nestedResult()
        {
            int index;
            if (nestedIndexes.TryDequeue(out index))
            {
                availableIndexes.Enqueue(index);
                return markers[index].offset;
            }
            else
            {
                return -1;
            }
        }
        public static void SendResult(string prefix, string suffix, int index=0)
        {
#if TRACETIME
            Print($"{prefix}{markers[index].offset}{suffix}");
            availableIndexes.Enqueue(index);
#endif
        }
        public static void SendExactResult(string prefix, string suffix, int index=0)
        {
#if TRACETIME
            Print($"{prefix}{markers[index].exactOffset}{suffix}");
            availableIndexes.Enqueue(index);
#endif
        }
        public static void SendNestedResult(string prefix, string suffix)
        {
#if TRACETIME
            int index;
            if (nestedIndexes.TryDequeue(out index))
            {
                availableIndexes.Enqueue(index);
                Print($"{prefix}{markers[index].offset}{suffix}");
            }
            else
            {
                LogHelper.Logln("Unable to retrieve from availableIndexes!", LogType.Error);
            }
#endif
        }
        public static void SendNestedExactResult(string prefix, string suffix)
        {
#if TRACETIME
            int index;
            if (nestedIndexes.TryDequeue(out index))
            {
                availableIndexes.Enqueue(index);
                Print($"{prefix}{markers[index].exactOffset}{suffix}");
            }
            else
            {
                LogHelper.Logln("Unable to retrieve from availableIndexes!", LogType.Error);
            }
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
