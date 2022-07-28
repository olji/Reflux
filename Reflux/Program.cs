using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Reflux
{
    class Program
    {
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
            Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);
        /* Import external methods */
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        static void Main()
        {
            Utils.CheckVersion();
            Config.Parse("config.ini");

            #region Parse unlockdb file
            Dictionary<string, Tuple<int, int>> unlock_db = new Dictionary<string, Tuple<int, int>>();
            bool init = false;
            if (Config.Save_remote)
            {
                if (File.Exists("unlockdb"))
                {
                    foreach (var line in File.ReadAllLines("unlockdb"))
                    {
                        var s = line.Split(',');
                        unlock_db.Add(s[0], new Tuple<int, int>(int.Parse(s[1]), int.Parse(s[2])));
                    }
                }
                else
                {
                    init = true;
                    Console.WriteLine("unlockdb not found, will initialize all songs on remote server");
                }
            }
            #endregion

            #region Set up session and json file naming

            DateTime now = DateTime.Now;

            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-us");
            DateTimeFormatInfo dtformat = culture.DateTimeFormat;
            dtformat.TimeSeparator = "_";

            DirectoryInfo sessionDir = new DirectoryInfo("sessions");
            if (Config.Save_local)
            {
                if (!sessionDir.Exists)
                {
                    sessionDir.Create();
                }
            }
            var sessionFile = new FileInfo(Path.Combine(sessionDir.FullName, $"Session_{now:yyyy_MM_dd_hh_mm_ss}.tsv"));
            var jsonfile = new FileInfo(Path.Combine(sessionDir.FullName, $"Session_{now:yyyy_MM_dd_hh_mm_ss}.json"));
            #endregion

            #region Repeadedly attempt hooking to application
            do
            {
                Process process = null;
                Console.WriteLine("Trying to hook to INFINITAS...");
                do
                {
                    var processes = Process.GetProcessesByName("bm2dx");
                    if (processes.Any())
                    {
                        process = processes[0];
                    }

                    Thread.Sleep(2000);
                } while (process == null);

                Console.Clear();
                Console.WriteLine("Hooked to process, waiting until song list is loaded...");


                IntPtr processHandle = OpenProcess(0x0410, false, process.Id); /* Open process for memory read */
                Utils.handle = processHandle;

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                var bm2dxModule = process.MainModule;
                Offsets.LoadOffsets("offsets.txt");

                /* Figure out if offset file is relevant */
                #region Figure out if offset file is relevant
                Utils.Debug($"Baseaddr is {bm2dxModule.BaseAddress.ToString("X")}");
                byte[] buffer = new byte[80000000]; /* 80MB */
                int nRead = 0;
                ReadProcessMemory((int)processHandle, (long)bm2dxModule.BaseAddress, buffer, buffer.Length, ref nRead);
                string versionSearch = "P2D:J:B:A:";
                var str = Encoding.GetEncoding("Shift-JIS").GetString(buffer);
                bool correctVersion = false;
                string foundVersion = "";
                for (int i = 0; i < str.Length - Offsets.Version.Length; i++)
                {

                    if (str.Substring(i, versionSearch.Length) == versionSearch)
                    {
                        foundVersion = str.Substring(i, Offsets.Version.Length);
                        Utils.Debug($"Found version {foundVersion} at address +0x{i:X}");
                        /* Don't break, first two versions appearing are referring to 2016-builds, actual version appears later */
                    }
                }

                if (foundVersion != Offsets.Version)
                {
                    if (Config.UpdateFiles)
                    {
                        Console.WriteLine($"The datecodes of Infinitas ({foundVersion}) and Reflux ({Offsets.Version}) don't match.  An update is available.\nUpdating...");
                        correctVersion = Network.UpdateOffset(foundVersion);
                    }
                    else
                    {
                        Console.WriteLine($"The datecodes of Infinitas ({foundVersion}) and Reflux ({Offsets.Version}) don't match.  An update is not currently available.\n \nThis is normal after an Infinitas update, and fixed as soon as we are made aware.\n \nPlease ping Okapi or another dev for assistance.") ;
                    }
                }
                else
                {
                    correctVersion = true;
                }
                Network.UpdateSupportFile("encodingfixes");
                Network.UpdateSupportFile("customtypes");
                Utils.LoadEncodingFixes();
                Utils.LoadCustomTypes();

                if (!correctVersion)
                {
                    Console.WriteLine("Reflux will now exit.");
                    Console.ReadLine();
                    return;
                }
                #endregion

                #region Wait until data is properly loaded

                bool songlistFetched = false;
                bool processExit = false;
                while (!songlistFetched)
                {
                    /* Don't fetch song list until it seems loaded */
                    while (!Utils.DataLoaded()) { 
                        Thread.Sleep(5000);
                        if (process.HasExited)
                        {
                            processExit = true;
                            break;
                        }
                    } 
                    if (process.HasExited || processExit)
                    {
                        processExit = true;
                        break;
                    }
                    Thread.Sleep(1000); /* Extra sleep just to avoid potentially undiscovered race conditions */
                    Utils.FetchSongDataBase();
                    if (Utils.songDb["80003"].totalNotes[3] < 10) /* If Clione (Ryu* Remix) SPH has less than 10 notes, the songlist probably wasn't completely populated when we fetched it. That memory space generally tends to hold 0, 2 or 3, depending on which 'difficulty'-doubleword you're reading */
                    {
                        Utils.Debug("Notecount data seems bad, retrying fetching in case list wasn't fully populated.");
                        Thread.Sleep(5000);
                    }
                    else
                    {
                        songlistFetched = true;
                    }
                }
                if (processExit)
                {
                    continue;
                }

                #endregion

                Console.WriteLine("Song list loaded.");

                #region Create session and json file if configured to track those

                if (Config.Save_local)
                {
                    File.Create(sessionFile.FullName).Close();
                    WriteLine(sessionFile, Config.GetTsvHeader());
                }

                if (Config.Save_json)
                {
                    JObject head = new JObject();
                    head["service"] = "Infinitas";
                    head["game"] = "iidx";
                    JObject json = new JObject();
                    json["head"] = head;
                    json["body"] = new JArray();
                    File.WriteAllText(jsonfile.FullName, json.ToString());
                }

                #endregion

                /* Primarily for debugging and checking for encoding issues */
                if (Config.Output_songlist)
                {
                    List<string> p = new List<string>() { "id\ttitle\ttitle2\tartist\tgenre" };
                    foreach (var v in Utils.songDb)
                    {
                        p.Add($"{v.Key}\t{v.Value.title}\t{v.Value.title_english}\t{v.Value.artist}\t{v.Value.genre}");
                    }
                    File.WriteAllLines("songs.tsv", p.ToArray());
                }

                Utils.GetUnlockStates();

                Console.WriteLine("Fetching song scoring data...");
                ScoreMap.LoadMap();
                Tracker.LoadTracker();

                #region Sync with server

                if (Config.Save_remote)
                {
                    Console.WriteLine("Checking for songs/charts to update at remote.");
                    int songcount = Utils.songDb.Where(song => !unlock_db.ContainsKey(song.Key)).Count();
                    Console.WriteLine($"Found {songcount} songs to upload to remote");

                    int i = 0;
                    foreach (var song in Utils.songDb)
                    {
                        if (songcount > 0)
                        {
                            double percent = ((double)i) / songcount * 100;
                            Console.Write($"\rProgress: {percent.ToString("0.##")}%   ");
                        }
                        /* Upload new songs */
                        if (!unlock_db.ContainsKey(song.Key) || init)
                        {
                            Network.UploadSongInfo(song.Key);
                        }
                        /* Upload changes to song (unlock type and unlock status) */
                        if (unlock_db.ContainsKey(song.Key))
                        {
                            if (unlock_db[song.Key].Item1 != (int)Utils.unlockDb[song.Key].type)
                            {
                                Network.UpdateChartUnlockType(song.Value);
                            }
                            if (unlock_db[song.Key].Item2 != Utils.unlockDb[song.Key].unlocks)
                            {
                                Network.ReportUnlock(song.Key, Utils.unlockDb[song.Key]);
                            }
                        }
                        i++;
                    }
                    Console.WriteLine("\rDone            ");
                }

                #endregion


                GameState state = GameState.songSelect;

                Console.WriteLine("Initialized and ready");

                string chart = "";

                if (Config.Stream_Marquee)
                {
                    Utils.Debug("Updating marquee.txt");
                    File.WriteAllText("marquee.txt", Config.MarqueeIdleText);
                }

                if (Config.Stream_Playstate)
                {
                    Utils.Debug("Writing initial menu state to playstate.txt");
                    File.WriteAllText("playstate.txt", "menu");
                }

                if (Config.Stream_FullSongInfo)
                {
                    File.WriteAllText("title.txt", String.Empty);
                    File.WriteAllText("artist.txt", String.Empty);
                    File.WriteAllText("englishtitle.txt", String.Empty);
                    File.WriteAllText("genre.txt", String.Empty);
                    File.WriteAllText("level.txt", String.Empty);
                    File.WriteAllText("playstate.txt", "menu"); // force menu switch here due to a bug
                }
                /* Main loop */
                while (!process.HasExited)
                {
                    try
                    {
                        var newstate = Utils.FetchGameState(state);
                        Utils.Debug(newstate.ToString());
                        if (newstate != state)
                        {
                            Console.Clear();
                            Console.WriteLine($"STATUS:{(newstate != GameState.playing ? " NOT" : "")} PLAYING");
                            if (newstate == GameState.resultScreen)
                            {
                                Thread.Sleep(1000); /* Sleep to avoid race condition */
                                var latestData = new PlayData();
                                latestData.Fetch();
                                if (latestData.DataAvailable)
                                {
                                    if (Config.Save_remote)
                                    {
                                        Network.SendPlayData(latestData);
                                    }
                                    if (Config.Save_local)
                                    {
                                        try
                                        {
                                            /* Update best lamp/grade */
                                            Chart c = new Chart() { songID = latestData.Chart.songid, difficulty = latestData.Chart.difficulty };
                                            var entry = Tracker.trackerDb[c];
                                            entry.grade = (Grade)Math.Max((int)entry.grade, (int)latestData.Grade);
                                            entry.lamp = (Lamp)Math.Max((int)entry.lamp, (int)latestData.Lamp);
                                            entry.misscount = Math.Min(entry.misscount, latestData.MissCount);
                                            entry.ex_score = Math.Max(entry.ex_score, latestData.ExScore);
                                            Tracker.trackerDb[c] = entry;
                                            Tracker.SaveTracker();

                                            WriteLine(sessionFile, latestData.GetTsvEntry());
                                        }
                                        catch (Exception e)
                                        {
                                            Utils.Except(e, "SessionEntry");
                                        }
                                    }
                                    if (Config.Save_json)
                                    {
                                        var entry = latestData.GetJsonEntry();
                                        var json = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(jsonfile.FullName));
                                        JArray arr = (JArray)json["body"];
                                        arr.Add(entry);
                                        json["body"] = arr;
                                        File.WriteAllText(jsonfile.FullName, json.ToString());
                                    }
                                    if (Config.Save_latestJson)
                                    {
                                        var entry = latestData.ToPostForm();
                                        var json = JsonConvert.SerializeObject(entry);
                                        File.WriteAllText("latest.json", json);
                                    }
                                    if (Config.Save_latestTxt)
                                    {
                                        File.WriteAllText("latest-grade.txt", latestData.Grade.ToString());
                                        File.WriteAllText("latest-lamp.txt", latestData.expandLamp(latestData.Lamp));
                                        File.WriteAllText("latest-difficulty.txt", latestData.Chart.difficulty.ToString());
                                        File.WriteAllText("latest-difficulty-color.txt",

                                            latestData.Chart.difficulty.ToString().EndsWith("N") ? "#0FABFD" :
                                            latestData.Chart.difficulty.ToString().EndsWith("H") ? "#F4903C" :
                                            latestData.Chart.difficulty.ToString().EndsWith("A") ? "#E52B19" :
                                            "#0FABFD"
                                            );

                                        File.WriteAllText("latest-titleenglish.txt", latestData.Chart.title_english);
                                        File.WriteAllText("latest.txt", latestData.Chart.title_english + Environment.NewLine + latestData.Grade.ToString() + Environment.NewLine + latestData.Lamp.ToString());
                                    }
                                }
                                try
                                {
                                    Print_PlayData(latestData);
                                }
                                catch (Exception e)
                                {
                                    Utils.Except(e, "ConsoleOutput");
                                }
                                if (Config.Stream_Playstate)
                                {
                                    Utils.Debug("Writing menu state to playstate.txt");
                                    File.WriteAllText("playstate.txt", "menu");
                                }
                                if (Config.Stream_Marquee)
                                {
                                    Utils.Debug("Updating marquee.txt");
                                    var clearstatus = latestData.Lamp == Lamp.F ? "FAIL!" : "CLEAR!";
                                    File.WriteAllText("marquee.txt", $"{chart} {clearstatus}");
                                }
                            }
                            else if (newstate == GameState.songSelect)
                            {
                                if (Config.Stream_Marquee)
                                {
                                    Utils.Debug("Updating marquee.txt");
                                    File.WriteAllText("marquee.txt", Config.MarqueeIdleText);
                                }

                                if (Config.Stream_FullSongInfo)
                                {
                                    File.WriteAllText("title.txt", String.Empty);
                                    File.WriteAllText("artist.txt", String.Empty);
                                    File.WriteAllText("englishtitle.txt", String.Empty);
                                    File.WriteAllText("genre.txt", String.Empty);
                                    File.WriteAllText("level.txt", String.Empty);
                                    File.WriteAllText("folder.txt", "-");
                                    File.WriteAllText("playstate.txt", "menu"); // force menu switch here due to a bug
                                }
                            }
                            else
                            {
                                if (Config.Stream_Playstate)
                                {
                                    Utils.Debug("Writing play state to playstate.txt");
                                    File.WriteAllText("playstate.txt", "play");
                                }
                                if (Config.Stream_Marquee)
                                {
                                    chart = Utils.CurrentChart();
                                    Utils.Debug($"Writing {chart} to marquee.txt");
                                    File.WriteAllText("marquee.txt", chart.ToUpper());

                                }
                                if (Config.Stream_FullSongInfo)
                                {
                                    var song = Utils.songDb[Utils.FetchCurrentChart().songID];
                                    File.WriteAllText("title.txt", song.title);
                                    File.WriteAllText("artist.txt", song.artist);
                                    File.WriteAllText("englishtitle.txt", song.title_english);
                                    File.WriteAllText("genre.txt", song.genre);
                                    File.WriteAllText("folder.txt", song.folder.ToString());
                                    File.WriteAllText("level.txt", song.level.ToString());
                                }
                            }
                        }
                        state = newstate;

                        if (state == GameState.songSelect)
                        {
                            var newUnlocks = Utils.UpdateUnlockStates();
                            if (Config.Save_local)
                            {
                                Utils.Debug("Saving tracker data tsv");
                                Tracker.SaveTrackerData("tracker.tsv");
                            }
                            if (Config.Save_remote && newUnlocks.Count > 0)
                            {
                                Network.ReportUnlocks(newUnlocks);
                            }
                        }

                        Thread.Sleep(2000);
                    }
                    catch (Exception e)
                    {
                        Utils.Except(e, "MainLoop");
                    }
                }
                if (Config.Save_local)
                {
                    Tracker.SaveTrackerData("tracker.tsv");
                }
                if (Config.Stream_Playstate)
                {
                    Utils.Debug("Writing menu state to playstate.txt");
                    File.WriteAllText("playstate.txt", "off");
                }
                if (Config.Stream_Marquee)
                {
                    Utils.Debug($"Writing NO SIGNAL to marquee.txt");
                    File.WriteAllText("marquee.txt", "NO SIGNAL");
                }
            } while (true);
            #endregion
        }

        /// <summary>
        /// Prints latest play data to the screen
        /// </summary>
        /// <param name="latestData"></param>
        static void Print_PlayData(PlayData latestData)
        {
            Console.WriteLine("\nLATEST CLEAR:");

            var header = Config.GetTsvHeader();
            var entry = latestData.GetTsvEntry();

            var h = header.Split('\t');
            var e = entry.Split('\t');
            for (int i = 0; i < h.Length; i++)
            {
                Console.WriteLine("{0,15}: {1,-50}", h[i], e[i]);
            }
        }
        static void WriteLine(FileInfo file, string str)
        {
            File.AppendAllLines(file.FullName, new string[] { str });
        }
    }

    #region Custom objects
    /// <summary>
    /// Enum for representing what screen is currently loaded
    /// </summary>
    enum GameState { playing = 0, resultScreen, songSelect };
    /// <summary>
    /// Enum for representing what playtype the player is using
    /// </summary>
    public enum PlayType { P1 = 0, P2, DP }
    #endregion
}
