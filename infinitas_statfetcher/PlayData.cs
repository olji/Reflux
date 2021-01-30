﻿using System;
using System.Collections.Generic;
using System.Text;

namespace infinitas_statfetcher
{
    class PlayData
    {
        DateTime timestamp;
        readonly Judge judges;
        readonly Settings settings;
        ChartInfo chart;
        int gauge;
        int ex;
        string songID, grade, clearLamp;
        public bool DataAvailable { get { return settings.DataAvailable; } }

        public PlayData()
        {
            judges = new Judge();
            settings = new Settings();
        }
        public void Fetch(Dictionary<string, SongInfo> songDb)
        {
            judges.Fetch(Offsets.JudgeData);
            settings.Fetch(Offsets.PlaySettings, judges.playtype);

            if (!settings.DataAvailable)
            {
                Console.WriteLine($"Eww, {(settings.Hran ? "H-RAN" : "Battle")}");
            }

            timestamp = DateTime.UtcNow;

            short word = 4;

            var song = Utils.ReadInt32(Offsets.PlayData, 0, word);
            var diffVal = Utils.ReadInt32(Offsets.PlayData, word, word);
            var lamp = Utils.ReadInt32(Offsets.PlayData, word * 6, word);
            gauge = Utils.ReadInt32(Offsets.PlayData, word * 8, word);

            songID = song.ToString("00000");

            chart = FetchChartInfo(songDb[songID], diffVal);


            var maxEx = songDb[songID].totalNotes[diffVal] * 2;
            var exPart = (double)maxEx / 9;

            var exscore = (judges.pgreat * 2 + judges.great);
            ex = exscore;

            if (exscore > exPart * 8)
            {
                grade = "AAA";
            }
            else if (exscore > exPart * 7)
            {
                grade = "AA";
            }
            else if (exscore > exPart * 6)
            {
                grade = "A";
            }
            else if (exscore > exPart * 5)
            {
                grade = "B";
            }
            else if (exscore > exPart * 4)
            {
                grade = "C";
            }
            else if (exscore > exPart * 3)
            {
                grade = "D";
            }
            else if (exscore > exPart * 2)
            {
                grade = "E";
            }
            else
            {
                grade = "F";
            }

            /* Lamp: 0-7, [noplay, fail, a-clear, e-clear, N, H, EX, FC] */
            switch (lamp)
            {
                case 0: clearLamp = "NP"; break;
                case 1: clearLamp = "F"; break;
                case 2: clearLamp = "AC"; break;
                case 3: clearLamp = "EC"; break;
                case 4: clearLamp = "NC"; break;
                case 5: clearLamp = "HC"; break;
                case 6: clearLamp = "EX"; break;
                case 7: clearLamp = "FC"; break;
            }
        }
        ChartInfo FetchChartInfo(SongInfo song, int diffVal)
        {
            ChartInfo result = new ChartInfo();
            /* Lamp: 0-7, [noplay, fail, a-clear, e-clear, N, H, EX, FC] */
            string diff = "";
            switch (diffVal)
            {
                case 0: diff = "SPB"; break;
                case 1: diff = "SPN"; break;
                case 2: diff = "SPH"; break;
                case 3: diff = "SPA"; break;
                case 4: diff = "SPL"; break;
                case 5: diff = "DPB"; break;
                case 6: diff = "DPN"; break;
                case 7: diff = "DPH"; break;
                case 8: diff = "DPA"; break;
                case 9: diff = "DPL"; break;
            }
            result.difficulty = diff;
            result.level = song.level[diffVal];
            result.title = song.title;
            result.title_english = song.title_english;
            result.totalNotes = song.totalNotes[diffVal];
            result.artist = song.artist;
            result.genre = song.genre;
            result.bpm = song.bpm;
            return result;

        }
        public Dictionary<string, string> ToPostForm()
        {
            return new Dictionary<string, string>
            {
                { "apikey", "apikey" },
                { "date", timestamp.ToString("s") },
                { "title", chart.title },
                { "title2", chart.title_english },
                { "bpm", chart.bpm },
                { "artist", chart.artist },
                { "genre", chart.genre },
                { "notecount", chart.totalNotes.ToString() },
                { "diff", chart.difficulty },
                { "level", chart.level.ToString() },
                { "grade", grade },
                { "gaugepercent", gauge.ToString() },
                { "lamp", clearLamp },
                { "exscore", ex.ToString() },
                { "pgreat", judges.pgreat.ToString() },
                { "great", judges.great.ToString() },
                { "good", judges.good.ToString() },
                { "bad", judges.bad.ToString() },
                { "poor", judges.poor.ToString() },
                { "fast", judges.fast.ToString() },
                { "slow", judges.slow.ToString() },
                { "combobreak", judges.combobreak.ToString() },
                { "playtype", judges.playtype.ToString() },
                { "style", settings.style.ToString() },
                { "style2", settings.style2.ToString() },
                { "gauge", settings.gauge.ToString() },
                { "assist", settings.assist.ToString() },
                { "range", settings.range.ToString() },
            };

        }
        public string GetTsvEntry()
        {
            StringBuilder sb = new StringBuilder($"{chart.title}\t{chart.difficulty}");
            if (Config.HeaderConfig.songInfo)
            {
            sb.Append($"\t{chart.title_english}\t{chart.bpm}\t{chart.artist}\t{chart.genre}");
            }
            if (Config.HeaderConfig.chartDetails)
            {
                sb.Append($"\t{chart.totalNotes}\t{chart.level}");
            }
            sb.Append($"\t{judges.playtype}\t{grade}\t{clearLamp}");
            if (Config.HeaderConfig.resultDetails)
            {
                sb.Append($"\t{gauge}\t{ex}");
            }
            if (Config.HeaderConfig.judge)
            {
                sb.Append($"\t{judges.pgreat}\t{judges.great}\t{judges.good}\t{judges.bad}\t{judges.poor}\t{judges.combobreak}\t{judges.fast}\t{judges.slow}");
            }
            if (Config.HeaderConfig.settings)
            {
                sb.Append($"\t{settings.style}\t{settings.style2}\t{settings.gauge}\t{settings.assist}\t{settings.range}");
            }
            sb.Append($"\t{timestamp}");
            return sb.ToString();
        }
        struct ChartInfo
        {
            public int totalNotes;
            public int level;
            public string title;
            public string title_english;
            public string difficulty;
            public string artist;
            public string genre;
            public string bpm;
        }
    }
}