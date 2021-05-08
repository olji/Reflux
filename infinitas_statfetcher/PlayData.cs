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
        public Difficulty difficulty;
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
        string songID;
        Grade grade;
        Lamp clearLamp;
        public bool DataAvailable { get; private set; }
        public ChartInfo Chart { get { return chart; } }
        public Grade Grade { get { return grade; } }
        public Lamp Lamp { get { return clearLamp; } }
        public bool PrematureEnd { get { return judges.prematureEnd; } }
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

            Difficulty difficulty = 0;
            try
            {
                var song = Utils.ReadInt32(Offsets.PlayData, 0, word);
                difficulty = (Difficulty)Utils.ReadInt32(Offsets.PlayData, word, word);
                clearLamp = (Lamp)Utils.ReadInt32(Offsets.PlayData, word * 6, word);
                gauge = Utils.ReadInt32(Offsets.PlayData, word * 8, word);

                songID = song.ToString("00000");
                chart = FetchChartInfo(Utils.songDb[songID], difficulty);
                DataAvailable = true;
            }
            catch
            {
                Console.WriteLine("Unable to fetch play data, using currentplaying value instead");
                var currentChart = Utils.FetchCurrentChart();
                songID = currentChart.songID;
                difficulty = currentChart.difficulty;
                clearLamp = Lamp.AC; /* What clear lamp should be given on stuff not sent to server? */
                chart = FetchChartInfo(Utils.songDb[songID], difficulty);
                DataAvailable = false;
            }

            if (judges.PFC)
            {
                clearLamp = Lamp.PFC;
            }



            var maxEx = Utils.songDb[songID].totalNotes[(int)difficulty] * 2;
            var exPart = (double)maxEx / 9;

            var exscore = (judges.pgreat * 2 + judges.great);
            ex = exscore;

            if (exscore > exPart * 8)
            {
                grade = Grade.AAA;
            }
            else if (exscore > exPart * 7)
            {
                grade = Grade.AA;
            }
            else if (exscore > exPart * 6)
            {
                grade = Grade.A;
            }
            else if (exscore > exPart * 5)
            {
                grade = Grade.B;
            }
            else if (exscore > exPart * 4)
            {
                grade = Grade.C;
            }
            else if (exscore > exPart * 3)
            {
                grade = Grade.D;
            }
            else if (exscore > exPart * 2)
            {
                grade = Grade.E;
            }
            else
            {
                grade = Grade.F;
            }

        }
        ChartInfo FetchChartInfo(SongInfo song, Difficulty diff)
        {
            ChartInfo result = new ChartInfo();
            /* Lamp: 0-7, [noplay, fail, a-clear, e-clear, N, H, EX, FC] */
            result.difficulty = diff;
            result.level = song.level[(int)diff];
            result.title = song.title;
            result.title_english = song.title_english;
            result.totalNotes = song.totalNotes[(int)diff];
            result.artist = song.artist;
            result.genre = song.genre;
            result.bpm = song.bpm;
            result.songid = song.ID;
            result.unlocked = Utils.GetUnlockStateForDifficulty(song.ID, diff);
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
                { "diff", chart.difficulty.ToString() },
                { "level", chart.level.ToString() },
                { "unlocked", chart.unlocked.ToString() },
                { "grade", grade.ToString() },
                { "gaugepercent", gauge.ToString() },
                { "lamp", clearLamp.ToString() },
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
            json["playtype"] = chart.difficulty.ToString().Substring(0, 2);
            json["difficulty"] = expandDifficulty(chart.difficulty.ToString().Substring(2, 1));
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
        string expandLamp(Lamp lamp)
        {
            switch (lamp)
            {
                case Lamp.NP: return "NO PLAY";
                case Lamp.F: return "FAILED";
                case Lamp.AC: return "ASSIST CLEAR";
                case Lamp.EC: return "EASY CLEAR";
                case Lamp.NC: return "CLEAR";
                case Lamp.HC: return "HARD CLEAR";
                case Lamp.EX: return "EX HARD CLEAR";
                case Lamp.FC:
                case Lamp.PFC: return "FULL COMBO";
            }
            throw new Exception($"Unexpected lamp {lamp}");
        }
    }
}
