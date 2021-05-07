using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace infinitas_statfetcher
{
    enum DiffInt { 
        SPB = 0,
        SPN,
        SPH,
        SPA,
        SPL,
        DPB,
        DPN,
        DPH,
        DPA,
        DPL
    }
    public struct SongInfo
    {
        public string ID;
        public int[] totalNotes; /* SPB, SPN, SPH, SPA, SPL, DPB, DPN, DPH, DPA, DPL */
        public int[] level; /* SPB, SPN, SPH, SPA, SPL, DPB, DPN, DPH, DPA, DPL */
        public string title;
        public string title_english;
        public string artist;
        public unlockType type;
        public string genre;
        public string bpm;
    }
    public struct TrackerInfo
    {
        public int grade;
        public int lamp;
    }
    public struct Chart
    {
        public string songID;
        public int difficulty;
    }
    public enum unlockType { Base = 1, Bits, Sub }; // Hidden being potentially hidden from you, as with subscription songs or song packs
    [StructLayout(LayoutKind.Sequential)]
    public struct UnlockData
    {
        public Int32 songID;
        public unlockType type;
        public Int32 unlocks;
    };
    class Utils
    {
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
            Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        public static IntPtr handle;
        public static Dictionary<string, UnlockData> unlockDb = new Dictionary<string, UnlockData>();
        public static Dictionary<string, SongInfo> songDb = new Dictionary<string, SongInfo>();
        public static Dictionary<Chart, TrackerInfo> trackerDb = new Dictionary<Chart, TrackerInfo>();
        readonly static Dictionary<string, string> knownEncodingIssues = new Dictionary<string, string>();

        public static void LoadEncodingFixes()
        {
            foreach (var line in File.ReadAllLines("encodingfixes.txt"))
            {
                if (!line.Contains('\t')) { continue; } /* Skip version string */
                var pair = line.Split('\t');
                knownEncodingIssues.Add(pair[0], pair[1]);
            }
        }
        public static GameState FetchGameState(GameState currentState)
        {
            short word = 4;

            var marker = ReadInt32(Offsets.JudgeData, word * 24, word);
            if (marker != 0)
            {
                return GameState.playing;
            }

            /* Cannot go from song select to result screen anyway */
            if(currentState == GameState.songSelect) { return currentState; }
            marker = ReadInt32(Offsets.PlaySettings - word * 5, 0, word);
            if (marker == 1)
            {
                return GameState.songSelect;
            }
            return GameState.resultScreen;
        }
        public static string CurrentChart(bool includeDiff = false)
        {
            var values = FetchCurrentChart();
            return $"{songDb[values.id].title_english}{(includeDiff ? " "+IntToDiff(values.diff) : "")}";
        }
        public struct currentChartData
        {
            public string id;
            public int diff;
        }
        public static void FetchSongDataBase()
        {
            Dictionary<string, SongInfo> result = new Dictionary<string, SongInfo>();
            Debug("Fetching available songs");
            var current_position = 0;
            while (true)
            {

                var songInfo = FetchSongInfo(Offsets.SongList + current_position);

                if (songInfo.title == null)
                {
                    Debug("Songs fetched.");
                    break;
                }

                if (knownEncodingIssues.ContainsKey(songInfo.title))
                {
                    var old = songInfo.title;
                    songInfo.title = knownEncodingIssues[songInfo.title];
                    Debug($"Fixed encoding issue \"{old}\" with \"{songInfo.title}\"");
                }
                if (knownEncodingIssues.ContainsKey(songInfo.artist))
                {
                    var old = songInfo.artist;
                    songInfo.artist = knownEncodingIssues[songInfo.artist];
                    Debug($"Fixed encoding issue \"{old}\" with \"{songInfo.artist}\"");
                }
                if (!result.ContainsKey(songInfo.ID))
                {
                    result.Add(songInfo.ID, songInfo);
                }

                current_position += 0x3F0;

            }
            songDb = result;
        }
        static Int32 BytesToInt32(byte[] input, int skip, int take)
        {
            if (skip == 0)
            {
                return BitConverter.ToInt32(input.Take(take).ToArray());
            }
            return BitConverter.ToInt32(input.Skip(skip).Take(take).ToArray());
        }
        [Conditional("DEBUG")]
        public static void Debug(string msg)
        {
            Console.WriteLine(msg);
        }

        #region Memory reading functions
        public static currentChartData FetchCurrentChart()
        {
            byte[] buffer = new byte[32];
            int nRead = 0;
            ReadProcessMemory((int)handle, Offsets.CurrentSong, buffer, buffer.Length, ref nRead);
            int songid = BytesToInt32(buffer, 0, 4);
            int diff = BytesToInt32(buffer, 4, 4);
            return new currentChartData() { id = songid.ToString("D5"), diff = diff };
        }
        public static bool SongListAvailable()
        {
            byte[] buffer = new byte[64];
            int nRead = 0;
            ReadProcessMemory((int)handle, Offsets.SongList, buffer, buffer.Length, ref nRead);
            var title = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Where(x => x != 0).ToArray());
            var titleNoFilter = Encoding.GetEncoding("Shift-JIS").GetString(buffer);
            buffer = new byte[4];
            ReadProcessMemory((int)handle, Offsets.UnlockData, buffer, buffer.Length, ref nRead);
            var id = Utils.BytesToInt32(buffer, 0, 4);
            Debug($"Read string: \"{title}\" in start of song list, expecting \"5.1.1.\"");
            Debug($"Read number: {id} in start of unlock list, expecting 1000");
            return title.Contains("5.1.1.") && id == 1000;
        }
        public static Int32 ReadInt32(long position, int offset, int size)
        {
            int bytesRead = 0;

            byte[] buffer = new byte[size];

            ReadProcessMemory((int) handle, position+offset, buffer, buffer.Length, ref bytesRead);
            return Utils.BytesToInt32(buffer.Take(size).ToArray(), 0, size);
        }
        private static SongInfo FetchSongInfo(long position)
        {
            int bytesRead = 0;
            short slab = 64;
            short word = 4; /* Int32 */

            byte[] buffer = new byte[1008];

            ReadProcessMemory((int)handle, position, buffer, buffer.Length, ref bytesRead);

            var title1 = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Take(slab).Where(x => x != 0).ToArray());

            if (Utils.BytesToInt32(buffer.Take(slab).ToArray(), 0, slab) == 0)
            {
                return new SongInfo();
            }

            var title2 = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(slab).Take(slab).Where(x => x != 0).ToArray());
            var genre = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(slab * 2).Take(slab).Where(x => x != 0).ToArray());
            var artist = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(slab * 3).Take(slab).Where(x => x != 0).ToArray());

            var diff_section = buffer.Skip(slab * 4 + slab / 2).Take(10).ToArray();
            var diff_levels = new int[] { Convert.ToInt32(diff_section[0]), Convert.ToInt32(diff_section[1]), Convert.ToInt32(diff_section[2]), Convert.ToInt32(diff_section[3]), Convert.ToInt32(diff_section[4]), Convert.ToInt32(diff_section[5]), Convert.ToInt32(diff_section[6]), Convert.ToInt32(diff_section[7]), Convert.ToInt32(diff_section[8]), Convert.ToInt32(diff_section[9]) };

            var bpms = buffer.Skip(slab * 5).Take(8).ToArray();
            var noteCounts_bytes = buffer.Skip(slab * 6 + 48).Take(slab).ToArray();

            var bpmMax = Utils.BytesToInt32(bpms, 0, word);
            var bpmMin = Utils.BytesToInt32(bpms, word, word);

            string bpm = "NA";
            if (bpmMin != 0)
            {
                bpm = $"{bpmMin:000}~{bpmMax:000}";
            }
            else
            {
                bpm = bpmMax.ToString("000");
            }

            var noteCounts = new int[] { Utils.BytesToInt32(noteCounts_bytes, 0, word), Utils.BytesToInt32(noteCounts_bytes, word, word), Utils.BytesToInt32(noteCounts_bytes, word * 2, word), Utils.BytesToInt32(noteCounts_bytes, word * 3, word), Utils.BytesToInt32(noteCounts_bytes, word * 4, word), Utils.BytesToInt32(noteCounts_bytes, word * 5, word), Utils.BytesToInt32(noteCounts_bytes, word * 6, word), Utils.BytesToInt32(noteCounts_bytes, word * 7, word), Utils.BytesToInt32(noteCounts_bytes, word * 8, word), Utils.BytesToInt32(noteCounts_bytes, word * 9, word) };


            var idarray = buffer.Skip(256 + 368).Take(4).ToArray();

            var ID = BitConverter.ToInt32(idarray, 0).ToString("D5");

            var song = new SongInfo
            {
                ID = ID,
                title = title1,
                title_english = title2,
                genre = genre,
                artist = artist,
                bpm = bpm,
                totalNotes = noteCounts,
                level = diff_levels
            };

            return song;

        }
        #endregion

        #region Unlock database related
        /// <summary>
        /// Update and detect changes to song unlock states
        /// </summary>
        /// <param name="unlocks"></param>
        /// <returns>Changes between the two unlock statuses</returns>
        public static Dictionary<string, UnlockData> UpdateUnlockStates()
        {
            var oldUnlocks = unlockDb;
            GetUnlockStates();
            var changes = new Dictionary<string, UnlockData>();
            foreach(var key in unlockDb.Keys)
            {
                if(unlockDb[key].unlocks != oldUnlocks[key].unlocks)
                {
                    UnlockData value = unlockDb[key];
                    changes.Add(key, value);
                    oldUnlocks[key] = unlockDb[key];
                }
            }
            return changes;
        }
        public static Dictionary<string, UnlockData> GetUnlockStates()
        {
            int songAmount = songDb.Count;
            int structSize = Marshal.SizeOf(typeof(UnlockData));
            byte[] buf = new byte[structSize * songAmount];
            int nRead = 0;

            /* Read information for all songs at once and cast to struct array after */
            ReadProcessMemory((int)handle, Offsets.UnlockData, buf, buf.Length, ref nRead);

            unlockDb = new Dictionary<string, UnlockData>();
            var extra = ParseUnlockBuffer(buf);

            /* Handle offset issues caused by unlock data having information on songs not present in song db */
            int moreExtra = 0;
            while(extra > 0)
            {
                buf = new byte[structSize * extra];
                ReadProcessMemory((int)handle, Offsets.UnlockData + structSize * (songAmount + moreExtra), buf, buf.Length, ref nRead);
                moreExtra = ParseUnlockBuffer(buf);
                extra = moreExtra;
            }

            return unlockDb;
        }
        static int ParseUnlockBuffer(byte[] buf)
        {
            int position = 0;
            int extra = 0;
            int structSize = Marshal.SizeOf(typeof(UnlockData));
            while(position < buf.Length)
            {
                var sData = buf.Skip(position).Take(structSize).ToArray();
                UnlockData data = new UnlockData { songID = BytesToInt32(sData, 0, 4), type = (unlockType)BytesToInt32(sData, 4, 4), unlocks = BytesToInt32(sData, 8, 4) };
                string id = data.songID.ToString("D5");
                if(id == "00000") /* Take into account where songDb is populated with unreleased songs */
                {
                    break;
                }
                unlockDb.Add(id, data);
                try
                {
                    var song = songDb[id];
                    song.type = data.type;
                    songDb[id] = song;
                } catch
                {
                    Debug($"Song {id} not present in song database");
                    extra++;
                }

                position += structSize;
            }
            return extra;

        }
        public static bool GetUnlockStateForDifficulty(string songid, int diff)
        {
            var unlockBits = unlockDb[songid].unlocks;
            int bit = 1 << diff;
            return (bit & unlockBits) > 0;
        }
        public static bool GetUnlockStateForDifficulty(string songid, string diff)
        {
            return GetUnlockStateForDifficulty(songid, DiffToInt(diff));
        }
        #endregion

        #region Tracker related
        static IEnumerable<string> GetTrackerEntries()
        {
            foreach(var songid in trackerDb.Keys.Select(x => x.songID).Distinct())
            {
                var song = unlockDb[songid];
                StringBuilder sb = new StringBuilder($"{songDb[songid].title}\t");

                StringBuilder chartData = new StringBuilder();
                int bitCost = 0;
                for(int i = 0; i < 10; i++)
                {
                    /* Skip beginner and leggendaria */
                    if(i == (int)DiffInt.SPB || i == (int)DiffInt.SPL || i == (int)DiffInt.DPB || i == (int)DiffInt.DPL) { continue; }
                    Chart chart = new Chart() { songID = songid, difficulty = i };
                    if (!trackerDb.ContainsKey(chart))
                    {
                        chartData.Append("\t\t\t\t");
                    }
                    else
                    {
                        bool unlockState = GetUnlockStateForDifficulty(songid, chart.difficulty);
                        chartData.Append($"{(unlockState ? "TRUE" : "FALSE")}\t");
                        chartData.Append($"{songDb[songid].level[chart.difficulty]}\t");
                        chartData.Append($"{Utils.IntToLamp(trackerDb[chart].lamp)}\t");
                        chartData.Append($"{Utils.IntToGrade(trackerDb[chart].grade)}\t");
                        if (!unlockState)
                        {
                            bitCost += 500 * songDb[songid].level[chart.difficulty];
                        }
                    }
                }
                sb.Append($"{(song.type == unlockType.Bits ? bitCost.ToString() : song.type.ToString())}\t");
                sb.Append(chartData);

                yield return sb.ToString();
            }
        }
        public static void SaveTrackerData(string filename)
        {

            StringBuilder sb = new StringBuilder();
            StringBuilder db = new StringBuilder();
            sb.AppendLine("title\tType\tSPN\tSPN Rating\tSPN Lamp\tSPN Letter\tSPH\tSPH Rating\tSPH Lamp\tSPH Letter\tSPA\tSPA Rating\tSPA Lamp\tSPA Letter\tDPN\tDPN Rating\tDPN Lamp\tDPN Letter\tDPH\tDPH Rating\tDPH Lamp\tDPH Letter\tDPA\tDPA Rating\tDPA Lamp\tDPA Letter");
            foreach (var entry in Utils.GetTrackerEntries())
            {
                sb.AppendLine(entry);
            }
            File.WriteAllText(filename, sb.ToString());
            if (Config.Save_remote)
            {
                foreach (var song in unlockDb)
                {
                    db.AppendLine($"{song.Key},{(int)song.Value.type},{song.Value.unlocks}");
                }
                File.WriteAllText("unlockdb", db.ToString());
            }
        }
        public static void LoadTracker()
        {
            /* Initialize if tracker file don't exist */
            if (File.Exists("tracker.db"))
            {
                foreach (var line in File.ReadAllLines("tracker.db"))
                {
                    var segments = line.Split(',');
                    trackerDb.Add(new Chart() { songID = segments[0], difficulty = int.Parse(segments[1]) }, new TrackerInfo() { grade = int.Parse(segments[2]), lamp = int.Parse(segments[3]) });
                }
            } else
            {
                foreach(var song in songDb)
                {
                    for(int i = 0; i < song.Value.level.Length; i++)
                    {
                        /* Skip beginner difficulties */
                        if(i == (int)DiffInt.SPB || i == (int)DiffInt.DPB) { continue; }
                        /* Skip charts with no difficulty rating */
                        if(song.Value.level[i] == 0) { continue; }

                        trackerDb.Add(new Chart() { songID = song.Key, difficulty = i }, new TrackerInfo() { grade = 0, lamp = 0 });
                        SaveTracker();
                    }
                }
            }
        }
        public static void SaveTracker()
        {
            List<string> entries = new List<string>();
            foreach (var entry in trackerDb)
            {
                entries.Add($"{entry.Key.songID},{entry.Key.difficulty},{entry.Value.grade},{entry.Value.lamp}");
            }
            Debug("Saving tracker.db");
            File.WriteAllLines("tracker.db", entries.ToArray());
        }
        #endregion

        #region Int to String conversion functions
        public static string IntToDiff(int diff) {
            switch (diff)
            {
                case 0: return "SPB";
                case 1: return "SPN";
                case 2: return "SPH";
                case 3: return "SPA";
                case 4: return "SPL";
                case 5: return "DPB";
                case 6: return "DPN";
                case 7: return "DPH";
                case 8: return "DPA";
                case 9: return "DPL";
                default: return "Unknown";
            }
        }
        public static int DiffToInt(string diff) { 
            switch (diff)
            {
                case "SPB": return 0;
                case "SPN": return 1;
                case "SPH": return 2;
                case "SPA": return 3;
                case "SPL": return 4;
                case "DPB": return 5;
                case "DPN": return 6;
                case "DPH": return 7;
                case "DPA": return 8;
                case "DPL": return 9;
                default: return -1;
            }
        }
        public static string IntToLamp(int lamp)
        {
            /* Lamp: 0-7, [noplay, fail, a-clear, e-clear, N, H, EX, FC] */
            switch (lamp)
            {
                case 0: return "NP";
                case 1: return "F"; 
                case 2: return "AC";
                case 3: return "EC";
                case 4: return "NC";
                case 5: return "HC";
                case 6: return "EX";
                case 7: return "FC";
                default: return "Unknown";
            }
        }
        public static int LampToInt(string lamp)
        {
            switch (lamp)
            {
                case "NP": return 0;
                case "F": return 1;
                case "AC": return 2;
                case "EC": return 3;
                case "NC": return 4;
                case "HC": return 5;
                case "EX": return 6;
                case "FC": return 7;
                default: return -1;
            }
        }
        public static int GradeToInt(string grade)
        {
            switch (grade)
            {
                case "": return 0;
                case "F": return 1;
                case "E": return 2;
                case "D": return 3;
                case "C": return 4;
                case "B": return 5;
                case "A": return 6;
                case "AA": return 7;
                case "AAA": return 8;
                default: return -1;
            }
        }
        public static string IntToGrade(int grade)
        {
            switch (grade)
            {
                case 0: return "";
                case 1: return "F";
                case 2: return "E";
                case 3: return "D";
                case 4: return "C";
                case 5: return "B";
                case 6: return "A";
                case 7: return "AA";
                case 8: return "AAA";
                default: return "";
            }
        }

        #endregion
    }
}
