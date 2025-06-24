using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Reflux
{
    public enum Difficulty
    {
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
    public enum Lamp
    {
        NP = 0,
        F,
        AC,
        EC,
        NC,
        HC,
        EX,
        FC,
        PFC
    }
    public enum Grade
    {
        NP = 0,
        F,
        E,
        D,
        C,
        B,
        A,
        AA,
        AAA,
    }

    /// <summary>
    /// All metadata for a song and its charts
    /// </summary>
    public struct SongInfo
    {
        public string ID;
        public int[] totalNotes; /* SPB, SPN, SPH, SPA, SPL, DPB, DPN, DPH, DPA, DPL */
        public int[] level; /* SPB, SPN, SPH, SPA, SPL, DPB, DPN, DPH, DPA, DPL */
        public string title;
        public string title_english;
        public string artist;
        public UnlockType type;
        public string genre;
        public string bpm;
        public int folder;
    }
    /// <summary>
    /// Information saved to the local tracker file
    /// </summary>
    public struct TrackerInfo
    {
        public Grade grade;
        public Lamp lamp;
        public decimal DJPoints;
        public int ex_score;
        public uint misscount;
    }
    /// <summary>
    /// Generic chart object for dictionary key lookup
    /// </summary>
    public struct Chart
    {
        public string songID;
        public Difficulty difficulty;
    }
    /// <summary>
    /// The three different kind of song unlock types, Bits are anything that is visible while locked, and Sub is anything that is not visible while locked
    /// </summary>
    public enum UnlockType { Base = 1, Bits, Sub }; // Assume subscription songs unless specifically addressed in customtypes.txt
    /// <summary>
    /// Structure of the unlock data array in memory
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UnlockData
    {
        public Int32 songID;
        public UnlockType type;
        public Int32 unlocks;
        public Int32 unknown1;
        public Int32 unknown2;
        public Int32 unknown3;
        public Int32 unknown4;
        public Int32 unknown5;
    };
    static class Utils
    {
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
            Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        public static IntPtr handle;
        /// <summary>
        /// DB for keeping track of unlocks and potential changes
        /// </summary>
        public static Dictionary<string, UnlockData> unlockDb = new Dictionary<string, UnlockData>();
        /// <summary>
        /// DB for easily looking up songs and their chart information
        /// </summary>
        public static Dictionary<string, SongInfo> songDb = new Dictionary<string, SongInfo>();
        /// <summary>
        /// DB of known encoding issues or inconsistencies to how they're generally known
        /// </summary>
        readonly static Dictionary<string, string> knownEncodingIssues = new Dictionary<string, string>();
        /// <summary>
        /// DB of custom types of unlocks, to separate DJP unlocks from bit unlocks and song pack unlocks from subscription songs
        /// </summary>
        public readonly static Dictionary<string, string> customTypes = new Dictionary<string, string>();

        public static Grade ScoreToGrade(string songID, Difficulty difficulty, int exscore)
        {
            var maxEx = Utils.songDb[songID].totalNotes[(int)difficulty] * 2;
            var exPart = (double)maxEx / 9;


            if (exscore >= exPart * 8)
            {
                return Grade.AAA;
            }
            else if (exscore >= exPart * 7)
            {
                return Grade.AA;
            }
            else if (exscore >= exPart * 6)
            {
                return Grade.A;
            }
            else if (exscore >= exPart * 5)
            {
                return Grade.B;
            }
            else if (exscore >= exPart * 4)
            {
                return Grade.C;
            }
            else if (exscore >= exPart * 3)
            {
                return Grade.D;
            }
            else if (exscore >= exPart * 2)
            {
                return Grade.E;
            }
            return Grade.F;


        }
        /// <summary>
        /// Populate DB for encoding issues, tab separated since commas can appear in title
        /// </summary>
        public static void LoadEncodingFixes()
        {
            try
            {
                foreach (var line in File.ReadAllLines("encodingfixes.txt"))
                {
                    if (!line.Contains('\t')) { continue; } /* Skip version string */
                    var pair = line.Split('\t');
                    knownEncodingIssues.Add(pair[0], pair[1].Trim());
                }
            }
            catch (Exception e)
            {
                Except(e);
            }
        }
        /// <summary>
        /// Populate DB for custom unlock types, comma separated
        /// </summary>
        public static void LoadCustomTypes()
        {
            try
            {
                foreach (var line in File.ReadAllLines("customtypes.txt"))
                {
                    if (!line.Contains(',')) { continue; } /* Skip version string */
                    var pair = line.Split(',');
                    customTypes.Add(pair[0], pair[1].Trim());
                }
            }
            catch (Exception e)
            {
                Except(e);
            }
        }
        /// <summary>
        /// Figure out if INFINITAS is currently playing a song, showing the results or hanging out in the song select
        /// </summary>
        /// <param name="currentState"></param>
        /// <returns></returns>
        public static GameState FetchGameState(GameState currentState)
        {
            short word = 4;
            short offset = 54;

            var marker = ReadInt32(Offsets.JudgeData, word * offset);
            if (marker != 0)
            {

                // In case it has shifted for whatever reason

                marker = ReadInt32(Offsets.JudgeData, word * (offset + 1));
                if (marker != 0)
                {
                    return GameState.playing;
                }
            }

            /* Cannot go from song select to result screen anyway */
            if (currentState == GameState.songSelect) { return currentState; }
            marker = ReadInt32(Offsets.PlaySettings - word * 6, 0);
            if (marker == 1)
            {
                return GameState.songSelect;
            }
            return GameState.resultScreen;
        }
        /// <summary>
        /// Fetch and format the current chart for saving to currentsong.txt
        /// </summary>
        /// <param name="includeDiff"></param>
        /// <returns></returns>
        public static string CurrentChart(bool includeDiff = false)
        {
            var values = FetchCurrentChart();
            return $"{songDb[values.songID].title_english}{(includeDiff ? " " + values.difficulty.ToString() : "")}";
        }
        /// <summary>
        /// Populate database for song metadata
        /// </summary>
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
        /// <summary>
        /// Util function to get an 32-bit integer from any section in a byte array
        /// </summary>
        /// <param name="input">Byte array to get value from</param>
        /// <param name="skip">Amount of bytes to skip before parsing</param>
        /// <param name="take">Amount of bytes to use for parsing</param>
        /// <returns></returns>
        public static Int32 BytesToInt32(byte[] input, int skip, int take = 4)
        {
            if (skip == 0)
            {
                return BitConverter.ToInt32(input.Take(take).ToArray());
            }
            return BitConverter.ToInt32(input.Skip(skip).Take(take).ToArray());
        }
        /// <summary>
        /// Util function to get an 64-bit integer from any section in a byte array
        /// </summary>
        /// <param name="input">Byte array to get value from</param>
        /// <param name="skip">Amount of bytes to skip before parsing</param>
        /// <param name="take">Amount of bytes to use for parsing</param>
        /// <returns></returns>
        public static Int64 BytesToInt64(byte[] input, int skip, int take = 8)
        {
            if (skip == 0)
            {
                return BitConverter.ToInt64(input.Take(take).ToArray());
            }
            return BitConverter.ToInt64(input.Skip(skip).Take(take).ToArray());
        }
        [Conditional("DEBUG")]
        public static void Debug(string msg)
        {
            Console.WriteLine(msg);
        }
        /// <summary>
        /// Print exception message to log for easier viewing
        /// </summary>
        /// <param name="e"></param>
        /// <param name="context"></param>
        public static void Except(Exception e, string context = "")
        {
            var stream = File.AppendText("log.txt");
            stream.WriteLine($"{DateTime.Now}[ERR]: {(context == "" ? "Unhandled exception" : context)}: {e.Message}\n#### STACKTRACE\n{e.StackTrace}\n####");
            Console.WriteLine("[ERR]: " + e.Message);
            stream.Flush();
            stream.Close();
        }
        public static void Log(string message)
        {
            var stream = File.AppendText("log.txt");
            stream.WriteLine($"{DateTime.Now}[INFO]: {message}");
            stream.Flush();
            stream.Close();
        }

        #region Memory reading functions
        /// <summary>
        /// Find the song and difficulty that is currently being played
        /// </summary>
        /// <returns></returns>
        public static Chart FetchCurrentChart()
        {
            byte[] buffer = new byte[32];
            int nRead = 0;
            ReadProcessMemory((int)handle, Offsets.CurrentSong, buffer, buffer.Length, ref nRead);
            int songid = BytesToInt32(buffer, 0);
            int diff = BytesToInt32(buffer, 4);
            return new Chart() { songID = songid.ToString("D5"), difficulty = (Difficulty)diff };
        }
        /// <summary>
        /// Figure out if all necessary data for populating different DBs are available
        /// </summary>
        /// <returns></returns>
        public static bool DataLoaded()
        {
            byte[] buffer = new byte[64];
            int nRead = 0;
            ReadProcessMemory((int)handle, Offsets.SongList, buffer, buffer.Length, ref nRead);
            var title = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Where(x => x != 0).ToArray());
            var titleNoFilter = Encoding.GetEncoding("Shift-JIS").GetString(buffer);
            buffer = new byte[4];
            ReadProcessMemory((int)handle, Offsets.UnlockData, buffer, buffer.Length, ref nRead);
            var id = Utils.BytesToInt32(buffer, 0);
            Debug($"Read string: \"{title}\" in start of song list, expecting \"5.1.1.\"");
            Debug($"Read number: {id} in start of unlock list, expecting 1000");
            return title.Contains("5.1.1.") && id == 1000;
        }
        public static byte[] ReadRaw(long position, int size)
        {
            int bytesRead = 0;

            byte[] buffer = new byte[size];

            ReadProcessMemory((int)handle, position, buffer, buffer.Length, ref bytesRead);
            return buffer;
        }
        /// <summary>
        /// Function to read any position in memory and convert to Int32
        /// </summary>
        /// <param name="position">Base offset in memory</param>
        /// <param name="offset">Potential extra offset for readability instead of just adding to <paramref name="position"/></param>
        /// <param name="size">Amount of bytes to read and convert</param>
        /// <returns></returns>
        public static Int32 ReadInt32(long position, int offset, int size = 4)
        {
            int bytesRead = 0;

            byte[] buffer = new byte[size];

            ReadProcessMemory((int)handle, position + offset, buffer, buffer.Length, ref bytesRead);
            return Utils.BytesToInt32(buffer.Take(size).ToArray(), 0);
        }
        /// <summary>
        /// Function to read any position in memory and convert to Int64
        /// </summary>
        /// <param name="position">Base offset in memory</param>
        /// <param name="offset">Potential extra offset for readability instead of just adding to <paramref name="position"/></param>
        /// <param name="size">Amount of bytes to read and convert</param>
        /// <returns></returns>
        public static Int64 ReadInt64(long position, int offset, int size = 8)
        {
            int bytesRead = 0;

            byte[] buffer = new byte[size];

            ReadProcessMemory((int)handle, position + offset, buffer, buffer.Length, ref bytesRead);
            return Utils.BytesToInt64(buffer.Take(size).ToArray(), 0);
        }
        /// <summary>
        /// Fetch metadata for one song
        /// </summary>
        /// <param name="position">Start position of song metadata</param>
        /// <returns>SongInfo object containing all metadata</returns>
        private static SongInfo FetchSongInfo(long position)
        {
            int bytesRead = 0;
            short slab = 64;
            short word = 4; /* Int32 */

            byte[] buffer = new byte[1008];

            ReadProcessMemory((int)handle, position, buffer, buffer.Length, ref bytesRead);

            var title1 = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Take(slab).Where(x => x != 0).ToArray());

            if (Utils.BytesToInt32(buffer.Take(slab).ToArray(), 0) == 0)
            {
                return new SongInfo();
            }

            var title2 = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(slab).Take(slab).Where(x => x != 0).ToArray());
            var genre = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(slab * 2).Take(slab).Where(x => x != 0).ToArray());
            var artist = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(slab * 3).Take(slab).Where(x => x != 0).ToArray());

            var folderBytes = buffer.Skip(slab * 4).Skip(24).Take(1).ToList();
            var folder = BitConverter.ToInt32(new byte[] { folderBytes[0], 0, 0, 0 });

            var diff_section = buffer.Skip(slab * 4 + slab / 2).Take(10).ToArray();
            var diff_levels = new int[] {
                Convert.ToInt32(diff_section[0]),
                Convert.ToInt32(diff_section[1]),
                Convert.ToInt32(diff_section[2]),
                Convert.ToInt32(diff_section[3]),
                Convert.ToInt32(diff_section[4]),
                Convert.ToInt32(diff_section[5]),
                Convert.ToInt32(diff_section[6]),
                Convert.ToInt32(diff_section[7]),
                Convert.ToInt32(diff_section[8]),
                Convert.ToInt32(diff_section[9]) };

            var bpms = buffer.Skip(slab * 5).Take(8).ToArray();
            var noteCounts_bytes = buffer.Skip(slab * 6 + 48).Take(slab).ToArray();

            var bpmMax = Utils.BytesToInt32(bpms, 0);
            var bpmMin = Utils.BytesToInt32(bpms, word);

            string bpm = "NA";
            if (bpmMin != 0)
            {
                bpm = $"{bpmMin:000}~{bpmMax:000}";
            }
            else
            {
                bpm = bpmMax.ToString("000");
            }

            var noteCounts = new int[] {
                Utils.BytesToInt32(noteCounts_bytes, 0),
                Utils.BytesToInt32(noteCounts_bytes, word),
                Utils.BytesToInt32(noteCounts_bytes, word * 2),
                Utils.BytesToInt32(noteCounts_bytes, word * 3),
                Utils.BytesToInt32(noteCounts_bytes, word * 4),
                Utils.BytesToInt32(noteCounts_bytes, word * 5),
                Utils.BytesToInt32(noteCounts_bytes, word * 6),
                Utils.BytesToInt32(noteCounts_bytes, word * 7),
                Utils.BytesToInt32(noteCounts_bytes, word * 8),
                Utils.BytesToInt32(noteCounts_bytes, word * 9)
            };


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
                level = diff_levels,
                folder = folder
            };

            return song;

        }
        #endregion

        #region Unlock database related
        /// <summary>
        /// Update and detect changes to song unlock states
        /// </summary>
        /// <returns>Changes between the two unlock statuses, if any. Empty dictionary otherwise</returns>
        public static Dictionary<string, UnlockData> UpdateUnlockStates()
        {
            var oldUnlocks = unlockDb;
            GetUnlockStates();
            var changes = new Dictionary<string, UnlockData>();
            foreach (var key in unlockDb.Keys)
            {
                if (!oldUnlocks.ContainsKey(key))
                {
                    Log($"Key {key} was not present in past unlocks array");
                    continue;
                }
                if (unlockDb[key].unlocks != oldUnlocks[key].unlocks)
                {
                    UnlockData value = unlockDb[key];
                    changes.Add(key, value);
                    oldUnlocks[key] = unlockDb[key];
                }
            }
            return changes;
        }
        /// <summary>
        /// Read and populate a dictionary for the unlock information of all songs
        /// </summary>
        /// <returns></returns>
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
            while (extra > 0)
            {
                buf = new byte[structSize * extra];
                ReadProcessMemory((int)handle, Offsets.UnlockData + structSize * (songAmount + moreExtra), buf, buf.Length, ref nRead);
                moreExtra = ParseUnlockBuffer(buf);
                extra = moreExtra;
            }

            return unlockDb;
        }

        /// <summary>
        /// Convert a byte array to an <see cref="UnlockData"/> object
        /// </summary>
        /// <param name="buf">Byte array</param>
        /// <returns>An <see cref="UnlockData"/> object representation of the input</returns>
        static int ParseUnlockBuffer(byte[] buf)
        {
            int position = 0;
            int extra = 0;
            int structSize = Marshal.SizeOf(typeof(UnlockData));
            while (position < buf.Length)
            {
                var sData = buf.Skip(position).Take(structSize).ToArray();
                UnlockData data = new UnlockData
                {
                    songID = BytesToInt32(sData, 0),

                    type = (UnlockType)BytesToInt32(sData, 4),

                    unlocks = BytesToInt32(sData, 8)
                };
                string id = data.songID.ToString("D5");
                if (id == "00000") /* Take into account where songDb is populated with unreleased songs */
                {
                    break;
                }
                unlockDb.Add(id, data);
                try
                {
                    var song = songDb[id];
                    song.type = data.type;
                    songDb[id] = song;
                }
                catch
                {
                    Debug($"Song {id} not present in song database");
                    extra++;
                }

                position += structSize;
            }
            return extra;

        }
        /// <summary>
        /// Get the unlock state for a specific chart of a song
        /// </summary>
        /// <param name="songid">SongID of interest</param>
        /// <param name="diff">Chart difficulty</param>
        /// <returns>True if unlocked, false if locked</returns>
        public static bool GetUnlockStateForDifficulty(string songid, Difficulty diff)
        {
            try
            {
                var unlockBits = unlockDb[songid].unlocks;
                int bit = 1 << (int)diff;
                bool unlockState = (bit & unlockBits) > 0;
                // Beginner difficulties are handled differently
                if (diff == Difficulty.SPB)
                {
                    unlockState = songDb[songid].type == UnlockType.Sub
                        ? unlockState // If part of a music pack, the unlock state determines availability
                        : songDb[songid].totalNotes[(int)diff] != 0; // Otherwise, note count not being zero means it's playable

                }
                return unlockState;
            }
            catch
            {
                Debug($"{songid} doesn't exist in unlockDb");
                return false;
            }
        }
        #endregion

        #region Tracker related
        #endregion

        /// <summary>
        /// Check if a newer release is available and notify user
        /// </summary>
        public static void CheckVersion()
        {
            /* Compare segments of current version and latest release tag and notify if newer version is available */
            var assemblyInfo = System.Reflection.Assembly.GetExecutingAssembly().GetName();
            var version = assemblyInfo.Version.ToString(3);
            Console.WriteLine(assemblyInfo.Name + " " + version);
            var netVersion = Network.GetLatestVersion();
            var segments = version.Split('.');
            for (int i = 0; i < segments.Length; i++)
            {
                var netSegments = netVersion.Split('.');
                if (netSegments.Length <= i)
                {
                    break;
                }
                if (int.Parse(segments[i]) < int.Parse(netSegments[i]))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Newer version {netVersion} is available.");
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                }
            }
        }
        /// <summary>
        /// Calculate the DJ Points awarded for a given chart
        /// </summary>
        /// <param name="songID">SongID</param>
        /// <param name="diff">Chart difficulty</param>
        /// <param name="score">EX score of best play</param>
        /// <param name="lamp">Best clear lamp</param>
        /// <returns>DJ Points as a decimal value</returns>
        public static decimal CalculateDJP(string songID, Difficulty diff, int score, Lamp lamp)
        {
            var grade = ScoreToGrade(songID, diff, score);
            /* C gets a value of 10 starting from A, with +5 for each grade above that */
            decimal C = (grade >= Grade.A ? 10 : 0) + Math.Max(0, grade - Grade.A) * 5;
            /* L increases by 5 for each lamp above AC (AC being 0), with HC and better increasing the value further by an extra 5 */
            decimal L = Math.Max(0, lamp - Lamp.AC) * 5 + (lamp >= Lamp.HC ? 5 : 0);
            return score * (100 + C + L) / 10000;
        }
    }
}
