using System;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Ini;

namespace infinitas_statfetcher
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
        public static HeaderValues HeaderConfig { get { return header; } }
        static HeaderValues header;
        static IConfigurationRoot root;

        public static void Parse(string configFile)
        {
            root = new ConfigurationBuilder().AddIniFile(configFile).Build();
            Server = ReadConfigString("remoterecord:serveraddress");
            API_key = ReadConfigString("remoterecord:apikey");

            UpdateFiles = ReadConfigBoolean("update:updatefiles");
            UpdateServer = ReadConfigString("update:updateserver");

            Save_remote = ReadConfigBoolean("record:saveremote");
            Save_local = ReadConfigBoolean("record:savelocal");

            header.songInfo = ReadConfigBoolean("localrecord:songinfo");
            header.chartDetails = ReadConfigBoolean("localrecord:chartdetails");
            header.resultDetails = ReadConfigBoolean("localrecord:resultdetails");
            header.judge = ReadConfigBoolean("localrecord:judge");
            header.settings = ReadConfigBoolean("localrecord:settings");

            Output_songlist = ReadConfigBoolean("debug:outputdb");
        }
        static string ReadConfigString(string key)
        {
            try { return root[key]; }
            catch
            {
                Console.WriteLine($"Couldn't read key {key}, setting empty string");
                return "";
            }
        }
        static bool ReadConfigBoolean(string key)
        {
            try { return bool.Parse(root[key]); }
            catch
            {
                Console.WriteLine($"Couldn't read key {key}, or unable to parse as boolean, setting false");
                return false;
            }
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
            sb.Append("\tplaytype\tgrade\tlamp");
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
