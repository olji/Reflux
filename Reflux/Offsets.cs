using System;
using System.IO;
using System.Text;

namespace Reflux
{

    public class OffsetsCollection
    {
        public long SongList { get; set; }
        public long DataMap { get; set; }
        public long JudgeData { get; set; }
        public long PlayData { get; set; }
        public long PlaySettings { get; set; }
        public long UnlockData { get; set; }
        public long CurrentSong { get; set; }
    }
    static class Offsets
    {
        static OffsetsCollection offsets;
        /// <summary>
        /// Version these offsets are valid for
        /// </summary>
        public static string Version { get; private set; }
        /// <summary>
        /// Location of songlist
        /// </summary>
        public static long SongList { get { return offsets.SongList; } }
        /// <summary>
        /// Location of the pointers to the hashmap for player top score data
        /// </summary>
        public static long DataMap { get { return offsets.DataMap; } }
        /// <summary>
        /// Location of judge data
        /// </summary>
        public static long JudgeData { get { return offsets.JudgeData; } }
        /// <summary>
        /// Location of play data (song, difficulty, ex score, lamp)
        /// </summary>
        public static long PlayData { get { return offsets.PlayData; } }
        /// <summary>
        /// Location of play settings
        /// </summary>
        public static long PlaySettings { get { return offsets.PlaySettings; } }
        /// <summary>
        /// Location of the unlock data array
        /// </summary>
        public static long UnlockData { get { return offsets.UnlockData; } }
        /// <summary>
        /// Location where information of currently playing song and difficulty is found
        /// </summary>
        public static long CurrentSong { get { return offsets.CurrentSong; } }

        public static void LoadOffsets()
        {
            var lines = File.ReadAllLines("offsets.txt");
            var newoffsets = new OffsetsCollection();
            for (int i = 0; i < lines.Length; i++)
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
                    case "judgedata": newoffsets.JudgeData = offset; break;
                    case "playdata": newoffsets.PlayData = offset; break;
                    case "songlist": newoffsets.SongList = offset; break;
                    case "unlockdata": newoffsets.UnlockData = offset; break;
                    case "datamap": newoffsets.DataMap = offset; break;
                    case "playsettings": newoffsets.PlaySettings = offset; break;
                    case "currentsong": newoffsets.CurrentSong = offset; break;
                }
            }
            Apply(Version, newoffsets);
            Utils.Debug("Offsets loaded:");
            Utils.Debug($"SongList: {SongList:X}");
            Utils.Debug($"UnlockData: {UnlockData:X}");
            Utils.Debug($"Playcard: {PlayData:X}");
            Utils.Debug($"Judgeinfo: {JudgeData:X}");
            Utils.Debug($"Playsettings: {PlaySettings:X}");
            Utils.Debug($"CurrentSong: {CurrentSong:X}");
        }
        private static void Apply(string version, OffsetsCollection newOffsets)
        {
            Version = version;
            offsets = newOffsets;
        }
        public static void SaveOffsets(string version, OffsetsCollection newOffsets)
        {
            if (!Directory.Exists("archive"))
            {
                Directory.CreateDirectory("archive");
            }
            File.Move("offsets.txt", $"archive/{Version.Replace(':', '_')}.txt", true);

            Apply(version, newOffsets);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(version);
            sb.AppendLine($"songList = 0x{newOffsets.SongList:X}");
            sb.AppendLine($"unlockdata = 0x{newOffsets.UnlockData:X}");
            sb.AppendLine($"playSettings = 0x{newOffsets.PlaySettings:X}");
            sb.AppendLine($"playData = 0x{newOffsets.PlayData:X}");
            sb.AppendLine($"currentsong = 0x{newOffsets.CurrentSong:X}");
            sb.AppendLine($"judgeData = 0x{newOffsets.JudgeData:X}");
            sb.AppendLine($"datamap = 0x{newOffsets.DataMap:X}");

            File.WriteAllText("offsets.txt", sb.ToString());

        }
    }
}
