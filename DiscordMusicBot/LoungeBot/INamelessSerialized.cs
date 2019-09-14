using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordMusicBot.LoungeBot
{
    interface INamelessSerialized<T>
    {
        /// <summary>
        /// Regular start up
        /// </summary>
        void OnStartUp();
        /// <summary>
        /// File not existant
        /// </summary>
        void OnFirstStart();
        void Serialize();
        T Deserialize();

    }
}
