using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Net.Http;
using System.IO;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;

namespace infinitas_statfetcher
{
    class Network
    {
        readonly static HttpClient client = new HttpClient();
        static string GetLatestSupportFile(string filename)
        {
            var builder = new UriBuilder(Config.UpdateServer + $"/{filename}.txt");
            var response = client.GetStringAsync(builder.Uri);
            response.Wait();
            return response.Result;
        }
        static string GetLatestOffset()
        {
            var builder = new UriBuilder(Config.UpdateServer + "/offsets.txt");
            var response = client.GetStringAsync(builder.Uri);
            response.Wait();
            return response.Result;
        }
        public static void UpdateSupportFile(string filename)
        {
            string currentVersion = File.ReadLines($"{filename}.txt").First();
            string content;
            try
            {
                content = GetLatestSupportFile(filename);
            } catch
            {
                Console.WriteLine($"Failed to fetch {filename}.txt from master");
                return;
            }
            var netVersion = "";
            using (var reader = new StringReader(content))
            {
                netVersion = reader.ReadLine().Trim();
            }
            DateTime current;
            DateTime net;
            try
            {
                current = DateTime.ParseExact(currentVersion.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture);
                net = DateTime.ParseExact(netVersion.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture);
                if (current < net)
                {
                    Console.WriteLine($"Found a more recent entry of {filename}.txt at master.");
                    if (!Directory.Exists("archive"))
                    {
                        Directory.CreateDirectory("archive");
                    }
                    File.Move($"{filename}.txt", $"archive/{filename}_{currentVersion}.txt", true);
                    File.WriteAllText($"{filename}.txt", content);
                }
            }
            catch (Exception e)
            {
                Utils.Except(e, $"FileUpdate_{filename}");
                throw;
            }
        }
        public static bool UpdateOffset(string version)
        {
            var filecontent = GetLatestOffset();
            var fileversion = "";
            using (var reader = new StringReader(filecontent))
            {
                fileversion = reader.ReadLine().Trim(); /* Handles several kinds of newline formats */
            }
            if (fileversion != version)
            {
                Console.WriteLine($"Latest offsets available are for build {fileversion}, which didn't match detected version {version}");
                return false;
            }
            if (!Directory.Exists("archive"))
            {
                Directory.CreateDirectory("archive");
            }
            var v = File.ReadAllLines("offsets.txt")[0];
            File.Move("offsets.txt", $"archive/{v.Replace(':', '_')}.txt", true);
            File.WriteAllText("offsets.txt", filecontent);
            Offsets.LoadOffsets("offsets.txt");
            return true;
        }
        public static async void UpdateChartUnlockType(SongInfo song)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "apikey", Config.API_key },
                    { "songid", song.ID },
                    { "unlockType", song.type.ToString() },
                }
            );
            var response = await client.PostAsync(Config.Server + "/api/updatesong", content);
            Utils.Debug(await response.Content.ReadAsStringAsync());
        }
        public static async void ReportUnlock(string songid, UnlockData unlocks)
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "apikey", Config.API_key },
                    { "songid", songid },
                    { "state", unlocks.unlocks.ToString()}
                }
            );
            var response = await client.PostAsync(Config.Server + "/api/unlocksong", content);
            Utils.Debug(await response.Content.ReadAsStringAsync());
        }
        public static async void ReportUnlocks(Dictionary<string, UnlockData> unlocks)
        {
            foreach (var keyval in unlocks)
            {
                ReportUnlock(keyval.Key, keyval.Value);
            }
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
                    { "apikey", Config.API_key },
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
                    { "apikey", Config.API_key },
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
                try
                {
                    ChartInfo chart = new ChartInfo()
                    {
                        level = song.level[i],
                        songid = songid,
                        difficulty = (Difficulty)i,
                        totalNotes = song.totalNotes[i],
                        unlocked = Utils.GetUnlockStateForDifficulty(songid, (Difficulty)i)
                    };

                    AddChart(chart);
                }
                catch
                {

                }

            }
        }
        public static async Task<int?> Kamai_GetSongID(string marqueeTitle)
        {
            var search = marqueeTitle;
            var client = new HttpClient();
            var builder = new UriBuilder("https://api.kamaitachi.xyz/v1/games/iidx/songs/search");
            bool songFound = false, giveup = false;
            int included_sections = 0;
            int? songID = null;
            do
            {
                builder.Query = $"title={search}";
                var response = await client.GetStringAsync(builder.Uri);
                JObject json = JsonConvert.DeserializeObject<JObject>(response);
                if (json["description"].ToString().Contains("Found 1"))
                {
                    songFound = true;
                    songID = (int)json["body"][0]["id"];
                }
                else if (json["description"].ToString().Contains("Found 0"))
                {
                    if (search != marqueeTitle)
                    {
                        giveup = true;
                    }
                    search = marqueeTitle.Split(' ')[0];
                    included_sections = 1;
                }
                else
                {
                    try
                    {
                        search += " " + marqueeTitle.Split(' ')[included_sections];
                        included_sections++;
                    }
                    catch
                    {
                        giveup = true;
                    }
                }
            } while (!songFound && !giveup);
            return songID;
        }
    }
}
