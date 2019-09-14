using System;
using System.Collections.Generic;
using System.Text;
#if RASPBERRY_PI
using LDecimal = System.Float
using LUInt = System.UInt16;
using LInt = System.Int16;
#else
using LDecimal = System.Double;
using LUint = System.UInt32;
using LInt = System.Int32;
#endif
namespace DiscordMusicBot.LoungeBot.Helper
{
    public static class StringDifference
    {
        /// <summary>
        /// used on single word
        /// </summary>
        /// <param name="input"></param>
        /// <param name="compare"></param>
        /// <returns></returns>
        public static LUint DifferenceScore(string input, string compare, uint deletionScore, uint insertionScore, uint differentSubstitutionScore)
        {
            LUint[] v0 = new LUint[compare.Length + 1];
            LUint[] v1 = new LUint[compare.Length + 1];

            for (LUint i = 0; i < compare.Length; i++)
            {
                v0[i] = i;
            }

            for (LUint i = 0; i < input.Length - 1; i++)
            {
                v1[0] = i + 1;
                for (LUint j = 0; j < compare.Length; j++)
                {
                    LUint deletionCost = v0[j + 1] + deletionScore;
                    LUint insertionCost = v1[j] + insertionScore;
                    LUint substitutionCost;
                    if (input[(int)i] == compare[(int)j])
                    {
                        substitutionCost = v0[j];
                    }
                    else
                    {
                        substitutionCost = v0[j] + differentSubstitutionScore;
                    }
                    v1[j + 1] = Math.Min(Math.Min(deletionCost, insertionCost), substitutionCost);
                }
                //swap rows
                LUint[] temp = v0;
                v0 = v1;
                v1 = temp;
            }
            return v0[compare.Length];
        }
    }
}
