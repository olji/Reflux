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
                    case "playsettings": PlaySettings = offset; break;
                }
            }
            Console.WriteLine("Offsets loaded:");
            Console.WriteLine($"SongList: {SongList.ToString("X")}");
            Console.WriteLine($"Playcard: {PlayData.ToString("X")}");
            Console.WriteLine($"Judgeinfo: {JudgeData.ToString("X")}");
            Console.WriteLine($"Playsettings: {PlaySettings.ToString("X")}");
        }
    }
}
