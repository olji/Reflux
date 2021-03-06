using System;
using System.Collections.Generic;
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
        public static async void ReportUnlocks(Dictionary<string,SongInfo> songdb, Dictionary<string, Utils.UnlockData> unlocks)
        {
            foreach(var keyval in unlocks)
            {
                var content = new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    { "songid", keyval.Key },
                    { "title2", songdb[keyval.Key].title_english },
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

    }
}
