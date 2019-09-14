using System;
using System.Collections.Generic;
using System.Text;

namespace SoundcloudDLHelper
{
    public struct SearchEntry
    {
        public bool isSong
        {
            get
            {
                int length = _url.Split('/',StringSplitOptions.RemoveEmptyEntries).Length;
#if DEBUG
                Console.WriteLine("Url " + _url + " contains " + length + ".");
#endif
                return length == 2;
            }
        }
        public bool isPlaylist
        {
            get
            {
                string[] strs = _url.Split('/',StringSplitOptions.RemoveEmptyEntries);
                return strs[2] == "sets" && strs.Length==3;
            }
        }
        public string title;
        public string url
        {
            get
            {
                return "https://soundcloud.com" + _url;
            }
            set
            {
                _url = value;
            }
        }
        private string _url;
        internal void InsertUrl(string str)
        {
            _url = str + _url;
        }
        internal void AddToUrl(string str)
        {
            _url += str;
        }
        internal SearchEntry(string t, string u)
        {
            title = t;
            _url = u;
        }
    }

}
