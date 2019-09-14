#if ML_INITIALIZED
#if !FORCE_NO_TRAINING
#define TRAINING
#endif
#endif

#if !WINDOWS && ! MAC
#define RASPBERRY_PI
#endif
using System;
using System.Collections.Generic;
using System.Text;

#if RASPBERRY_PI
using LDecimal = System.Float;
using LUInt = System.UInt16;
using LInt = System.Int16;
#else
using LDecimal = System.Double;
using LUint = System.UInt32;
using LInt = System.Int32;
#endif
namespace DiscordMusicBot.LoungeBot
{
    internal struct StringDifferenceService
    {
        uint deletionScore;
        uint insertionScore;
        uint differentSubstitutionScore;
        internal StringDifferenceService(uint delScore, uint insScore, uint diffScore)
        {
            deletionScore = delScore;
            insertionScore = insScore;
            differentSubstitutionScore = diffScore;
        }
        public LUint DifferenceScore(string i, string c)
        {
            return Helper.StringDifference.DifferenceScore(i, c, deletionScore, insertionScore, differentSubstitutionScore);
        }
    }
    class SongManager
    {
        LoungeSongs loungeSongs;
        StringDifferenceService strDiff;
        public SongManager(string songsDirectory)
        {
            loungeSongs = new LoungeSongs(songsDirectory);
            //if config contains info, initialize strDiff;
            //else
            strDiff = new StringDifferenceService(1, 1, 2);

        }
#if TRAINING

#endif
    }
}
