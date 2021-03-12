﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Net.Http;
using System.IO;
using System.Web;

namespace infinitas_statfetcher
{
    class Network
    {
        readonly static HttpClient client = new HttpClient();
        static string GetLatestOffset()
        {
            var builder = new UriBuilder(Config.UpdateServer + "/offsets.txt");
            var response = client.GetStringAsync(builder.Uri);
            response.Wait();
            return response.Result;
        }
        public static bool UpdateOffset(string version)
        {
            var filecontent = GetLatestOffset();
            var fileversion = "";
            using (var reader = new StringReader(filecontent))
            {
                fileversion = reader.ReadLine().Trim(); /* Handles several kinds of newline formats */
            }
            if (fileversion != version) {
                Console.WriteLine($"Latest offsets available are for build {fileversion}, which didn't match detected version {version}");
                return false;
            }
            if (!Directory.Exists("archive")) {
                Directory.CreateDirectory("archive");
            }
            var v = File.ReadAllLines("offsets.txt")[0];
            File.Move("offsets.txt", $"archive/{v.Replace(':', '_')}.txt", true);
            File.WriteAllText("offsets.txt", filecontent);
            Offsets.LoadOffsets("offsets.txt");
            return true;
        }
        public static async void ReportUnlocks(Dictionary<string, UnlockData> unlocks)
        {
            foreach (var keyval in unlocks)
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "songid", keyval.Key },
                    { "state", keyval.Value.unlocks.ToString()}
                }
                );
                var response = await client.PostAsync(Config.Server + "/api/unlocksong", content);
                Utils.Debug(await response.Content.ReadAsStringAsync());
            }
        }
        public static void UpdateEncodingFixes()
        {
        }
        public static async void SendPlayData(PlayData latestData)
        {
            var content = new FormUrlEncodedContent(latestData.ToPostForm());

            try
            {
                Console.WriteLine($"Saving to server {Config.Server}");
                var response = await client.PostAsync(Config.Server + "/api/songplayed", content);

                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseString);
            }
            catch
            {
                Console.WriteLine("Uploading failed");
            }
        }
        public static async void AddSong(SongInfo song)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "songid", song.ID },
                    { "unlockType", song.type.ToString() },
                    { "title", song.title},
                    { "title2", song.title_english},
                    { "artist", song.artist},
                    { "genre", song.genre},
                    { "bpm", song.bpm}
                }
            );
            var response = await client.PostAsync(Config.Server + "/api/addsong", content);
            Utils.Debug(await response.Content.ReadAsStringAsync());
        }
        public static async void AddChart(ChartInfo chart)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "songid", chart.songid },
                    { "unlocked", chart.unlocked.ToString() },
                    { "diff", chart.difficulty.ToString()},
                    { "level", chart.level.ToString()},
                    { "notecount", chart.totalNotes.ToString()}
                }
            );
            var response = await client.PostAsync(Config.Server + "/api/addchart", content);
            Utils.Debug(await response.Content.ReadAsStringAsync());

        }

        public static async void UploadSongInfo(string songid)
        {
            var song = Utils.songDb[songid];

            Utils.Debug($"Song {songid}");

            AddSong(song);

            for (int i = 0; i < song.level.Length; i++)
            {
                if (i == 0 || i == 5 || song.level[i] == 0) { continue; }

                Thread.Sleep(10);
                ChartInfo chart = new ChartInfo() {
                    level = song.level[i],
                    songid = songid,
                    difficulty = Utils.IntToDiff(i),
                    totalNotes = song.totalNotes[i],
                    unlocked = Utils.GetUnlockStateForDifficulty(songid, i)
                };

                AddChart(chart);

            }
        }
    }
}