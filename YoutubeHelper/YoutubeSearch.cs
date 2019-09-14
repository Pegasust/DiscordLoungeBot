using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

using DiscordMusicBot;
using DiscordMusicBot.LoungeBot;

namespace YoutubeHelper
{
    internal static class YoutubeSearch
    {
        /// <summary>
        /// Returns the first song url in the search list
        /// </summary>
        /// <param name="searchKeyword"></param>
        /// <param name="applicationName"></param>
        /// <returns>First song url in the search list</returns>
        public static async Task<string> GetFirstMatchingVideo(string searchKeyword, string applicationName=null)
        {
            YouTubeService youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyCEXZKGjS0FQQcq6ESUgFSFRfWsLvKazM0",
                ApplicationName = applicationName
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = searchKeyword; // Replace with your search term.
            searchListRequest.MaxResults = 15;
            // Call the search.list method to retrieve results matching the specified query term.
            var searchListResponse = await searchListRequest.ExecuteAsync();
#if DEBUG
            LogHelper.Logln("Search result:\n{",LogType.Debug);
            foreach(var v in searchListResponse.Items)
            {
                LogHelper.Logln($"{v.Snippet.Title} ({v.Id.VideoId})", LogType.Debug);
            }
#endif
            LogHelper.Logln("}", LogType.Debug);

            foreach(var result in searchListResponse.Items)
            {
                if (result.Id.Kind == "youtube#video")
                {
                    return "https://www.youtube.com/watch?v=" + result.Id.VideoId;
                }
            }
            return null;

            //// Add each result to the appropriate list, and then display the lists of
            //// matching videos, channels, and playlists.
            //foreach (var searchResult in searchListResponse.Items)
            //{
            //    switch (searchResult.Id.Kind)
            //    {
            //        case "youtube#video":
            //            videos.Add(String.Format("{0} ({1})", searchResult.Snippet.Title, searchResult.Id.VideoId));
            //            break;
                
            //        case "youtube#channel":
            //            channels.Add(String.Format("{0} ({1})", searchResult.Snippet.Title, searchResult.Id.ChannelId));
            //            break;

            //        case "youtube#playlist":
            //            playlists.Add(String.Format("{0} ({1})", searchResult.Snippet.Title, searchResult.Id.PlaylistId));
            //            break;
            //    }
            //}
            
        }
        public static async Task<SearchEntry[]> GetEntries(string search, string appName = null, long maxResult = 15)
        {
            YouTubeService youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = "AIzaSyCEXZKGjS0FQQcq6ESUgFSFRfWsLvKazM0",
                ApplicationName = appName
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.Q = search; // Replace with your search term.
            searchListRequest.MaxResults = maxResult;
            // Call the search.list method to retrieve results matching the specified query term.
            var searchListResponse = await searchListRequest.ExecuteAsync();
            SearchEntry[] entries = new SearchEntry[searchListResponse.Items.Count];
            for(int i =0;i<entries.Length;i++)
            {
                switch (searchListResponse.Items[i].Id.Kind)
                {
                    case "youtube#video":
                        entries[i] = new SearchEntry(SearchEntry.EntryType.Video,
                            searchListResponse.Items[i].Snippet.Title, searchListResponse.Items[i].Id.VideoId);
                        break;

                    case "youtube#channel":
                        entries[i] = new SearchEntry(SearchEntry.EntryType.Channel,
                            searchListResponse.Items[i].Snippet.Title, searchListResponse.Items[i].Id.ChannelId);
                        break;

                    case "youtube#playlist":
                        entries[i] = new SearchEntry(SearchEntry.EntryType.Playlist,
                            searchListResponse.Items[i].Snippet.Title, searchListResponse.Items[i].Id.PlaylistId);
                        break;
                }
            }
            return entries;
        }
    }
}
