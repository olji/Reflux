using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Reflux
{
    static class Tracker
    {
        /// <summary>
        /// DB for keeping track of PBs on different charts
        /// </summary>
        public static Dictionary<Chart, TrackerInfo> trackerDb = new Dictionary<Chart, TrackerInfo>();
        /// <summary>
        /// Save tracker tsv and unlockdb, as they're somewhat interlinked due to the unlock information in both
        /// </summary>
        /// <param name="filename"></param>
        public static void SaveTrackerData(string filename)
        {

            try
            {
                StringBuilder sb = new StringBuilder();
                StringBuilder db = new StringBuilder();
                sb.AppendLine("title\tType\tLabel\tCost Normal\tCost Hyper\tCost Another\tSP DJ Points\tDP DJ Points\t" +
                    "SPB Unlocked\tSPB Rating\tSPB Lamp\tSPB Letter\tSPB EX Score\tSPB Miss Count\tSPB Note Count\tSPB DJ Points\t" +
                    "SPN Unlocked\tSPN Rating\tSPN Lamp\tSPN Letter\tSPN EX Score\tSPN Miss Count\tSPN Note Count\tSPN DJ Points\t" +
                    "SPH Unlocked\tSPH Rating\tSPH Lamp\tSPH Letter\tSPH EX Score\tSPH Miss Count\tSPH Note Count\tSPH DJ Points\t" +
                    "SPA Unlocked\tSPA Rating\tSPA Lamp\tSPA Letter\tSPA EX Score\tSPA Miss Count\tSPA Note Count\tSPA DJ Points\t" +
                    "SPL Unlocked\tSPL Rating\tSPL Lamp\tSPL Letter\tSPL EX Score\tSPL Miss Count\tSPL Note Count\tSPL DJ Points\t" +
                    "DPN Unlocked\tDPN Rating\tDPN Lamp\tDPN Letter\tDPN EX Score\tDPN Miss Count\tDPN Note Count\tDPN DJ Points\t" +
                    "DPH Unlocked\tDPH Rating\tDPH Lamp\tDPH Letter\tDPH EX Score\tDPH Miss Count\tDPH Note Count\tDPH DJ Points\t" +
                    "DPA Unlocked\tDPA Rating\tDPA Lamp\tDPA Letter\tDPA EX Score\tDPA Miss Count\tDPA Note Count\tDPA DJ Points\t" +
                    "DPL Unlocked\tDPL Rating\tDPL Lamp\tDPL Letter\tDPL EX Score\tDPL Miss Count\tDPL Note Count\tDPL DJ Points");

                foreach (var entry in GetTrackerEntries())
                {
                    sb.AppendLine(entry);
                }
                File.WriteAllText(filename, sb.ToString());
                if (Config.Save_remote)
                {
                    foreach (var song in Utils.unlockDb)
                    {
                        db.AppendLine($"{song.Key},{(int)song.Value.type},{song.Value.unlocks}");
                    }
                    File.WriteAllText("unlockdb", db.ToString());
                }
            } catch (Exception e)
            {
                Utils.Except(e);
            }
        }
        /// <summary>
        /// Get each song entry for the tracker TSV
        /// </summary>
        /// <returns>Lazily evaluated list of entries</returns>
        static IEnumerable<string> GetTrackerEntries()
        {
            foreach(var songid in trackerDb.Keys.Select(x => x.songID).Distinct())
            {
                if (!Utils.unlockDb.ContainsKey(songid)) { continue; }
                var song = Utils.unlockDb[songid];
                string identifier = Utils.customTypes.ContainsKey(songid) ? Utils.customTypes[songid] : song.type.ToString();

                StringBuilder sb = new StringBuilder($"{Utils.songDb[songid].title}\t{song.type}\t{identifier}\t");

                StringBuilder bitCostData = new StringBuilder();
                StringBuilder chartData = new StringBuilder();
                decimal totalDJP = 0;
                decimal SPD = 0;
                decimal DPD = 0;
                bool sp_counted = false;
                bool dp_counted = false;
                for(int i = 0; i < 10; i++)
                {
                    /* Skip DPB as it doesn't exist */
                    if(i == (int)Difficulty.DPB) { continue; }
                    
                    Chart chart = new Chart() { songID = songid, difficulty = (Difficulty)i };
                    /* Handle columns for missing charts */
                    if (!trackerDb.ContainsKey(chart))
                    {
                        if (i > (int)Difficulty.SPB && i < (int)Difficulty.SPL)
                        {
                            /* Add tab for bit cost */
                            bitCostData.Append($"\t");
                        }
                        /* Add tabs for data columns below */
                        chartData.Append("\t\t\t\t\t\t\t\t");
                    }
                    else
                    {
                        decimal djp = ScoreMap.Scores[chart.songID].DJPoints[i];
                        djp = Decimal.Equals(ScoreMap.Scores[chart.songID].DJPoints
                            .Skip((i > 4) ? 5 : 0) // Skip SP difficulties if DP
                            .Take(5).Max(), djp) // If max DJP of SP/DP difficulties = djp...
                            ? djp
                            : 0;
                        totalDJP += djp;
                        string djp_str;
                        if (!Decimal.Equals(djp,0) && ((!sp_counted && i < 6) || (!dp_counted && i > 5)))
                        {
                            djp_str = $"{djp.ToString("E08", CultureInfo.CreateSpecificCulture("en-US"))}\t";
                            if(i < 6)
                            {
                                SPD = djp;
                                sp_counted = true;
                            } else
                            {
                                DPD = djp;
                                dp_counted = true;
                            }
                        }else
                        {
                            djp_str = "\t";
                        }
                        bool unlockState = Utils.GetUnlockStateForDifficulty(songid, chart.difficulty);
                        if (i > (int)Difficulty.SPB && i < (int)Difficulty.SPL)
                        {
                            var levels = Utils.songDb[songid].level;
                            int cost = (song.type == unlockType.Bits && !Utils.customTypes.ContainsKey(songid)
                                ? 500 * (levels[(int)chart.difficulty] + levels[(int)chart.difficulty + (int)Difficulty.DPB]) 
                                : 0);
                            bitCostData.Append($"{cost}\t");
                        }
                        chartData.Append($"{(unlockState ? "TRUE" : "FALSE")}\t");
                        chartData.Append($"{Utils.songDb[songid].level[(int)chart.difficulty]}\t");
                        chartData.Append($"{trackerDb[chart].lamp}\t");
                        chartData.Append($"{trackerDb[chart].grade}\t");
                        chartData.Append($"{trackerDb[chart].ex_score}\t");
                        chartData.Append($"{((int)trackerDb[chart].misscount == -1 ? "-" : trackerDb[chart].misscount.ToString())}\t");
                        chartData.Append($"{Utils.songDb[songid].totalNotes[(int)chart.difficulty]}\t");
                        chartData.Append(djp_str);
                    }
                }
                sb.Append(bitCostData);
                //sb.Append($"{totalDJP.ToString("E04", CultureInfo.CreateSpecificCulture("en-US"))}\t");
                sb.Append($"{SPD.ToString(CultureInfo.CreateSpecificCulture("en-US"))}\t{DPD.ToString(CultureInfo.CreateSpecificCulture("en-US"))}\t");
                sb.Append(chartData);

                yield return sb.ToString();
            }
        }
        /// <summary>
        /// If saving to remote, load tracker.db if exist, otherwise create, populate potential new songs with data from score data hash map
        /// When not saving to remote, just generate the tracker info from INFINITAS internal hash map
        /// </summary>
        public static void LoadTracker()
        {
            /* Initialize if tracker file don't exist */
            if (File.Exists("tracker.db"))
            {
                try
                {
                    foreach (var line in File.ReadAllLines("tracker.db"))
                    {
                        var segments = line.Split(',');
                        trackerDb.Add(new Chart() { 
                            songID = segments[0],
                            difficulty = (Difficulty)Enum.Parse(typeof(Difficulty), segments[1]) },
                            new TrackerInfo() { grade = (Grade)Enum.Parse(typeof(Grade), segments[2]),
                                lamp = (Lamp)Enum.Parse(typeof(Lamp), segments[3]),
                                ex_score = int.Parse(segments[4]),
                                misscount = uint.Parse(segments[5]),
                                });
                    }
                }
                catch (Exception e)
                {
                    Utils.Except(e);
                }
            }
            /* Add any potentially new songs */
            foreach (var song in Utils.songDb)
            {
                for (int i = 0; i < song.Value.level.Length; i++)
                {
                    /* Skip charts with no difficulty rating */
                    if (song.Value.level[i] == 0) { continue; }

                    var c = new Chart() { songID = song.Key, difficulty = (Difficulty)i };

                    if (!trackerDb.ContainsKey(c)) {
                        trackerDb.Add(c, new TrackerInfo()
                        {
                            lamp = ScoreMap.Scores[song.Key].lamp[i],
                            grade = (ScoreMap.Scores[song.Key].lamp[i] == Lamp.NP && ScoreMap.Scores[song.Key].score[i] == 0) ? Grade.NP : Utils.ScoreToGrade(song.Key, (Difficulty)i, ScoreMap.Scores[song.Key].score[i]),
                            ex_score = ScoreMap.Scores[song.Key].score[i],
                            misscount = ScoreMap.Scores[song.Key].misscount[i],
                            DJPoints = ScoreMap.Scores[song.Key].DJPoints[i]
                        });
                    } else
                    {
                        var entry = trackerDb[c];
                        /* Only overwrite lamp and grade, as those are the only with potential custom values */
                        entry.lamp = (Lamp)Math.Max((int)ScoreMap.Scores[song.Key].lamp[i], (int)entry.lamp);
                        entry.grade = (Grade)Math.Max((int)Utils.ScoreToGrade(song.Key, (Difficulty)i, ScoreMap.Scores[song.Key].score[i]), (int)entry.grade);
                        entry.misscount = ScoreMap.Scores[song.Key].misscount[i];
                        entry.ex_score = ScoreMap.Scores[song.Key].score[i];
                        entry.DJPoints = ScoreMap.Scores[song.Key].DJPoints[i];
                        trackerDb[c] = entry;
                    }
                }
            }
            SaveTracker();
        }
        /// <summary>
        /// Save tracker information to tracker.db for memory between executions
        /// </summary>
        public static void SaveTracker()
        {
            try
            {
                List<string> entries = new List<string>();
                foreach (var entry in trackerDb)
                {
                    entries.Add($"{entry.Key.songID},{entry.Key.difficulty},{entry.Value.grade},{entry.Value.lamp},{entry.Value.ex_score},{entry.Value.misscount}");
                }
                Utils.Debug("Saving tracker.db");
                File.WriteAllLines("tracker.db", entries.ToArray());
            }
            catch (Exception e)
            {
                Utils.Except(e);
            }
        }
    }
}
