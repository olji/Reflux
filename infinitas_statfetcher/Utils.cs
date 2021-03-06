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
    class Utils
    {
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
            Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        public static IntPtr handle;
        readonly static Dictionary<string, string> knownEncodingIssues = new Dictionary<string, string>();

        public static void LoadEncodingFixes()
        {
            /* This shouldn't be necessary, Viva!, fffff, AETHER and Sweet Sweet Magic encoded fine during early development */
            foreach (var line in File.ReadAllLines("encodingfixes.txt"))
            {
                var pair = line.Split('\t');
                knownEncodingIssues.Add(pair[0], pair[1]);
            }
        }
        public static GameState FetchGameState()
        {
            short word = 4;

            var marker = ReadInt32(Offsets.JudgeData, word * 24, word);

            if (marker == 0)
            {
                return GameState.finished;
            }
            return GameState.started;
        }
        public static bool SongListAvailable()
        {
            byte[] buffer = new byte[64];
            int nRead = 0;
            ReadProcessMemory((int)handle, Offsets.SongList, buffer, buffer.Length, ref nRead);
            var title = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Where(x => x != 0).ToArray());
            var titleNoFilter = Encoding.GetEncoding("Shift-JIS").GetString(buffer);
            Debug($"Read string: \"{title}\", expecting \"5.1.1.\"");
            return title.Contains("5.1.1.");
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
        public static Dictionary<string, SongInfo> FetchSongDataBase()
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
            return result;
        }
        public enum unlockType { Base = 1, Bits, Hidden }; // Hidden being potentially hidden from you, as with subscription songs or song packs
        [StructLayout(LayoutKind.Sequential)]
        public struct UnlockData {
            public Int32 songID; 
            public unlockType type;
            public Int32 unlocks;
        };
        static Int32 BytesToInt32(byte[] input, int skip, int take)
        {
            if (skip == 0)
            {
                return BitConverter.ToInt32(input.Take(take).ToArray());
            }
            return BitConverter.ToInt32(input.Skip(skip).Take(take).ToArray());
        }
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

        /// <summary>
        /// Update and detect changes to song unlock states
        /// </summary>
        /// <param name="unlocks"></param>
        /// <returns>Changes between the two unlock statuses</returns>
        public static Dictionary<string, UnlockData> UpdateUnlockStates(Dictionary<string, UnlockData> unlocks)
        {
            var newUnlock = GetUnlockStates(unlocks.Count);
            var changes = new Dictionary<string, UnlockData>();
            foreach(var key in newUnlock.Keys)
            {
                if(newUnlock[key].unlocks != unlocks[key].unlocks)
                {
                    UnlockData value = newUnlock[key];
                    changes.Add(key, value);
                    unlocks[key] = newUnlock[key];
                }
            }
            return changes;
        }
        public static Dictionary<string, UnlockData> GetUnlockStates(int songAmount)
        {
            int structSize = Marshal.SizeOf(typeof(UnlockData));
            byte[] buf = new byte[structSize * songAmount];
            int nRead = 0;

            /* Read information for all songs at once and cast to struct array after */
            ReadProcessMemory((int)handle, Offsets.UnlockData, buf, buf.Length, ref nRead);

            Dictionary<string, UnlockData> result = new Dictionary<string, UnlockData>();
            int position = 0;
            while(position < buf.Length)
            {
                var sData = buf.Skip(position).Take(structSize).ToArray();
                UnlockData data = new UnlockData { songID = BytesToInt32(sData, 0, 4), type = (unlockType)BytesToInt32(sData, 4, 4), unlocks = BytesToInt32(sData, 8, 4) };
                result.Add(data.songID.ToString("D5"), data);

                position += structSize;
            }
            IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UnlockData)) * songAmount);
            Marshal.Copy(buf, 0, p, songAmount);

            return result;
        }
        static string SongToUnlockEntry(string songTitle, UnlockData song)
        {
            //StringBuilder sb = new StringBuilder("title\tSPN\tSPH\tSPA\tDPN\tDPH\tDPA");
            StringBuilder sb = new StringBuilder($"{songTitle}\t{song.type.ToString()}");

            var unlockWord = new BitVector32(song.unlocks);
            var SPB = BitVector32.CreateMask();
            var SPN = BitVector32.CreateMask(SPB);
            var SPH = BitVector32.CreateMask(SPN);
            var SPA = BitVector32.CreateMask(SPH);
            var SPL = BitVector32.CreateMask(SPA);
            var DPB = BitVector32.CreateMask(SPL);
            var DPN = BitVector32.CreateMask(DPB);
            var DPH = BitVector32.CreateMask(DPN);
            var DPA = BitVector32.CreateMask(DPH);
            var DPL = BitVector32.CreateMask(DPA);

            sb.Append($"{unlockWord[SPN]}\t");
            sb.Append($"{unlockWord[SPH]}\t");
            sb.Append($"{unlockWord[SPA]}\t");
            sb.Append($"{unlockWord[DPN]}\t");
            sb.Append($"{unlockWord[DPH]}\t");
            sb.Append($"{unlockWord[DPA]}");

            return sb.ToString();
        }
        public static void SaveUnlockStates(string filename, Dictionary<string, SongInfo> db, Dictionary<string, UnlockData> unlocks)
        {

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("title\tType\tSPN\tSPH\tSPA\tDPN\tDPH\tDPA");
            foreach(var song in unlocks)
            {
                try
                {
                    sb.AppendLine(Utils.SongToUnlockEntry(db[song.Key].title, song.Value));
                } catch
                {
                }
            }
            File.WriteAllText(filename, sb.ToString());
        }
        [Conditional("DEBUG")]
        public static void Debug(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
