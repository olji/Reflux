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
using System.Net;

namespace Reflux
{
    class Network
    {
        readonly static HttpClient client = new HttpClient();
        public static string GetLatestVersion()
        {
            var req = WebRequest.Create("https://github.com/olji/Reflux/releases/latest");
            req.Method = "HEAD";
            var response = req.GetResponse() as HttpWebResponse;
            return response.ResponseUri.Segments.Last();
        }
        /// <summary>
        /// Fetch file content of target and return
        /// </summary>
        /// <param name="filename">File to fetch</param>
        /// <returns>File contents</returns>
        static string GetLatestSupportFile(string filename)
        {
            var builder = new UriBuilder(Config.UpdateServer + $"/{filename}.txt");
            var response = client.GetStringAsync(builder.Uri);
            response.Wait();
            return response.Result;
        }
        /// <summary>
        /// Check version of support file with what's at server and replace if newer
        /// </summary>
        /// <param name="filename">File to process</param>
        public static void UpdateSupportFile(string filename, string version = "")
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
        /// <summary>
        /// Fetches latest version of offsets.txt and see if it's version matches what is needed, replace current if version matches.
        /// </summary>
        /// <param name="version">Version to match</param>
        /// <returns>true if net version matched for version, false otherwise</returns>
        public static bool UpdateOffset(string version)
        {
            var filecontent = GetLatestSupportFile("offsets");
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
        /// <summary>
        /// Update the unlock type for a chart at remote
        /// </summary>
        /// <param name="song"></param>
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
        /// <summary>
        /// Update unlock state of charts for song at remote
        /// </summary>
        /// <param name="songid">Song whose charts are to be updated</param>
        /// <param name="unlocks">Unlock information for song</param>
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
        /// <summary>
        /// Report unlock state for multiple songs
        /// </summary>
        /// <param name="unlocks">Dictionary where key is songIDs, and values are the unlock information</param>
        public static async void ReportUnlocks(Dictionary<string, UnlockData> unlocks)
        {
            foreach (var keyval in unlocks)
            {
                ReportUnlock(keyval.Key, keyval.Value);
            }
        }
        /// <summary>
        /// Send play information to remote
        /// </summary>
        /// <param name="latestData">Play information</param>
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
        /// <summary>
        /// Add a new song at remote
        /// </summary>
        /// <param name="song">Song metadata</param>
        static HttpStatusCode AddSong(SongInfo song)
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
            var response = client.PostAsync(Config.Server + "/api/addsong", content);
            response.Wait();
            Utils.Debug(response.Result.Content.ReadAsStringAsync().Result);
            return response.Result.StatusCode;
        }
        /// <summary>
        /// Add a new chart for a song at remote
        /// </summary>
        /// <param name="chart">Metadata for one chart difficulty</param>
        /// <returns>Server response</returns>
        static HttpStatusCode AddChart(ChartInfo chart)
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
            var response = client.PostAsync(Config.Server + "/api/addchart", content);
            Utils.Debug(response.Result.Content.ReadAsStringAsync().Result);
            return response.Result.StatusCode;
        }
        /// <summary>
        /// Post score to remote
        /// </summary>
        /// <param name="chart">Chart information</param>
        /// <param name="exscore">Ex score</param>
        /// <param name="misscount">Miss count</param>
        /// <param name="lamp">Clear state</param>
        /// <returns></returns>
        static HttpStatusCode PostScore(ChartInfo chart, int exscore, int misscount, Lamp lamp)
        {
            var grade = (lamp == Lamp.NP && exscore == 0) ? Grade.NP : Utils.ScoreToGrade(chart.songid, chart.difficulty, exscore);
            var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "apikey", Config.API_key },
                    { "songid", chart.songid },
                    { "diff", chart.difficulty.ToString()},
                    { "exscore", exscore.ToString()},
                    { "misscount", misscount.ToString()},
                    { "grade", grade.ToString()},
                    { "lamp", lamp.ToString()}
                }
            );
            var response = client.PostAsync(Config.Server + "/api/postscore", content);
            Utils.Debug(response.Result.Content.ReadAsStringAsync().Result);
            return response.Result.StatusCode;
        }
        /// <summary>
        /// Upload new song and chart data to remote
        /// </summary>
        /// <param name="songid">ID of song to add</param>
        public static async void UploadSongInfo(string songid)
        {
            var song = Utils.songDb[songid];

            //Utils.Debug($"Song {songid}");

            HttpStatusCode response = HttpStatusCode.InternalServerError;
            do
            {
                response = AddSong(song);
            } while (response != HttpStatusCode.OK);

            for (int i = 0; i < song.level.Length; i++)
            {
                if (i == 0 || i == 5 || song.level[i] == 0) { continue; }

                Thread.Sleep(20);
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

                    do
                    {
                        response = AddChart(chart);
                    } while (response != HttpStatusCode.OK);

                    do
                    {
                        response = PostScore( chart, ScoreMap.Scores[songid].score[i], ScoreMap.Scores[songid].misscount[i], ScoreMap.Scores[songid].lamp[i]);
                    } while (response != HttpStatusCode.OK);
                }
                catch
                {
                    Utils.Log($"Server issues when adding {songid}[{i}]");
                }

            }
        }
        /// <summary>
        /// Try to find out songID of Kamaitachi song entries
        /// </summary>
        /// <param name="alterativeTitle">The alternative title that is void of special characters</param>
        /// <returns></returns>
        public static async Task<int?> Kamai_GetSongID(string alterativeTitle)
        {
            var search = alterativeTitle;
            var client = new HttpClient();
            var builder = new UriBuilder("https://api.kamaitachi.xyz/v1/games/iidx/songs/search");
            bool songFound = false, giveup = false;
            int included_sections = 0;
            int? songID = null;
            bool iterativeSearch = false;
            int iterations = 0;
            /* If nothing matches the alternative title, split on spaces and append one section at a time, since it's generally strange spacing that's the issue */
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
                else if (!iterativeSearch && json["description"].ToString().Contains("Found 0"))
                {
                    if (search != alterativeTitle)
                    {
                        giveup = true;
                    }
                    iterativeSearch = true;
                    search = alterativeTitle.Split(' ')[0];
                    included_sections = 1;
                }
                else
                {
                    try
                    {
                        search += " " + alterativeTitle.Split(' ')[included_sections];
                        included_sections++;
                    }
                    catch
                    {
                        giveup = true;
                    }
                }
                iterations++;
            } while (iterations < 10 && !songFound && !giveup);
            return songID;
        }
    }
}
