using System;
using System.IO;

namespace infinitas_statfetcher
{
    static class Offsets
    {
        /// <summary>
        /// Version these offsets are valid for
        /// </summary>
        public static string Version { get; private set; }
        /// <summary>
        /// Location of songlist
        /// </summary>
        public static long SongList { get; private set; }
        /// <summary>
        /// Location of the pointers to the hashmap for player top score data
        /// </summary>
        public static long DataMap { get; private set; }
        /// <summary>
        /// Location of judge data
        /// </summary>
        public static long JudgeData {get; private set; }
        /// <summary>
        /// Location of play data (song, difficulty, ex score, lamp)
        /// </summary>
        public static long PlayData { get; private set; }
        /// <summary>
        /// Location of play settings
        /// </summary>
        public static long PlaySettings { get; private set; }
        /// <summary>
        /// Location of the unlock data array
        /// </summary>
        public static long UnlockData { get; private set; }
        /// <summary>
        /// Location where information of currently playing song and difficulty is found
        /// </summary>
        public static long CurrentSong { get; private set; }

        public static void LoadOffsets(string filename)
        {
            var lines = File.ReadAllLines(filename);
            for(int i = 0; i < lines.Length; i++)
            {
                if (i == 0)
                {
                    Version = lines[i];
                    continue;
                }

                var line = lines[i];
                var sections = line.Split('=');
                sections[0] = sections[0].Trim();
                sections[1] = sections[1].Trim();
                var offset = Convert.ToInt64(sections[1], 16);
                switch (sections[0].ToLower())
                {
                    case "judgedata": JudgeData = offset; break;
                    case "playdata": PlayData = offset; break;
                    case "songlist": SongList = offset; break;
                    case "unlockdata": UnlockData = offset; break;
                    case "datamap": DataMap = offset; break;
                    case "playsettings": PlaySettings = offset; break;
                    case "currentsong": CurrentSong = offset; break;
                }
            }
            Utils.Debug("Offsets loaded:");
            Utils.Debug($"SongList: {SongList:X}");
            Utils.Debug($"UnlockData: {UnlockData:X}");
            Utils.Debug($"Playcard: {PlayData:X}");
            Utils.Debug($"Judgeinfo: {JudgeData:X}");
            Utils.Debug($"Playsettings: {PlaySettings:X}");
            Utils.Debug($"CurrentSong: {CurrentSong:X}");
        }
    }
}
