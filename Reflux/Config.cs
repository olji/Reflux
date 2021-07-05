using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Ini;

namespace Reflux
{
    static class Config
    {
        public static string Server { get; private set; }
        public static string UpdateServer { get; private set; }
        public static bool UpdateFiles { get; private set; }
        public static string API_key { get; private set; }
        public static bool Output_songlist { get; private set; }
        public static bool Save_remote { get; private set; }
        public static bool Save_local { get; private set; }
        public static bool Save_json { get; private set; }
        public static bool Save_latestJson { get; private set; }
        public static bool Save_latestTxt { get; private set; }
        public static bool Stream_Playstate { get; private set; }
        public static bool Stream_Marquee { get; private set; }
        public static bool Stream_FullSongInfo { get; private set; }
        public static string MarqueeIdleText { get; private set; }
        public static HeaderValues HeaderConfig { get { return header; } }
        static HeaderValues header;
        static IDictionary<string, string> config;

        public static void Parse(string configFile)
        {
            using (var stream = new System.IO.FileStream(configFile, System.IO.FileMode.OpenOrCreate))
            {
                config = IniStreamConfigurationProvider.Read(stream);
            }

            Utils.Debug("Loading config...");

            Server = ReadConfigString("remoterecord:serveraddress");
            API_key = ReadConfigString("remoterecord:apikey");

            UpdateFiles = ReadConfigBoolean("update:updatefiles");
            UpdateServer = ReadConfigString("update:updateserver");

            Save_remote = ReadConfigBoolean("record:saveremote");
            Save_local = ReadConfigBoolean("record:savelocal");
            Save_json = ReadConfigBoolean("record:savejson");
            Save_latestJson = ReadConfigBoolean("record:savelatestjson");
            Save_latestTxt = ReadConfigBoolean("record:savelatesttxt");

            header.songInfo = ReadConfigBoolean("localrecord:songinfo");
            header.chartDetails = ReadConfigBoolean("localrecord:chartdetails");
            header.resultDetails = ReadConfigBoolean("localrecord:resultdetails");
            header.judge = ReadConfigBoolean("localrecord:judge");
            header.settings = ReadConfigBoolean("localrecord:settings");

            Stream_Playstate = ReadConfigBoolean("livestream:showplaystate");
            Stream_Marquee = ReadConfigBoolean("livestream:enablemarquee");
            Stream_FullSongInfo = ReadConfigBoolean("livestream:enablefullsonginfo");
            MarqueeIdleText = ReadConfigString("livestream:marqueeidletext");

            Output_songlist = ReadConfigBoolean("debug:outputdb");
            Utils.Debug("Done\n");

        }
        static string ReadConfigString(string key)
        {
            if (config.ContainsKey(key))
            {
                Utils.Debug($"{key}: {config[key]}");
                return config[key];
            }
            Console.WriteLine($"Couldn't read key {key}, setting empty string");
            return "";
        }
        static bool ReadConfigBoolean(string key)
        {
            if (config.ContainsKey(key))
            {
                bool value;
                if (bool.TryParse(config[key], out value))
                {
                    Utils.Debug($"{key}: {value}");
                    return value;
                }
            }
            Console.WriteLine($"Couldn't read key {key}, or unable to parse as boolean, setting false");
            return false;
        }
        public static string GetTsvHeader()
        {
            StringBuilder sb = new StringBuilder("title\tdifficulty");
            if (HeaderConfig.songInfo)
            {
                sb.Append("\ttitle2\tbpm\tartist\tgenre");
            }
            if (HeaderConfig.chartDetails)
            {
                sb.Append("\tnotecount\tlevel");
            }
            sb.Append("\tplaytype\tgrade\tlamp\tmisscount");
            if (HeaderConfig.resultDetails)
            {
                sb.Append("\tgaugepercent\texscore");
            }
            if (HeaderConfig.judge)
            {
                sb.Append("\tpgreat\tgreat\tgood\tbad\tpoor\tcombobreak\tfast\tslow");
            }
            if (HeaderConfig.settings)
            {
                sb.Append("\tstyle\tstyle2\tgauge\tassist\trange");
            }
            sb.Append("\tdate");

            return sb.ToString();
        }
    }
    struct HeaderValues
    {
        /* Title, difficulty, grade and lamp will always be included */
        public bool songInfo; /* Include artist, bpm, genre */
        public bool chartDetails; /* Include level, notecount */
        public bool resultDetails; /* Include gauge percent, ex score */
        public bool judge; /* Include all judge data (Pgreat, combobreak, fast, slow etc.) */
        public bool settings; /* Include style, gauge, assist, flip, range etc. */
    }
}
