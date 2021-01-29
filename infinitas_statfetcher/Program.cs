using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Ini;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace infinitas_statfetcher
{
    class Program
    {
        /* Import external methods */
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
            Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);


        static HttpClient client = new HttpClient();
        static Config config = new Config();
        static Dictionary<string, string> knownEncodingIssues = new Dictionary<string, string>();
        static void Main(string[] args)
        {
            Process process = null;

            ParseConfig();
            Console.WriteLine("Trying to hook to INFINITAS...");
            do
            {
                var processes = Process.GetProcessesByName("bm2dx");
                if (processes.Any())
                {
                    process = processes[0];
                    process.Exited += Process_Exited;
                }

                Thread.Sleep(2000);
            } while (process == null);

            Console.Clear();
            Console.WriteLine("Hooked to process, waiting until song list is loaded...");


            LoadEncodingFixes();

            IntPtr processHandle = OpenProcess(0x0410, false, process.Id); /* Open process for memory read */

            var songDb = new Dictionary<string, SongInfo>();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Offsets offsets = new Offsets();
            foreach (var line in File.ReadAllLines("offsets.txt"))
            {
                var sections = line.Split('=');
                sections[0] = sections[0].Trim();
                sections[1] = sections[1].Trim();
                var offset = Convert.ToInt64(sections[1], 16);
                switch (sections[0].ToLower())
                {
                    case "judgedata": offsets.judgeData = offset; break;
                    case "playdata": offsets.playData = offset; break;
                    case "songlist": offsets.songList = offset; break;
                    case "playsettings": offsets.playSettings = offset; break;
                }
            }

            bool songlistFetched = false;
            while (!songlistFetched)
            {
                while (!SongListAvailable(processHandle, offsets.songList)) { Thread.Sleep(5000); } /* Don't fetch song list until it seems loaded */
                songDb = FetchSongDataBase(processHandle, offsets.songList);
                if (songDb["80003"].totalNotes[3] < 10) /* If Clione (Ryu* Remix) SPH has less than 10 notes, the songlist probably wasn't completely populated when we fetched it. That memory space generally tends to hold 0, 2 or 3, depending on which 'difficulty'-doubleword you're reading */
                {
                    Console.WriteLine("Notecount data seems bad, retrying fetching in case list wasn't fully populated.");
                    Thread.Sleep(5000);
                }
                else
                {
                    songlistFetched = true;
                }
            }

            //upload_songlist(songDb);
            /* Primarily for debugging and checking for encoding issues */
            if (config.output_db)
            {
                List<string> p = new List<string>() { "id,title,title2,artist,genre" };
                foreach (var v in songDb)
                {
                    p.Add($"{v.Key},{v.Value.title},{v.Value.title_english},{v.Value.artist},{v.Value.genre}");
                }
                File.WriteAllLines("songs.csv", p.ToArray());
            }
            gameState state = gameState.finished;

            PlayData latestData = new PlayData();

            while (!process.HasExited)
            {
                var newstate = FetchGameState(processHandle, state, offsets.judgeData);
                if (newstate != state)
                {
                    Console.Clear();
                    Console.WriteLine($"STATUS:{(newstate == gameState.finished ? " NOT" : "")} PLAYING");
                    if (newstate == gameState.finished)
                    {
                        latestData = FetchPlayData(processHandle, songDb, offsets.playData, offsets.judgeData, offsets.playSettings);
                        Send_PlayData(songDb, latestData);
                        Print_PlayData(songDb, latestData);
                    }
                }
                state = newstate;

                Thread.Sleep(2000);
            }

            Cleanup();
        }

        static void Print_PlayData(Dictionary<string, SongInfo> songDb, PlayData latestData)
        {
            Console.WriteLine("\nLATEST CLEAR:");

            Console.WriteLine($"{songDb[latestData.songID].title} {latestData.difficulty} [{songDb[latestData.songID].level[latestData.difficulty_index]}]");
            Console.WriteLine($"" +
                $"{latestData.judges.playtype.ToString()} " +
                $"{latestData.settings.style} " +
                $"{(latestData.judges.playtype == playType.DP ? latestData.settings.style2 : " ")}" +
                $"{latestData.settings.gauge} " +
                $"{latestData.settings.assist} " +
                $"{latestData.settings.range} " +
                $"{(latestData.judges.playtype == playType.DP && latestData.settings.flip ? "FLIP " : "")}" +
                $"{(latestData.settings.Hran ? "H-RAN " : "")}" +
                $"{(latestData.settings.battle ? "BATTLE " : "")}");
            Console.WriteLine("Clear:\t\t" + latestData.clearLamp);
            Console.WriteLine("DJ Level:\t" + latestData.grade);
            Console.WriteLine("EX score:\t" + latestData.ex);
            Console.WriteLine("pgreat:\t\t" + latestData.judges.pgreat);
            Console.WriteLine("great:\t\t" + latestData.judges.great);
            Console.WriteLine("good:\t\t" + latestData.judges.good);
            Console.WriteLine("bad:\t\t" + latestData.judges.bad);
            Console.WriteLine("poor:\t\t" + latestData.judges.poor);
            Console.WriteLine("combo break:\t" + latestData.judges.combobreak);
            Console.WriteLine("fast:\t\t" + latestData.judges.fast);
            Console.WriteLine("slow:\t\t" + latestData.judges.slow);

        }

        static async void Send_PlayData(Dictionary<string, SongInfo> songDb, PlayData latestData)
        {
            var form = new Dictionary<string, string>
            {
                { "apikey", "apikey" },
                { "date", latestData.timestamp.ToString("s") },
                { "title", songDb[latestData.songID].title },
                { "title2", songDb[latestData.songID].title_english },
                { "bpm", songDb[latestData.songID].bpm },
                { "artist", songDb[latestData.songID].artist },
                { "genre", songDb[latestData.songID].genre },
                { "notecount", songDb[latestData.songID].totalNotes[latestData.difficulty_index].ToString() },
                { "diff", latestData.difficulty },
                { "level", songDb[latestData.songID].level[latestData.difficulty_index].ToString() },
                { "grade", latestData.grade },
                { "gaugepercent", latestData.gauge.ToString() },
                { "lamp", latestData.clearLamp },
                { "exscore", latestData.ex.ToString() },
                { "pgreat", latestData.judges.pgreat.ToString() },
                { "great", latestData.judges.great.ToString() },
                { "good", latestData.judges.good.ToString() },
                { "bad", latestData.judges.bad.ToString() },
                { "poor", latestData.judges.poor.ToString() },
                { "fast", latestData.judges.fast.ToString() },
                { "slow", latestData.judges.slow.ToString() },
                { "combobreak", latestData.judges.combobreak.ToString() },
                { "playtype", latestData.judges.playtype.ToString() },
                { "style", latestData.settings.style.ToString() },
                { "style2", latestData.settings.style2.ToString() },
                { "gauge", latestData.settings.gauge.ToString() },
                { "assist", latestData.settings.assist.ToString() },
                { "range", latestData.settings.range.ToString() },
            };

            var content = new FormUrlEncodedContent(form);

            try
            {
                var response = await client.PostAsync(config.server + "/api/songplayed", content);
                //var response = await client.PostAsync("http://127.0.0.1:5000/api/songplayed", content);

                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseString);
            }
            catch
            {
                Console.WriteLine("Uploading failed");
            }


        }
        private static void Process_Exited(object sender, EventArgs e)
        {
            Cleanup();
            Environment.Exit(0);
        }


        private static gameState FetchGameState(IntPtr handle, gameState current, long position)
        {
            byte[] buffer_playdata = new byte[1008];
            int nRead = 0;
            short word = 4;
            ReadProcessMemory((int)handle, position, buffer_playdata, buffer_playdata.Length, ref nRead);

            var marker = BytesToInt32(buffer_playdata.Skip(word * 24).Take(word).ToArray(), 0, word);

            if (current == gameState.started && marker == 0)
            {
                return gameState.finished;
            }
            if (current == gameState.finished && marker != 0)
            {
                return gameState.started;
            }
            return current;
        }
        private static PlayData FetchPlayData(IntPtr handle, Dictionary<string, SongInfo> songDb, long position_playdata, long position_judgedata, long position_playsettings)
        {
            JudgeStats judges = FetchJudgeStats(handle, position_judgedata);
            PlaySettings settings = FetchPlaySettings(handle, position_playsettings, judges.playtype);

            PlayData data = new PlayData();
            data.judges = judges;
            data.settings = settings;

            if (settings.Hran || settings.battle)
            {
                data.report = false;
                Console.WriteLine($"Eww, {(settings.Hran ? "H-RAN" : "Battle")}");
                return data;
            }
            data.report = true;

            data.timestamp = DateTime.UtcNow;
            int bytesRead = 0;
            short word = 4;
            byte[] buffer = new byte[1008];

            ReadProcessMemory((int)handle, position_playdata, buffer, buffer.Length, ref bytesRead);

            var songID = BytesToInt32(buffer.Take(word).ToArray(), 0, word);
            var difficulty = BytesToInt32(buffer.Skip(word).Take(word).ToArray(), 0, word);
            var lamp = BytesToInt32(buffer.Skip(word * 6).Take(word).ToArray(), 0, word);
            var gauge = BytesToInt32(buffer.Skip(word * 8).Take(word).ToArray(), 0, word);
            data.songID = songID.ToString("00000");
            data.gauge = gauge;
            /* Lamp: 0-7, [noplay, fail, a-clear, e-clear, N, H, EX, FC] */
            string diff = "";
            switch (difficulty)
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

            var index = difficulty;
            var maxEx = songDb[data.songID].totalNotes[index] * 2;
            var exPart = (double)maxEx / 9;
            data.difficulty_index = index;

            var exscore = (data.judges.pgreat * 2 + data.judges.great);
            data.ex = exscore;
            var grade = "";
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
            var clearLamp = "";

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

            data.clearLamp = clearLamp;
            /* 0-9: [SPB, SPN, SPH, SPA, SPL, DPB, DPN, DPH, DPA, DPL] */
            data.difficulty = diff;
            /* DJLevel: 0-7, [AAA, AA, A, B, C, D, E, F] */
            data.grade = grade;
            return data;

        }
        private static JudgeStats FetchJudgeStats(IntPtr handle, long position)
        {
            JudgeStats result = new JudgeStats();

            int bytesRead = 0;
            short word = 4;

            byte[] buffer = new byte[1008];

            ReadProcessMemory((int)handle, position, buffer, buffer.Length, ref bytesRead);

            var style = playType.P1;
            var p1pgreat = BytesToInt32(buffer.Skip(word).Take(word).ToArray(), 0, word);
            var p1great = BytesToInt32(buffer.Skip(word * 2).Take(word).ToArray(), 0, word);
            var p1good = BytesToInt32(buffer.Skip(word * 3).Take(word).ToArray(), 0, word);
            var p1bad = BytesToInt32(buffer.Skip(word * 4).Take(word).ToArray(), 0, word);
            var p1poor = BytesToInt32(buffer.Skip(word * 5).Take(word).ToArray(), 0, word);
            var p2pgreat = BytesToInt32(buffer.Skip(word * 6).Take(word).ToArray(), 0, word);
            var p2great = BytesToInt32(buffer.Skip(word * 7).Take(word).ToArray(), 0, word);
            var p2good = BytesToInt32(buffer.Skip(word * 8).Take(word).ToArray(), 0, word);
            var p2bad = BytesToInt32(buffer.Skip(word * 9).Take(word).ToArray(), 0, word);
            var p2poor = BytesToInt32(buffer.Skip(word * 10).Take(word).ToArray(), 0, word);
            var p1cb = BytesToInt32(buffer.Skip(word * 11).Take(word).ToArray(), 0, word);
            var p2cb = BytesToInt32(buffer.Skip(word * 12).Take(word).ToArray(), 0, word);
            var p1fast = BytesToInt32(buffer.Skip(word * 13).Take(word).ToArray(), 0, word);
            var p2fast = BytesToInt32(buffer.Skip(word * 14).Take(word).ToArray(), 0, word);
            var p1slow = BytesToInt32(buffer.Skip(word * 15).Take(word).ToArray(), 0, word);
            var p2slow = BytesToInt32(buffer.Skip(word * 16).Take(word).ToArray(), 0, word);

            if (p1pgreat + p1great + p1good + p1bad + p1poor == 0)
            {
                style = playType.P2;
            }
            else if (p2pgreat + p2great + p2good + p2bad + p2poor > 0)
            {
                style = playType.DP;
            }

            result.playtype = style;
            result.pgreat = p1pgreat + p2pgreat;
            result.great = p1great + p2great;
            result.good = p1good + p2good;
            result.bad = p1bad + p2bad;
            result.poor = p1poor + p2poor;
            result.fast = p1fast + p2fast;
            result.slow = p1slow + p2slow;
            result.combobreak = p1cb + p2cb;

            return result;
        }
        private static PlaySettings FetchPlaySettings(IntPtr handle, long position, playType playstyle)
        {
            int bytesRead = 0;
            short word = 4;

            byte[] buffer = new byte[128];

            ReadProcessMemory((int)handle, position, buffer, buffer.Length, ref bytesRead);
            PlaySettings result = new PlaySettings();
            int style = 0;
            int gauge = 0;
            int assist = 0;
            int range = 0;
            int style2 = 0;
            if (playstyle == playType.P1)
            {
                style = BytesToInt32(buffer.Take(word).ToArray(), 0, word);
                gauge = BytesToInt32(buffer.Skip(word).Take(word).ToArray(), 0, word);
                assist = BytesToInt32(buffer.Skip(word * 2).Take(word).ToArray(), 0, word);
                range = BytesToInt32(buffer.Skip(word * 4).Take(word).ToArray(), 0, word);
            }
            else if (playstyle == playType.P2)
            {
                style = BytesToInt32(buffer.Skip(word * 12).Take(word).ToArray(), 0, word);
                gauge = BytesToInt32(buffer.Skip(word * 13).Take(word).ToArray(), 0, word);
                assist = BytesToInt32(buffer.Skip(word * 14).Take(word).ToArray(), 0, word);
                range = BytesToInt32(buffer.Skip(word * 16).Take(word).ToArray(), 0, word);
            }
            else /* DP */
            {
                style = BytesToInt32(buffer.Take(word).ToArray(), 0, word);
                gauge = BytesToInt32(buffer.Skip(word).Take(word).ToArray(), 0, word);
                assist = BytesToInt32(buffer.Skip(word * 2).Take(word).ToArray(), 0, word);
                range = BytesToInt32(buffer.Skip(word * 4).Take(word).ToArray(), 0, word);
                style2 = BytesToInt32(buffer.Skip(word * 5).Take(word).ToArray(), 0, word);
            }
            int flip = BytesToInt32(buffer.Skip(word * 3).Take(word).ToArray(), 0, word);
            int battle = BytesToInt32(buffer.Skip(word * 6).Take(word).ToArray(), 0, word);
            int Hran = BytesToInt32(buffer.Skip(word * 7).Take(word).ToArray(), 0, word);

            switch (style)
            {
                case 0: result.style = "OFF"; break;
                case 1: result.style = "RANDOM"; break;
                case 2: result.style = "R-RANDOM"; break;
                case 3: result.style = "S-RANDOM"; break;
                case 4: result.style = "MIRROR"; break;
            }
            switch (style2)
            {
                case 0: result.style2 = "OFF"; break;
                case 1: result.style2 = "RANDOM"; break;
                case 2: result.style2 = "R-RANDOM"; break;
                case 3: result.style2 = "S-RANDOM"; break;
                case 4: result.style2 = "MIRROR"; break;
            }

            switch (gauge)
            {
                case 0: result.gauge = "OFF"; break;
                case 1: result.gauge = "ASSISTED EASY"; break;
                case 2: result.gauge = "EASY"; break;
                case 3: result.gauge = "HARD"; break;
                case 4: result.gauge = "EX HARD"; break;
            }

            switch (assist)
            {
                case 0: result.assist = "OFF"; break;
                case 1: result.assist = "AUTO SCRATCH"; break;
                case 2: result.assist = "5KEYS"; break;
                case 3: result.assist = "LEGACY NOTE"; break;
                case 4: result.assist = "KEY ASSIST"; break;
                case 5: result.assist = "ANY KEY"; break;
            }

            switch (range)
            {
                case 0: result.range = "OFF"; break;
                case 1: result.range = "SUDDEN+"; break;
                case 2: result.range = "HIDDEN+"; break;
                case 3: result.range = "SUD+ & HID+"; break;
                case 4: result.range = "LIFT"; break;
                case 5: result.range = "LIFT & SUD+"; break;
            }
            result.flip = flip == 0 ? false : true;
            result.battle = battle == 0 ? false : true;
            result.Hran = Hran == 0 ? false : true;

            return result;
        }
        private static Dictionary<string, SongInfo> FetchSongDataBase(IntPtr processHandle, long current_position)
        {
            Dictionary<string, SongInfo> result = new Dictionary<string, SongInfo>();
            Console.WriteLine("Fetching available songs");
            while (true)
            {

                var songInfo = FetchSongInfo(processHandle, current_position);

                if (songInfo.title == null)
                {
                    Console.WriteLine("Songs fetched.");
                    break;
                }

                if (knownEncodingIssues.ContainsKey(songInfo.title))
                {
                    var old = songInfo.title;
                    songInfo.title = knownEncodingIssues[songInfo.title];
                    Console.WriteLine($"Fixed encoding issue \"{old}\" with \"{songInfo.title}\"");
                }
                if (knownEncodingIssues.ContainsKey(songInfo.artist))
                {
                    var old = songInfo.artist;
                    songInfo.artist = knownEncodingIssues[songInfo.artist];
                    Console.WriteLine($"Fixed encoding issue \"{old}\" with \"{songInfo.artist}\"");
                }
                if (!result.ContainsKey(songInfo.ID))
                {
                    result.Add(songInfo.ID, songInfo);
                }

                current_position += 0x3F0;

            }
            return result;
        }
        private static SongInfo FetchSongInfo(IntPtr handle, long position)
        {
            int bytesRead = 0;
            short slab = 64;
            short word = 4; /* Int32 */

            byte[] buffer = new byte[1008];

            ReadProcessMemory((int)handle, position, buffer, buffer.Length, ref bytesRead);

            var title1 = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Take(slab).Where(x => x != 0).ToArray());

            if (BytesToInt32(buffer.Take(slab).ToArray(), 0, slab) == 0)
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

            var bpmMax = BytesToInt32(bpms, 0, word);
            var bpmMin = BytesToInt32(bpms, word, word);

            string bpm = "NA";
            if (bpmMin != 0)
            {
                bpm = $"{bpmMin.ToString("000")}~{bpmMax.ToString("000")}";
            }
            else
            {
                bpm = bpmMax.ToString("000");
            }

            var noteCounts = new int[] { BytesToInt32(noteCounts_bytes, 0, word), BytesToInt32(noteCounts_bytes, word, word), BytesToInt32(noteCounts_bytes, word * 2, word), BytesToInt32(noteCounts_bytes, word * 3, word), BytesToInt32(noteCounts_bytes, word * 4, word), BytesToInt32(noteCounts_bytes, word * 5, word), BytesToInt32(noteCounts_bytes, word * 6, word), BytesToInt32(noteCounts_bytes, word * 7, word), BytesToInt32(noteCounts_bytes, word * 8, word), BytesToInt32(noteCounts_bytes, word * 9, word) };


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
        private static void Cleanup()
        {
            /* Save files and whatever that should carry over sessions */
        }

        static void ParseConfig()
        {
            var root = new ConfigurationBuilder().AddIniFile("config.ini").Build();
            config.server = root["connection:serveraddress"];
            config.api_key = root["connection:apikey"];

            config.output_db = bool.Parse(root["debug:outputdb"]);
        }
        static bool SongListAvailable(IntPtr handle, long offset)
        {
            byte[] buffer = new byte[64];
            int nRead = 0;
            ReadProcessMemory((int)handle, offset, buffer, buffer.Length, ref nRead);
            var title = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Where(x => x != 0).ToArray());
            var titleNoFilter = Encoding.GetEncoding("Shift-JIS").GetString(buffer);
            Console.WriteLine($"Read string: \"{title}\", expecting \"5.1.1.\"");
            return title.Contains("5.1.1.");
        }
        static void LoadEncodingFixes()
        {
            /* This shouldn't be necessary, Viva!, fffff, AETHER and Sweet Sweet Magic encoded fine during early development */
            foreach (var line in File.ReadAllLines("encodingfixes.txt"))
            {
                var pair = line.Split('\t');
                knownEncodingIssues.Add(pair[0], pair[1]);
            }
        }
        static Int32 BytesToInt32(byte[] input, int skip, int take)
        {
            if (skip == 0)
            {
                return BitConverter.ToInt32(input.Take(take).ToArray());
            }
            return BitConverter.ToInt32(input.Skip(skip).Take(take).ToArray());
        }
    }

    #region Custom objects
    enum gameState { started = 0, finished };
    enum offsetCategories { judgeInfo = 0, noteInfo, songInfo }
    public enum playType { P1 = 0, P2, DP }
    public struct Offsets
    {
        public long songList;
        public long judgeData;
        public long playData;
        public long playSettings;
    }
    public struct Config
    {
        public string server;
        public string api_key;
        public bool output_db;
    }
    public struct SongInfo
    {
        public string ID;
        public int[] totalNotes; /* SPB, SPN, SPH, SPA, SPL, DPB, DPN, DPH, DPA, DPL */
        public int[] level; /* SPB, SPN, SPH, SPA, SPL, DPB, DPN, DPH, DPA, DPL */
        public string title;
        public string title_english;
        public string artist;
        public string genre;
        public string bpm;
    }
    public struct PlayData
    {
        public DateTime timestamp;
        public bool report;
        public string songID;
        public string grade;
        public JudgeStats judges;
        public PlaySettings settings;
        public int gauge;
        public int ex;
        public string difficulty; 
        public int difficulty_index; 
        public string clearLamp;
    }
    public struct PlaySettings
    {
        public string style;
        public string style2; /* Style for 2p side in DP */
        public string gauge;
        public string assist;
        public string range;
        public bool flip;
        public bool battle;
        public bool Hran;
    }
    public struct JudgeStats
    {
        public playType playtype;
        public int pgreat;
        public int great;
        public int good;
        public int bad;
        public int poor;
        public int fast;
        public int slow;
        public int combobreak;
    }
    #endregion
}
