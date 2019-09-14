using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordMusicBot.LoungeBot
{
    interface IHasDefaultValue<T>
    {
        T DefaultValue
        {
            get;
        }
    }
}
