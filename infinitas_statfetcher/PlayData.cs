using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace infinitas_statfetcher
{
    struct ChartInfo
    {
        public int totalNotes;
        public int level;
        public bool unlocked;
        public string title;
        public string title_english;
        public string difficulty;
        public string artist;
        public string genre;
        public string bpm;
        public string songid;
    }
    class PlayData
    {
        DateTime timestamp;
        readonly Judge judges;
        readonly Settings settings;
        ChartInfo chart;
        int gauge;
        int ex;
        string songID, grade, clearLamp;
        public bool DataAvailable { get; private set; }
        public bool PrematureEnd { get { return judges.prematureEnd; } }
        public string ClearState { get { return clearLamp; } }
        public int JudgedNotes { get { return judges.notesJudged; } }
        public bool MissCountValid { get { return (DataAvailable && !PrematureEnd && settings.assist == "OFF"); } }

        public PlayData()
        {
            judges = new Judge();
            settings = new Settings();
        }
        public void Fetch()
        {

            judges.Fetch(Offsets.JudgeData, Offsets.NotesProgress);
            settings.Fetch(Offsets.PlaySettings, judges.playtype);

            timestamp = DateTime.UtcNow;

            short word = 4;

            int diffVal = 0;
            try
            {
                var song = Utils.ReadInt32(Offsets.PlayData, 0, word);
                diffVal = Utils.ReadInt32(Offsets.PlayData, word, word);
                var lamp = Utils.ReadInt32(Offsets.PlayData, word * 6, word);
                gauge = Utils.ReadInt32(Offsets.PlayData, word * 8, word);

                songID = song.ToString("00000");
                clearLamp = Utils.IntToLamp(lamp);
                chart = FetchChartInfo(Utils.songDb[songID], diffVal);
                DataAvailable = true;
            }
            catch
            {
                Console.WriteLine("Unable to fetch play data, using currentplaying value instead");
                var currentChart = Utils.FetchCurrentChart();
                songID = currentChart.id;
                diffVal = currentChart.diff;
                clearLamp = "NA"; /* What clear lamp should be given on stuff not sent to server? */
                chart = FetchChartInfo(Utils.songDb[songID], diffVal);
                DataAvailable = false;
            }



            var maxEx = Utils.songDb[songID].totalNotes[diffVal] * 2;
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

        }
        ChartInfo FetchChartInfo(SongInfo song, int diffVal)
        {
            ChartInfo result = new ChartInfo();
            /* Lamp: 0-7, [noplay, fail, a-clear, e-clear, N, H, EX, FC] */
            result.difficulty = Utils.IntToDiff(diffVal);
            result.level = song.level[diffVal];
            result.title = song.title;
            result.title_english = song.title_english;
            result.totalNotes = song.totalNotes[diffVal];
            result.artist = song.artist;
            result.genre = song.genre;
            result.bpm = song.bpm;
            result.songid = song.ID;
            result.unlocked = Utils.GetUnlockStateForDifficulty(song.ID, diffVal);
            return result;

        }
        public Dictionary<string, string> ToPostForm()
        {
            return new Dictionary<string, string>
            {
                { "apikey", Config.API_key },
                { "songid", chart.songid },
                { "title", chart.title },
                { "title2", chart.title_english },
                { "bpm", chart.bpm },
                { "artist", chart.artist },
                { "genre", chart.genre },
                { "notecount", chart.totalNotes.ToString() },
                { "diff", chart.difficulty },
                { "level", chart.level.ToString() },
                { "unlocked", chart.unlocked.ToString() },
                { "grade", grade },
                { "gaugepercent", gauge.ToString() },
                { "lamp", clearLamp },
                { "exscore", ex.ToString() },
                { "notesjudged", judges.notesJudged.ToString() },
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
        public JObject GetJsonEntry()
        {
            var kamaiTask = Network.Kamai_GetSongID(chart.title_english);
            kamaiTask.Wait();
            var kamaiID = kamaiTask.Result;
            JObject json = new JObject();
            json["score"] = ex;
            json["lamp"] = expandLamp(clearLamp);
            if (kamaiID == null)
            {
                json["matchType"] = "title";
                json["identifier"] = chart.title;
            }
            else
            {
                json["matchType"] = "songID";
                json["identifier"] = kamaiID;
            }
            json["playtype"] = chart.difficulty.Substring(0, 2);
            json["difficulty"] = expandDifficulty(chart.difficulty.Substring(2, 1));
            json["timeAchieved"] = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds();
            json["hitData"] = new JObject();
            json["hitData"]["pgreat"] = judges.pgreat;
            json["hitData"]["great"] = judges.great;
            json["hitData"]["good"] = judges.good;
            json["hitData"]["bad"] = judges.bad;
            json["hitData"]["poor"] = judges.poor;
            json["hitMeta"] = new JObject();
            json["hitMeta"]["fast"] = judges.fast;
            json["hitMeta"]["slow"] = judges.slow;
            json["hitMeta"]["comboBreak"] = judges.combobreak;
            json["hitMeta"]["gauge"] = gauge;
            if (MissCountValid)
            {
                json["hitMeta"]["bp"] = judges.bad + judges.poor;
            }
            return json;
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
            sb.Append($"\t{judges.playtype}\t{grade}\t{clearLamp}\t{(MissCountValid ? (judges.poor+judges.bad).ToString() : "-")}");
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
        string expandDifficulty(string diff)
        {
            switch (diff)
            {
                case "B": return "BEGINNER";
                case "N": return "NORMAL";
                case "H": return "HYPER";
                case "A": return "ANOTHER";
                case "L": return "LEGGENDARIA";
            }
            throw new Exception($"Unexpected difficulty character {diff}");
        }
        string expandLamp(string lamp)
        {
            switch (lamp)
            {
                case "NP": return "NO PLAY";
                case "F": return "FAILED";
                case "AC": return "ASSIST CLEAR";
                case "EC": return "EASY CLEAR";
                case "NC": return "CLEAR";
                case "HC": return "HARD CLEAR";
                case "EX": return "EX HARD CLEAR";
                case "FC": return "FULL COMBO";
            }
            throw new Exception($"Unexpected lamp code {lamp}");
        }
    }
}
