using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reflux
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
        int exscore;
        string songID;
        Grade grade;
        Lamp clearLamp;
        /// <summary>
        /// False if play data isn't available (H-RAN, BATTLE or assist options enabled)
        /// </summary>
        public bool DataAvailable { get; private set; }
        public ChartInfo Chart { get { return chart; } }
        public Grade Grade { get { return grade; } }
        public Lamp Lamp { get { return clearLamp; } }
        public uint MissCount { get { return (uint)(judges.bad + judges.poor); } }
        public int ExScore { get { return exscore; } }
        /// <summary>
        /// Play ended prematurely (Quit or failed HC/EXH or 50 consecutive misses in Normal Gauge)
        /// </summary>
        public bool PrematureEnd { get { return judges.prematureEnd; } }
        /// <summary>
        /// True if miss count shouldn't be calculated and saved (When it's not shown in the result screens essentially)
        /// </summary>
        public bool MissCountValid { get { return (DataAvailable && !PrematureEnd && settings.assist == "OFF"); } }
        public string Gauge { get { return settings.gauge; } }

        public PlayData()
        {
            judges = new Judge();
            settings = new Settings();
        }
        /// <summary>
        /// Fetch all data relating to the recent play
        /// </summary>
        public void Fetch()
        {

            judges.Fetch();
            settings.Fetch(judges.playtype);

            timestamp = DateTime.UtcNow;

            short word = 4;

            Difficulty difficulty;
            try
            {
                var song = Utils.ReadInt32(Offsets.PlayData, 0);
                difficulty = (Difficulty)Utils.ReadInt32(Offsets.PlayData, word);
                clearLamp = (Lamp)Utils.ReadInt32(Offsets.PlayData, word * 6);
                gauge = Utils.ReadInt32(Offsets.JudgeData, word * 81) + Utils.ReadInt32(Offsets.JudgeData, word * 82); /* It's not in the place near the play data
                                                                                                                        * Since the gauge percent in judge data is separate for P1 and P2
                                                                                                                        * they need to be merged */

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

            if (judges.PFC && clearLamp == Lamp.FC)
            {
                clearLamp = Lamp.PFC;
            }

            exscore = (judges.pgreat * 2 + judges.great);

            grade = Utils.ScoreToGrade(songID, difficulty, exscore);

        }
        /// <summary>
        /// Fetch all metadata for a specific chart
        /// </summary>
        /// <param name="song"></param>
        /// <param name="difficulty"></param>
        /// <returns></returns>
        ChartInfo FetchChartInfo(SongInfo song, Difficulty difficulty)
        {
            ChartInfo result = new ChartInfo
            {
                /* Lamp: 0-7, [noplay, fail, a-clear, e-clear, N, H, EX, FC] */
                difficulty = difficulty,
                level = song.level[(int)difficulty],
                title = song.title,
                title_english = song.title_english,
                totalNotes = song.totalNotes[(int)difficulty],
                artist = song.artist,
                genre = song.genre,
                bpm = song.bpm,
                songid = song.ID,
                unlocked = Utils.GetUnlockStateForDifficulty(song.ID, difficulty)
            };
            return result;

        }
        /// <summary>
        /// Generate and return a post form to send to remote
        /// </summary>
        /// <returns></returns>
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
                { "unlockType", Utils.unlockDb[chart.songid].type.ToString() },
                { "notecount", chart.totalNotes.ToString() },
                { "diff", chart.difficulty.ToString() },
                { "level", chart.level.ToString() },
                { "unlocked", chart.unlocked.ToString() },
                { "grade", grade.ToString() },
                { "gaugepercent", gauge.ToString() },
                { "lamp", clearLamp.ToString() },
                { "exscore", exscore.ToString() },
                { "prematureend", judges.prematureEnd.ToString().ToLower() },
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
        /// <summary>
        /// Generate and return a JSON entry to save to the json document
        /// </summary>
        /// <returns></returns>
        public JObject GetJsonEntry()
        {
            JObject json = new JObject
            {
                ["score"] = exscore,
                ["lamp"] = ExpandLamp(clearLamp),
                ["matchType"] = "title",
                ["identifier"] = chart.title,
                ["playtype"] = chart.difficulty.ToString().Substring(0, 2),
                ["difficulty"] = ExpandDifficulty(chart.difficulty.ToString().Substring(2, 1)),
                ["timeAchieved"] = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds(),
            };
            json["hitData"] = new JObject
            {
                ["pgreat"] = judges.pgreat,
                ["great"] = judges.great,
                ["good"] = judges.good,
                ["bad"] = judges.bad,
                ["poor"] = judges.poor
            };
            json["hitMeta"] = new JObject
            {
                ["fast"] = judges.fast,
                ["slow"] = judges.slow,
                ["comboBreak"] = judges.combobreak,
                ["gauge"] = gauge
            };
            if (MissCountValid)
            {
                json["hitMeta"]["bp"] = judges.bad + judges.poor;
            }
            return json;
        }
        /// <summary>
        /// Generate and return an entry to save to the session tsv
        /// </summary>
        /// <returns></returns>
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
            sb.Append($"\t{judges.playtype}\t{grade}\t{clearLamp}\t{(MissCountValid ? (judges.poor + judges.bad).ToString() : "-")}");
            if (Config.HeaderConfig.resultDetails)
            {
                sb.Append($"\t{gauge}\t{exscore}");
            }
            if (Config.HeaderConfig.judge)
            {
                sb.Append($"\t{judges.pgreat}\t{judges.great}\t{judges.good}\t{judges.bad}\t{judges.poor}\t{judges.combobreak}\t{judges.fast}\t{judges.slow}");
            }
            if (Config.HeaderConfig.settings)
            {
                sb.Append($"\t{settings.style}\t{settings.style2}\t{settings.gauge}\t{settings.assist}\t{settings.range}");
            }
            sb.Append($"\t{timestamp.ToLocalTime():yyyy/MM/dd HH:mm:ss}");
            return sb.ToString();
        }
        /// <summary>
        /// Expand difficulty abbreviations to full string
        /// </summary>
        /// <param name="diff">Abbreviated difficulty void of SP or DP</param>
        /// <returns></returns>
        string ExpandDifficulty(string diff)
        {
            return diff switch
            {
                "B" => "BEGINNER",
                "N" => "NORMAL",
                "H" => "HYPER",
                "A" => "ANOTHER",
                "L" => "LEGGENDARIA",
                _ => throw new NotImplementedException($"Unhandled difficulty string code {diff}"),
            };
            throw new Exception($"Unexpected difficulty character {diff}");
        }
        /// <summary>
        /// Expand lamp abbreviations to full strings
        /// </summary>
        /// <param name="lamp"></param>
        /// <returns></returns>
        public string ExpandLamp(Lamp lamp)
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
