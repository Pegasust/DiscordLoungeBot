using System;
using System.IO;
using System.Threading.Tasks;

#if RASPBERRY_PI
using LDecimal = System.Float
using LUInt = System.UInt16;
using LInt = System.Int16;
#else
using LUint = System.UInt32;
#endif
//TODO: fix constructors, less try/catch please.
namespace DiscordMusicBot.LoungeBot
{
    internal struct OfflineSong
    {
        internal Song song;
        internal LUint timesPlayed;
        internal static readonly OfflineSong Null = new OfflineSong(Song.Null, 0);
        internal OfflineSong(string filePath, string title, string duration, string url, uint timesPlayed)
        {
            song = new Song(filePath, title, duration, url);
            this.timesPlayed = timesPlayed;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="song"></param>
        /// <param name="timesPlayed"> default = 1 because if the user wants to add a song to a playlist,
        /// he would need to add it to the queue</param>
        internal OfflineSong(Song song, uint timesPlayed = 1)
        {
            this.song = song;
            this.timesPlayed = timesPlayed;
        }
        public static bool operator ==(OfflineSong a, Song b)
        {
            return a.song.Duration == b.Duration && a.song.Title == b.Title;
        }
        public static bool operator !=(OfflineSong a, Song b)
        {
            return a.song.Duration != b.Duration || a.song.Title != b.Title;
        }
    }
    internal readonly struct OfflineSongInfo
    {
        internal readonly OfflineSong offSong;
        internal readonly LUint lineNumber;
        internal OfflineSongInfo(OfflineSong offSong, LUint lineNo)
        {
            this.offSong = offSong;
            lineNumber = lineNo;
        }
    }
    public struct LoungeSongs
    {
        public readonly string SongCollectionPath;
        public const string defaultSongCollectionPath = "LoungeSongs";
        internal SongCollection songCollection;
        public LoungeSongs(string directoryPath = defaultSongCollectionPath,string infoFileName = "songinf")
        {
            SongCollectionPath = Path.GetFullPath(directoryPath);
#if DEBUG
            LogHelper.Logln($"Path of LoungeSongs service is {SongCollectionPath}.", LogType.Debug);
#endif
            try
            {
                Directory.CreateDirectory(SongCollectionPath);
                songCollection = new SongCollection(Path.Join(SongCollectionPath, infoFileName+SongCollectionFormatter.extension));
                LogHelper.Logln("LoungeSongs service initialized successfully.", LogType.Success);
            }
            catch(Exception e)
            {
                LogHelper.Logln($"Error initializing LoungeSongs service. {e.Message}", LogType.Error);
                songCollection = null;
            }
        }
        /// <summary>
        /// <para>
        /// In byte; max value: 15 exabyte = 15 * 1024 petabyte = 15 * 1024^2 terabyte
        /// </para>
        /// <para>
        /// TODO: Algorithm might take quite a while if the directory is too big. => Better time complexity for larger project please :D
        /// </para>
        /// </summary>
        /// <param name="p">
        /// Directory path
        /// </param>
        internal static ulong GetDirectorySize(string p)
        {
            // 1
            // Get array of all file names.
            string[] a = Directory.GetFiles(p, "*.*");

            // 2
            // Calculate total bytes of all files in a loop.
            ulong b = 0;
            foreach (string name in a)
            {
                // 3
                // Use FileInfo to get length of each file.
                FileInfo info = new FileInfo(name);
                b += (ulong)info.Length;
            }
            // 4
            // Return total size
            return b;
        }
    }
    internal class SongCollection
    {
        internal
#if !DEBUG
            readonly
#endif
            string infoFilePath;
        internal SongCollection(string filePath)
        {
            if (File.Exists(filePath))
            {
#if DEBUG
                LogHelper.Logln("File already exists, no need to create new one.", LogType.Debug);
#endif
                infoFilePath = filePath;
            }
            else
            {
#if DEBUG
                LogHelper.Logln("File does not exist. Creating new file.", LogType.Debug);
#endif
                try
                {
                    using (var writer = File.CreateText(filePath))
                    {
                        writer.WriteLineAsync('0');
                    }
                    infoFilePath = filePath;
                }
                catch(Exception e)
                {
                    LogHelper.Logln($"Error creating file {filePath}. Hence, SongCollection could not be constructed. Exception: {e.Message}.", LogType.Error);
                }
            }
        }
        private readonly StringDifferenceService diffService = new StringDifferenceService(2, 2, 1);
        /// <summary>
        ///
        /// </summary>
        /// <param name="input"></param>
        /// <returns>Song.Null if cannot find a song</returns>
        public async Task<OfflineSongInfo?> GetSongByTitleAsync(string inputTitle)
        {
            OfflineSongInfo? off;
            //TODO: If text cannot be read, the problem is here vvv
            using (StreamReader sr = new StreamReader(infoFilePath, false))
            {
                LUint totalLines;
                if (!TryGetTotalLines(sr, out totalLines))
                {
                    return null;
                }
#if !TRAINING
                string minDistanceObjStr = null;
                LUint minDistance = LUint.MaxValue;
                LUint objTimesPlayed = 0;
                LUint lineNum = 0;
                for (LUint i = 0; i < totalLines; i++)
                {
                    string songLine = await sr.ReadLineAsync();
                    string songTitle = SongCollectionFormatter.GetSongTitle(songLine);
                    LUint distance = diffService.DifferenceScore(inputTitle, songTitle);
                    if (distance < minDistance)
                    {
                        minDistanceObjStr = songLine;
                        objTimesPlayed = SongCollectionFormatter.GetTimesPlayed(songLine);
                        lineNum =  i;
                        if (distance == 0)
                        {
                            break;
                        }
                    }
                    else if (distance == minDistance)
                    {
                        LUint songTimesPlayed = SongCollectionFormatter.GetTimesPlayed(songLine);
                        if (songTimesPlayed > objTimesPlayed)
                        {
                            minDistanceObjStr = songLine;
                            objTimesPlayed = songTimesPlayed;
                            lineNum = i;
                        }
                    }
                }
                if (minDistanceObjStr is null)
                {
                    off = null;
                }
                else
                {
                    off = new OfflineSongInfo(SongCollectionFormatter.DeserializeString(minDistanceObjStr).Value, lineNum);
                }
#endif
            }
            return off;
        }
        public async Task<OfflineSongInfo?> GetSongByURLAsync(string url)
        {
#if DEBUG
            LogHelper.Logln("Initializing GetSongByURLAsync(url) in Song Collection", LogType.Debug);
#endif
            OfflineSongInfo? output = null;
            //TODO: If text cannot be read, the problem is here vvv
            using (StreamReader sr = new StreamReader(infoFilePath, false))
            {
                LUint totalLines;
                if (!TryGetTotalLines(sr, out totalLines))
                {
                    return null;
                }
#if DEBUG
                LogHelper.Logln($"There are a total of {totalLines} of songs.", LogType.Debug);
#endif
                for (LUint i = 0; i < totalLines; i++)
                {
                     string songLine = await sr.ReadLineAsync();
                    string oldUrl = SongCollectionFormatter.GetUrl(songLine);
#if DEBUG
                    LogHelper.Logln($"oldUrl = {oldUrl}", LogType.Debug);
#endif
                    if (oldUrl == url)
                    {
                        output = new OfflineSongInfo(SongCollectionFormatter.DeserializeString(songLine).Value, i);
                        break;
                    }
                    else if(i == totalLines -1)
                    {
                        //Last index
                        output = null;
                    }
                }
            }
            return output;
        }
        public async Task<OfflineSongInfo?> GetSongByFilePathAsync(string path)
        {
            OfflineSongInfo? output = null;
            //TODO: If text cannot be read, the problem is here vvv
            using (StreamReader sr = new StreamReader(infoFilePath, false))
            {
                LUint totalLines;
                if (!TryGetTotalLines(sr, out totalLines))
                {
                    return null;
                }
                for (LUint i = 0; i < totalLines; i++)
                {
                    string songLine = await sr.ReadLineAsync();
                    if (SongCollectionFormatter.GetFilePath(songLine) == path)
                    {
                        output = new OfflineSongInfo(SongCollectionFormatter.DeserializeString(songLine).Value, i);
                        break;
                    }
                    else if (i == totalLines - 1)
                    {
                        //Last index
                        output = null;
                    }
                }
            }
            return output;
        }
        private bool TryGetTotalLines(StreamReader sr, out LUint totalLines)
        {
            if (!LUint.TryParse(sr.ReadLine(), out totalLines))
            {
                LogHelper.Logln($"The file {infoFilePath} might not be in the format of Nameless song collection.", LogType.Error);
                return false;
            }
            return true;
        }
        /// <summary>
        /// Please make sure the infoFilePath exists
        /// </summary>
        /// <param name="song"></param>
        /// <returns></returns>
        public async Task<bool> TryAddSongAsync(OfflineSong song)
        {
            bool songAdded;
            using (StreamReader sr = File.OpenText(infoFilePath))
            using (StreamWriter sw = File.CreateText(infoFilePath + "temp"))
            {
                LUint totalLines;
                string line = await sr.ReadLineAsync();
                if (!LUint.TryParse(line, out totalLines))
                {
                    LogHelper.Logln($"{infoFilePath} is not encoded as expected.", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    sw.Close();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                if (totalLines == 0)
                {
                    await sw.WriteLineAsync('1');
                    await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(song));
                    sr.Close();
                    await sw.FlushAsync();
                    sw.Close();
                    File.Delete(infoFilePath);
                    File.Move(infoFilePath + "temp", infoFilePath);
                    return true;
                }
#if DEBUG
                LogHelper.Logln($"Total lines = {totalLines}", LogType.Warning);
#endif
                await sw.WriteLineAsync((totalLines+1).ToString());
                //We are now done with getting and writing the number of totalLines of the new file
                songAdded = false;
                for (int i = 0; i < totalLines; i++)
                {
                    string songLine = await sr.ReadLineAsync();
                    if (!songAdded)
                    {
#if DEBUG
                        LogHelper.Logln("!songAdded; i = " + i);
#endif
                        LUint songLineTimesPlayed = SongCollectionFormatter.GetTimesPlayed(songLine);
                        if (songLineTimesPlayed == song.timesPlayed)
                        {
                            //2»aaaaaaa...
                            //1»abcde»... //songLine
                            //1»bcdefeee»... //song.song
                            string songTitle = SongCollectionFormatter.GetSongTitle(songLine);
                            int minLength = Math.Min(song.song.Title.Length, songTitle.Length); //5, 8 => 5
                            for (int j = 0; j < minLength; j++)
                            {
                                if (song.song.Title[j] < songTitle[j])
                                    //if the adding song is less than the existed one alphabetically
                                {
                                    await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(song));
                                    songAdded = true;
                                    break;
                                }
                                else if (j == minLength - 1)
                                    //If the adding song is a modification of the existed one
                                {
                                    if (song.song.Title.Length != songTitle.Length) //the original or the new one might be a remix
                                    {
                                        //It does not matter anymore if the new title is an inserted of an old one
                                        await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(song));
                                        songAdded = true;
                                        break;
                                    }
                                    else //same title, exit
                                    {
                                        string songDuration = SongCollectionFormatter.GetDuration(songLine);
                                        //Check duration of both
                                        if (songDuration == song.song.Duration)
                                        {
                                            //Same song
                                            sr.Close();
                                            await sw.FlushAsync();
                                            sw.Close();
                                            File.Delete(infoFilePath + "temp");
                                            return true;
                                        }
                                        //The else case is automatically handled
                                    }
                                }
                                else if(song.song.Title[j] > songTitle[j])
                                {
                                        break;
                                }
                            }
                        }
                        else if (songLineTimesPlayed < song.timesPlayed)
                        {
                            await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(song));
                            songAdded = true;
                        }
                    }
                    await sw.WriteLineAsync(songLine);
                }
                if(!songAdded)
                {
                    //When you have eliminated the impossible, whatever remains, however improbable, must be the truth. 
                    //- Sherlock Holmes/Sir Arthur Conan Doyle
                    await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(song));
                    songAdded = true;
                }
            }
            #region Clean-up (works perfectly)
#if DEBUG
            LogHelper.Logln("Flushed sr and sw.", LogType.Success);
#endif
            try
            {
#if DEBUG
                if (songAdded)
                {
                    LogHelper.Logln("Since songAdded, delete old file, replace new file", LogType.Success);
#endif
                    File.Delete(infoFilePath);
                    File.Move(infoFilePath + "temp", infoFilePath);
#if DEBUG
                }
                else
                {
                    LogHelper.Logln("Since !songAdded, delete new file.", LogType.Success);
                    File.Delete(infoFilePath + "temp");
                }
#endif
            }
            catch (Exception e)
            {
                LogHelper.Logln($"Error while deleting ${infoFilePath} and replacing {infoFilePath} with {infoFilePath + "temp"}. {e.Message}", LogType.Error);
            }
#if DEBUG
            LogHelper.Logln("TryAddSongAsync algorithm got to the bottom of before returning.", LogType.Success);
#endif
            #endregion
            return songAdded;
        }
        public async Task<bool> TryReplaceSongAsync(OfflineSong oldSong, OfflineSong newSong)
        {
            bool replaced = false;
            using (StreamReader sr = File.OpenText(infoFilePath))
            using (StreamWriter sw = File.CreateText(infoFilePath + "temp"))
            {
                //Get total lines
                LUint totalLines;
                //Read and assign to totalLines
                if(!TryGetTotalLines(sr,out totalLines))
                {
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                //Write totalLines on new one
                await sw.WriteLineAsync(totalLines.ToString());
                string findingString = SongCollectionFormatter.SongFormatter(oldSong);
                for(LUint i =0;i < totalLines; i++)
                {
                    //Read new line
                    string songLine = await sr.ReadLineAsync();
                    if(songLine == findingString)
                    {
                        //Write new song if found
                        await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(newSong));
                        replaced = true;
                    }
                    else
                    {
                        //Write the line that was read if not found
                        await sw.WriteLineAsync(songLine);
                    }
                }
            }
            if(replaced)
            {
                File.Delete(infoFilePath);
                File.Move(infoFilePath + "temp", infoFilePath);
                return true;
            }
            else
            {
                File.Delete(infoFilePath + "temp");
                return false;
            }
        }
        public async Task<bool> TryReplaceSongAsync(OfflineSong newSong, LUint lineNo)
        {
            using (StreamWriter sw = File.CreateText(infoFilePath + "temp"))
            using (StreamReader sr = File.OpenText(infoFilePath))
            {
                LUint totalLines;
                string line = await sr.ReadLineAsync();
                if (!LUint.TryParse(line, out totalLines))
                {
                    LogHelper.Logln($"{infoFilePath} is not encoded as expected.", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                if (totalLines == 0)
                {
                    LogHelper.Logln($"Trying to change {infoFilePath}, which has 0 line of song in it!", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                if (totalLines < lineNo)
                {
                    LogHelper.Logln($"Trying to change line {lineNo} out of {totalLines} in {infoFilePath}!.", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                try
                {
                    await sw.WriteLineAsync(totalLines.ToString());
                    for (int i = 0; i < lineNo; i++)
                    {
                        await sw.WriteLineAsync(await sr.ReadLineAsync());
                    }
                    //Write the changing value
                    await sr.ReadLineAsync();
                    await sw.WriteLineAsync(
                        SongCollectionFormatter.SongFormatter(newSong));
                    for (; lineNo < totalLines; lineNo++)
                    {
                        await sw.WriteLineAsync(await sr.ReadLineAsync());
                    }
                }
                catch (Exception e)
                {
                    LogHelper.Logln($"Error occured while reading {infoFilePath} and writing {infoFilePath + "temp"}. {e.Message}", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
            }
            try
            {
                File.Delete(infoFilePath);
                File.Move(infoFilePath + "temp", infoFilePath);
            }
            catch (Exception e)
            {
                LogHelper.Logln($"Error while deleting ${infoFilePath} and replacing {infoFilePath} with {infoFilePath + "temp"}. {e.Message}", LogType.Error);
            }
            LogHelper.Logln($"Successfully rewrite times played and flushed all underlying streams.", LogType.Success);
            return true;
        }
        public async Task<bool> TryChangeTimesPlayed(LUint lineNo, OfflineSong offlineSong)
        {
            using (StreamWriter sw = File.CreateText(infoFilePath + "temp"))
            using (StreamReader sr = File.OpenText(infoFilePath))
            {
                LUint totalLines;
                string line = await sr.ReadLineAsync();
                #region Precautions
                if (!LUint.TryParse(line, out totalLines))
                {
                    LogHelper.Logln($"{infoFilePath} is not encoded as expected.", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                if (totalLines == 0)
                {
                    LogHelper.Logln($"Trying to change {infoFilePath}, which has 0 line of song in it!", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                if (totalLines < lineNo)
                {
                    LogHelper.Logln($"Trying to change line {lineNo} out of {totalLines} in {infoFilePath}!.", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                #endregion
                //totalLines is now assigned
                try
                {
                    await sw.WriteLineAsync(totalLines.ToString());
                    bool songRearranged = false;
                    for (int i = 0; i < lineNo; i++)
                    {
                        string songLine = await sr.ReadLineAsync();
                        if(!songRearranged)
                        {
                            if(SongCollectionFormatter.GetTimesPlayed(songLine) == offlineSong.timesPlayed)
                            {
                                int min = Math.Min(SongCollectionFormatter.GetSongTitle(songLine).Length
                                    , offlineSong.song.Title.Length);
                                for (int j = 0; j < min; j++)
                                {
                                    if (offlineSong.song.Title[j] < SongCollectionFormatter.GetSongTitle(songLine)[j] || j == min - 1)
                                    {
                                        songRearranged = true;
                                        await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(offlineSong));
                                        break;
                                    }
                                    else if (offlineSong.song.Title[j] > SongCollectionFormatter.GetSongTitle(songLine)[j])
                                    {
                                        break;
                                    }
                                }
                            }
                            else if(SongCollectionFormatter.GetTimesPlayed(songLine) < offlineSong.timesPlayed)
                            {
                                //We might want to re-arrange it if the song being changed the times played has
                                //a promotion of rank
                                songRearranged = true;
                                await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(offlineSong));
                            }
                        }
                        await sw.WriteLineAsync(songLine);
                    }
                    await sr.ReadLineAsync(); //Move to next line on the reader since we don't need to use it
                    if (!songRearranged) //If ranking does not change
                    {
                        await sw.WriteLineAsync(SongCollectionFormatter.SongFormatter(offlineSong));
                    }
                    for (; lineNo < totalLines; lineNo++) //DEBUNK THIS
                    {
                        await sw.WriteLineAsync(await sr.ReadLineAsync());
                    }
                }
                catch (Exception e)
                {
                    LogHelper.Logln($"Error occured while reading {infoFilePath} and writing {infoFilePath + "temp"}. {e.Message}", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
            }
            #region Clean-up
            try
            {
                File.Delete(infoFilePath);
                File.Move(infoFilePath + "temp", infoFilePath);
            }
            catch (Exception e)
            {
                LogHelper.Logln($"Error while deleting ${infoFilePath} and replacing {infoFilePath} with {infoFilePath + "temp"}. {e.Message}", LogType.Error);
            }
            LogHelper.Logln($"Successfully rewrite times played and flushed all underlying streams.", LogType.Success);
            #endregion
            return true;
        }
        [Obsolete]
        /// <summary>
        /// 
        /// </summary>
        /// <param name="lineNo"></param>
        /// <param name="newTimesPlayed"></param>
        /// <returns>Whether the program actually changed times played</returns>
        public async Task<bool> TryChangeTimesPlayed(LUint lineNo, LUint newTimesPlayed)
        {
            using (StreamWriter sw = File.CreateText(infoFilePath+"temp"))
            using (StreamReader sr = File.OpenText(infoFilePath))
            {
                LUint totalLines;
                string line = await sr.ReadLineAsync();
                if (!LUint.TryParse(line, out totalLines))
                {
                    LogHelper.Logln($"{infoFilePath} is not encoded as expected.", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                if (totalLines == 0)
                {
                    LogHelper.Logln($"Trying to change {infoFilePath}, which has 0 line of song in it!", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                if (totalLines < lineNo)
                {
                    LogHelper.Logln($"Trying to change line {lineNo} out of {totalLines} in {infoFilePath}!.", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }
                try
                {
                    await sw.WriteLineAsync(totalLines.ToString());
                    for (int i = 0; i < lineNo; i++)
                    {
                        await sw.WriteLineAsync(await sr.ReadLineAsync());
                    }
                    //Write the changing value
                    string changingString = await sr.ReadLineAsync();
                    await sw.WriteLineAsync(
                        SongCollectionFormatter.ChangeTimesPlayed(
                            changingString, newTimesPlayed));
                    for (; lineNo < totalLines; lineNo++)
                    {
                        await sw.WriteLineAsync(await sr.ReadLineAsync());
                    }
                }
                catch(Exception e)
                {
                    LogHelper.Logln($"Error occured while reading {infoFilePath} and writing {infoFilePath + "temp"}. {e.Message}", LogType.Error);
                    sr.Close();
                    await sw.FlushAsync();
                    File.Delete(infoFilePath + "temp");
                    return false;
                }                
            }
            try
            {
                File.Delete(infoFilePath);
                File.Move(infoFilePath + "temp", infoFilePath);
            }
            catch (Exception e)
            {
                LogHelper.Logln($"Error while deleting ${infoFilePath} and replacing {infoFilePath} with {infoFilePath + "temp"}. {e.Message}", LogType.Error);
            }
            LogHelper.Logln($"Successfully rewrite times played and flushed all underlying streams.", LogType.Success);
            return true;
        }
    }
    /// <summary>
    /// Format of an x.songcx file:
    /// { number of lines continues from here }
    /// "${timesPlayed}»{title}» {filePath}» {duration}» {url}" //Ordered alphabetically
    /// </summary>
    internal static class SongCollectionFormatter
    {
        internal const string extension = ".songcx";
        internal static string SongFormatter(OfflineSong s)
        {
            return $"{s.timesPlayed}»{s.song.Title}»{s.song.FilePath}»{s.song.Duration}»{s.song.Url}";
        }
        internal static OfflineSong? DeserializeString(string formatted)
        {
            if (formatted is null)
            {
                return null;
            }
            string title = "", path="", duration="";
            LUint timesPlayed = 0;
            sbyte symbolMet = 0;
            string buffer = "";
            for (int i = 0; i < formatted.Length; i++)
            {
                if (formatted[i] == '»')
                {
                    switch (symbolMet++)
                    {
                        case 0:
                            timesPlayed = LUint.Parse(buffer);
                            buffer = "";
                            break;
                        case 1:
                            title = buffer;
                            buffer = "";
                            break;
                        case 2:
                            path = buffer;
                            buffer = "";
                            break;
                        case 3:
                            duration = buffer;
                            buffer = "";
                            break;                        
                    }
                }
                else
                {
                    buffer += formatted[i];
                }
            }
            //buffer is url
            return new OfflineSong(path, title, duration, buffer, timesPlayed);
        }
        internal static LUint GetTimesPlayed(string songLine)
        {
            string timesPlayed = string.Empty;
            for (int i = 0; i < songLine.Length; i++)
            {
                if (songLine[i] != '»')
                {
                    timesPlayed += songLine[i];
                }
                else
                {
                    break;
                }
            }
            return LUint.Parse(timesPlayed);
        }
        internal static string ChangeTimesPlayed(string songLine, LUint newTimesPlayed)
        {
            string returnVal =newTimesPlayed.ToString();
            for (int i = GetTimesPlayed(songLine).ToString().Length; i < songLine.Length; i++)
            {
                returnVal += songLine[i];
            }
            return returnVal;
        }
        internal static string GetSongTitle(string songLine)
        {
            string title = string.Empty;
            sbyte symbolMet = 0;
            for (int i = 0; i < songLine.Length; i++)
            {
                if (songLine[i] == '»')
                {
                    if (++symbolMet < 2)
                    {
                        continue;
                    }
                    else
                        break;
                }
                else
                {
                    if (symbolMet == 1)
                    {
                        title += songLine[i];
                    }
                }
            }
            return title;
        }
        internal static string GetFilePath(string songLine)
        {
            string path = string.Empty;
            sbyte symbolMet = 0;
            for (int i = 0; i < songLine.Length; i++)
            {
                if (songLine[i] == '»')
                {
                    if (++symbolMet < 3)
                    {
                        continue;
                    }
                    else
                        break;
                }
                else
                {
                    if (symbolMet == 2)
                    {
                        path += songLine[i];
                    }
                }
            }
            return path;
        }
        internal static string GetDuration(string songLine)
        {
            string duration = string.Empty;
            sbyte symbolMet = 0;
            for (int i = songLine.Length - 1; i >= 0; i--)
            {
                if (songLine[i] == '»')
                {
                    if (++symbolMet == 2)
                        break;
                }
                else
                {
                    if (symbolMet == 1)
                    {
                        duration = songLine[i] + duration;
                    }
                }
            }
            return duration;
        }
        internal static string GetUrl(string songLine)
        {
            string url = string.Empty;
            for (int i = songLine.Length - 1; i >= 0; i--)
            {
                if (songLine[i] == '»')
                {
                    break;
                }
                else
                {
                    url = songLine[i] + url;
                }
            }
            return url;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">file path without extension</param>
        /// <param name="s"></param>
        /// <returns></returns>
        internal static async Task<bool> SerializeAsync(string path, OfflineSong[] cs, bool createFileIfNotExist = true)
        {
            path = path + extension;
            if (!createFileIfNotExist && !File.Exists(path))
            {
                LogHelper.Logln($"File {path} does not exist yet!", LogType.Error);
                return false;
            }
            try
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    await sw.WriteLineAsync(cs.Length.ToString());
                    string[] songStrings = new string[cs.Length];
                    for (int i = 0; i < songStrings.Length; i++)
                    {
                        songStrings[i] = SongFormatter(cs[i]);
                    }
                    Array.Sort(songStrings);
                    for (int i = 0; i < songStrings.Length; i++)
                    {
                        await sw.WriteLineAsync(songStrings[i]);
                    }
                }
                LogHelper.Logln($"Serialize song collection at {path} successfully.", LogType.Success);
                return true;
            }
            catch (Exception e)
            {
                LogHelper.Logln($"Error serializing song collection. {e.Message}\n{e.StackTrace}", LogType.Error);
                return false;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">file path without extension</param>
        /// <param name="s"></param>
        /// <returns></returns>
        internal static async Task<bool> SerializeSortedArrayAsync(string path, OfflineSong[] cs, bool createFileIfNotExist = true)
        {
            path = path + extension;
            if (!createFileIfNotExist && !File.Exists(path))
            {
                LogHelper.Logln($"File {path} does not exist yet!", LogType.Error);
                return false;
            }
            try
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    await sw.WriteLineAsync(cs.Length.ToString());
                    for (int i = 0; i < cs.Length; i++)
                    {
                        await sw.WriteLineAsync(SongFormatter(cs[i]));
                    }
                }
                LogHelper.Logln($"Serialize song collection at {path} successfully.", LogType.Success);
                return true;
            }
            catch (Exception e)
            {
                LogHelper.Logln($"Error serializing song collection. {e.Message}\n{e.StackTrace}", LogType.Error);
                return false;
            }
        }
    }
#if X
    internal class SongCollectionFormat: INamelessSerialized<SongCollection>
    {
        //{name}.songcx[prefix]
        //|
        // {how many lines will follow this line}
        // "${title}» {filePath}» {duration}» {url}" \n
        // {Next song}
        private
#if !DEBUG
            readonly
#endif
            string infoFilePath;
        internal const string extensionTail = ".songcx";
        private static string SerializationInfoFromSong(Song s)
        {
            return $"{s.Title}»{s.FilePath}»{s.Duration}»{s.Url}";
        }
        internal SongCollectionFormat(string infoFileName)
        {
            infoFilePath = Path.GetFileNameWithoutExtension(infoFileName)+extensionTail;
        }
        internal SongCollectionFormat(SongCollection songCollection)
        {
            infoFilePath = songCollection.infoFilePath;
        }
        public void OnStartUp()
        {
            if (!File.Exists(infoFilePath))
            {
                //First start
                OnFirstStart();
            }

        }
        public void OnFirstStart()
        {
            Dictionary<char, Song[]> prefixCharOfSongs = GetAllSongTitlesInDirectory(Path.GetDirectoryName(infoFilePath));
            foreach (var element in prefixCharOfSongs)
            {
                using (var newText = File.CreateText($"{infoFilePath}{element.Key}"))
                {
                    newText.WriteLine(element.Value.Length);
                    for (int i = 0; i < element.Value.Length; i++)
                    {
                        newText.WriteLine(element.Value[i]);
                    }
                }
            }
        }
        public void Serialize()
        {

        }
        public SongCollection Deserialize()
        {

        }
        /// <summary>
        /// Only used if user wants to serialize the whole collection from all over again.
        /// TODO: Implement an alphabetically categorized txt files
        /// </summary>
        /// <param name="filePathAndName"></param>
        /// <param name="songs">Song array that contains all songs that is being serialized</param>
        /// <param name="createDirectoryIfPathNotFound"></param>
        /// <returns>Serialization completed successfully</returns>
        internal bool SerializeOffline(string filePathAndName, Song[] songs, bool createDirectoryIfPathNotFound = false)
        {
#if TRACETIME
            BenchTime.beginNested();
#endif
            bool result = false;
            string output = "";
            LogType outputType = LogType.Error;
            TryAgain:
            try
            {
                using (StreamWriter sw = File.CreateText(Path.GetFullPath(filePathAndName) + extensionTail))
                {
                    sw.WriteLine(songs.Length);
                    for (int i = songs.Length; i >0; --i)
                    {
                        string songInfo = SerializationInfoFromSong(songs[i]);
                        sw.WriteLine(songInfo);
                    }
                    output = "Successfully serialized the song collection.";
                    outputType = LogType.Success;
                    result = true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                output += "Program does not have authorized access to create text, try running with Admin rights.";
            }
            catch (PathTooLongException)
            {
                output += "Input path is too long!";
            }
            catch (DirectoryNotFoundException)
            {
                if (createDirectoryIfPathNotFound)
                {
                    try
                    {
                        File.Create(filePathAndName + extensionTail);
                        goto TryAgain; //I am so sorry :(
                    }
                    catch (Exception e)
                    {
                        LogHelper.Logln($"Cannot create file at {filePathAndName + extensionTail}! {e.Message}", LogType.Error);
                        return false;
                    }
                }
                else
                {
                    output += "Directory not found! Pass in createDirectoryIfPathNotFound = true might simply solve the problem.";
                }
            }
            finally
            {
                LogHelper.Logln(output, outputType);
            }
#if TRACETIME
            BenchTime.SendNestedResult("Serialize song collection took ", "ms.");
#endif
            return result;
        }
        /// <summary>
        /// Only used to scan offline songs
        /// TODO: Implement a dictionary of restricted chars so that try/catch block is no longer needed.
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        internal Dictionary<char,Song[]> GetAllSongTitlesInDirectory(string directory)
        {
            directory = Path.GetDirectoryName(directory);
            Dictionary<char, Song[]> songTitlesStartsWith = new Dictionary<char, Song[]>();
#if DEBUG
            List<char> restrictedChars = new List<char>(char.MaxValue - 58); //58 = ('a' -> 'z') + ('A' -> 'Z') + ('0' -> '9')
            char c = char.MinValue;
            for (; c <= char.MaxValue; c++)
#else
            for(char c=char.MinValue; c<=char.MaxValue; c++)
#endif
            {
                try
                {
                    string[] songTitles = Directory.GetFiles(directory, c + "*.mp3");
                    Song[] songs = new Song[songTitles.Length];
                    for (int i = 0; i < songTitles.Length; i++)
                    {
                        FileInfo songFile = new FileInfo(songTitles[i]);
                        ProcessStartInfo Ffprobe = new ProcessStartInfo()
                        {
                            FileName = "ffprobe",
                            Arguments = $"fprobe -v quiet -print_format compact=print_section=0:nokey=1:escape=csv -show_entries format=duration \"{songFile.FullName}\"",
                        };
                        Process ffprobeProcess = Process.Start(Ffprobe);
                        string duration;
                        using (Stream output = ffprobeProcess.StandardOutput.BaseStream)
                        {
                            StreamReader outputReader = new StreamReader(output);
                            duration = outputReader.ReadToEnd();
                        }
                        Song thisSong = new Song(path: songFile.FullName, songFile.Name, duration, Song.offlineUrl);
                        songs[i] = thisSong;
                    }
                    songTitlesStartsWith[c] = songs;
                }
                catch (ArgumentException)
                {
                    //if file path contains restricted character
#if DEBUG
                    restrictedChars[restrictedChars.Count] = c;
#endif
                    continue;
                }
                catch (Exception e)
                {
                    LogHelper.Logln($"Failed to get songs in directory. {e.Message}", LogType.Error);
#if DEBUG
                    if (restrictedChars.Count > 0)
                        LogHelper.Logln($"Restricted chars: {restrictedChars.ToString()}", LogType.Debug);
#endif
                    return null;
                }
            }
#if DEBUG
            LogHelper.Logln($"Restricted chars: {string.Join(", ", restrictedChars)}",LogType.Debug);
#endif
            return songTitlesStartsWith;
        }
    }
#endif
}