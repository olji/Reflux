using System;
using System.Collections.Generic;
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


        enum gameState { started = 0, finished };

        enum offsetCategories { judgeInfo = 0, noteInfo, songInfo }
        static HttpClient client = new HttpClient();
        static void Main(string[] args)
        {
            Process process = null;

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
            Console.WriteLine("Infinitas launched, waiting for song selection screen...");

            IntPtr processHandle = OpenProcess(0x0410, false, process.Id); /* Open process for memory read */

            var songDb = new Dictionary<string, SongInfo>();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var lastSongId = string.Empty;

            Offsets offsets = new Offsets();
            File.ReadAllLines("offsets.txt");
            foreach(var line in File.ReadAllLines("offsets.txt"))
            {
                var sections = line.Split('=');
                switch (sections[0].ToLower())
                {
                    case "judgedata": offsets.judgeData = Convert.ToInt64(sections[1]); break;
                    case "playdata": offsets.playData = Convert.ToInt64(sections[1]); break;
                    case "songlist": offsets.songList = Convert.ToInt64(sections[1]); break;
                    case "playsettings": offsets.playSettings = Convert.ToInt64(sections[1]); break;
                }
            }
            offsets.songList = 0x141D4A160;
            offsets.playData = 0x1414373D4;
            offsets.judgeData = 0x1415E6378;
            offsets.playSettings = 0x141437160;
            var current_position = offsets.songList;

            Console.WriteLine("Fetching available songs");
            while (true)
            {

                var songInfo = FetchSongInfo(processHandle, current_position);

                if (songInfo.title == null)
                {
                    Console.WriteLine("Songs fetched.");
                    break;
                }

                if (!songDb.ContainsKey(songInfo.ID))
                {
                    songDb.Add(songInfo.ID, songInfo);
                }

                current_position += 0x3F0;

                Console.WriteLine(songInfo.title);
            }

            gameState state = gameState.finished;

            int latestPlayTimer = 0;
            SongInfo selectedSong = new SongInfo();
            bool newclear = false;
            PlayData latestData = new PlayData();

            while (true)
            {
                var newstate = FetchGameState(processHandle, state, offsets.judgeData, ref latestPlayTimer);
                if (newstate != state)
                {
                    Console.Clear();
                    Console.WriteLine($"STATUS:{(newstate == gameState.finished ? " NOT" : "")} PLAYING");
                    if (newstate == gameState.finished)
                    {
                        latestData = FetchPlayData(processHandle, songDb, offsets.playData, offsets.judgeData);
                        Send_PlayData(songDb, latestData, new Config());
                        newclear = true;
                        Print_PlayData(songDb, latestData);
                    }
                }
                state = newstate;
                var currentSongId = GetLastPreviewId(processHandle);

                Thread.Sleep(2000);
            }

            Cleanup();
        }

        static void Print_PlayData(Dictionary<string, SongInfo> songDb, PlayData latestData)
        {
            Console.WriteLine("\nLATEST CLEAR:");

            Console.WriteLine($"{songDb[latestData.songID].title} {latestData.difficulty} [{songDb[latestData.songID].level[latestData.difficulty_index]}]");
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

        static async void Send_PlayData(Dictionary<string, SongInfo> songDb, PlayData latestData, Config config )
        {
            var form = new Dictionary<string, string>
            {
                { "apikey", "apikey" },
                { "title", songDb[latestData.songID].title },
                { "bpm", songDb[latestData.songID].bpm },
                { "artist", songDb[latestData.songID].artist },
                { "notecount", songDb[latestData.songID].totalNotes[latestData.difficulty_index].ToString() },
                { "diff", latestData.difficulty },
                { "level", songDb[latestData.songID].level[latestData.difficulty_index].ToString() },
                { "grade", latestData.grade },
                { "lamp", latestData.clearLamp },
                { "exscore", latestData.ex.ToString() },
                { "pgreat", latestData.judges.pgreat.ToString() },
                { "great", latestData.judges.great.ToString() },
                { "good", latestData.judges.good.ToString() },
                { "bad", latestData.judges.bad.ToString() },
                { "poor", latestData.judges.poor.ToString() },
                { "fast", latestData.judges.fast.ToString() },
                { "slow", latestData.judges.slow.ToString() },
                { "combobreak", latestData.judges.combobreak.ToString() }
            }; 

            /*
            var form = new Dictionary<string, string>();
            form.Add("apikey", "apikey");
            form.Add( "title", songDb[latestData.songID].title);
            form.Add( "bpm", songDb[latestData.songID].bpm);
            form.Add( "artist", songDb[latestData.songID].artist);
            form.Add( "notecount", songDb[latestData.songID].totalNotes[latestData.difficulty_index].ToString());
            form.Add( "diff", latestData.difficulty);
            form.Add( "level", songDb[latestData.songID].level[latestData.difficulty_index].ToString());
            form.Add( "grade", latestData.grade);
            form.Add( "lamp", latestData.clearLamp);
            form.Add( "exscore", latestData.ex.ToString());
            form.Add( "pgreat", latestData.judges.pgreat.ToString());
            form.Add( "great", latestData.judges.great.ToString());
            form.Add( "good", latestData.judges.good.ToString());
            form.Add( "bad", latestData.judges.bad.ToString());
            form.Add( "poor", latestData.judges.poor.ToString());
            form.Add( "fast", latestData.judges.fast.ToString());
            form.Add( "slow", latestData.judges.slow.ToString());
            form.Add( "combobreak", latestData.judges.combobreak.ToString());
            */

            var content = new FormUrlEncodedContent(form);

            //var response = await client.PostAsync(config.server, content);
            var response = await client.PostAsync("http://127.0.0.1:5000/api/songplayed", content);

            var responseString = await response.Content.ReadAsStringAsync();

            Console.WriteLine(responseString);

        }
        private static void Process_Exited(object sender, EventArgs e)
        {
            Cleanup();
            Environment.Exit(0);
        }


        //private static gameState FetchGameState(IntPtr handle, gameState current, long positionSettings, long positionPlayData, ref int prevTimer)
        private static gameState FetchGameState(IntPtr handle, gameState current, long position, ref int prevTimer)
        {
            byte[] buffer_playdata = new byte[1008];
            byte[] buffer_settings = new byte[1008];
            int nRead = 0;
            short word = 4;
            ReadProcessMemory((int)handle, position, buffer_playdata, buffer_playdata.Length, ref nRead);
            //ReadProcessMemory((int)handle, positionSettings, buffer_settings, buffer_settings.Length, ref nRead);

            var playTimer = buffer_playdata.Skip(word * 30).Take(word).ToArray();
            var timerValue = BytesToInt32(playTimer, 0, word);

            var marker = BytesToInt32(buffer_playdata.Skip(word * 24).Take(word).ToArray(), 0, word);


            //var inMenu = BytesToInt(buffer_settings.Skip(word * 21).Take(word).ToArray(), 0, word);

            bool songStarted = timerValue < prevTimer;
            prevTimer = timerValue;

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
        private static PlayData FetchPlayData(IntPtr handle, Dictionary<string, SongInfo> songDb, long position_playdata, long position_judgedata)
        {
            JudgeStats judges = FetchJudgeStats(handle, position_judgedata);
            PlayData data = new PlayData();
            data.judges = judges;

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
        private static string GetLastPreviewId(IntPtr processHandle)
        {
            int bytesRead = 0;

            byte[] buffer = new byte[5];

            // this is the address of the song playing preview file, for example 01006_pre.2dx
            ReadProcessMemory((int)processHandle, 0x141D15008, buffer, buffer.Length, ref bytesRead);

            var lastBgmSongId = Encoding.UTF8.GetString(buffer);

            return lastBgmSongId;
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

            var noteCounts = new int[] { BytesToInt32(noteCounts_bytes, 0, word), BytesToInt32(noteCounts_bytes, word, word), BytesToInt32(noteCounts_bytes, word*2, word), BytesToInt32(noteCounts_bytes, word*3, word), BytesToInt32(noteCounts_bytes, word*4, word), BytesToInt32(noteCounts_bytes, word*5, word), BytesToInt32(noteCounts_bytes, word*6, word), BytesToInt32(noteCounts_bytes, word*7, word), BytesToInt32(noteCounts_bytes, word*8, word), BytesToInt32(noteCounts_bytes, word*9, word)};


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

        static Int32 BytesToInt32(byte[] input, int skip, int take)
        {
            if(skip == 0)
            {
                return BitConverter.ToInt32(input.Take(take).ToArray());
            }
            return BitConverter.ToInt32(input.Skip(skip).Take(take).ToArray());
        }
    }

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
    }
    public struct SongInfo
    {
        public string ID;
        public int[] totalNotes; /* SPN, SPH, SPA, DPN, DPH, DPA */
        public int[] level; /* SPN, SPH, SPA, DPN, DPH, DPA */
        public string title;
        public string title_english;
        public string artist;
        public string genre;
        public string bpm;
    }
    public struct PlayData
    {
        public string songID;
        public string grade;
        public JudgeStats judges;
        public int gauge;
        public int ex;
        public string difficulty; 
        public int difficulty_index; 
        public string clearLamp;
    }
    public struct JudgeStats
    {
        public int pgreat;
        public int great;
        public int good;
        public int bad;
        public int poor;
        public int fast;
        public int slow;
        public int combobreak;
    }
}
