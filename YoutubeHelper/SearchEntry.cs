using System;
using System.Collections.Generic;
using System.Text;

namespace YoutubeHelper
{
    public struct SearchEntry
    {
        const string PlaylistPrefix = "https://www.youtube.com/playlist?list=";
        const string VideoPrefix = "https://www.youtube.com/watch?v=";
        const string ChannelPrefix = "https://www.youtube.com/channel/";
        public enum EntryType
        {
            Video,
            Playlist,
            Channel
        }
        public EntryType entryType { get; private set; }
        public string title { get; private set; }
        public string url
        {
            get
            {
                switch(entryType)
                {
                    case EntryType.Video:
                        return VideoPrefix + _url;
                    case EntryType.Playlist:
                        return VideoPrefix + _url;
                    case EntryType.Channel:
                        return ChannelPrefix + _url;
                    default:
                        return null;
                }
            }
        }
        private string _url;
        internal SearchEntry(EntryType type, string title, string url)
        {
            entryType = type;
            this.title = title;
            _url = url;
        }
    }
}
