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
        static bool QueryOffsetAvailable(string version)
        {
            var builder = new UriBuilder(Config.UpdateServer + "/api/offsetforversion");
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["version"] = version;
            builder.Query = query.ToString();
            var response = client.GetAsync(builder.Uri, HttpCompletionOption.ResponseContentRead);
            if(response.Result.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return true;
            }
            return false;

        }
        static string GetNewOffsetVersion(string version)
        {
            var builder = new UriBuilder(Config.UpdateServer + "/api/getoffsetfile");
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["version"] = version;
            builder.Query = query.ToString();
            var response = client.GetAsync(builder.Uri, HttpCompletionOption.ResponseContentRead);
            var strtask = response.Result.Content.ReadAsStringAsync();
            strtask.Wait();
            return strtask.Result;
        }
        public static bool UpdateOffset(string version)
        {
            if (QueryOffsetAvailable(version))
            {
                var filecontent = GetNewOffsetVersion(version);
                if (!Directory.Exists("archive")) {
                    Directory.CreateDirectory("archive");
                }
                var v = File.ReadAllLines("offsets.txt")[0];
                File.Move("offsets.txt", $"archive/{v.Replace(':', '_')}.txt", true);
                File.WriteAllText("offsets.txt", filecontent);
                Offsets.LoadOffsets("offsets.txt");
                return true;
            } else
            {
                Console.WriteLine($"No offset file for version {version} available on server, enabling memory dump hotkey");
            }
            return false;
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
