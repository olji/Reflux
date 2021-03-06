using System;
using System.IO;

namespace infinitas_statfetcher
{
    static class Offsets
    {
        public static string Version { get; private set; }
        public static long SongList { get; private set; }
        public static long JudgeData {get; private set; }
        public static long PlayData { get; private set; }
        public static long PlaySettings { get; private set; }
        public static long UnlockData { get; private set; }
        public static long NotesProgress { get; private set; }

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
                    case "playsettings": PlaySettings = offset; break;
                    case "notesprogress": NotesProgress = offset; break;
                }
            }
            Utils.Debug("Offsets loaded:");
            Utils.Debug($"SongList: {SongList:X}");
            Utils.Debug($"UnlockData: {UnlockData:X}");
            Utils.Debug($"Playcard: {PlayData:X}");
            Utils.Debug($"Judgeinfo: {JudgeData:X}");
            Utils.Debug($"Playsettings: {PlaySettings:X}");
            Utils.Debug($"Notesprogress: {NotesProgress:X}");
        }
    }
}
