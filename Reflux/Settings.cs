namespace infinitas_statfetcher
{
    class Settings
    {
        public string style;
        public string style2; /* Style for 2p side in DP */
        public string gauge;
        public string assist;
        public string range;
        public bool flip;
        public bool battle;
        public bool Hran;

        /// <summary>
        /// Fetch settings
        /// </summary>
        /// <param name="playstyle"></param>
        public void Fetch(PlayType playstyle)
        {
            int word = 4;

            int styleVal = 0;
            int gaugeVal = 0;
            int assistVal = 0;
            int rangeVal = 0;
            int style2Val = 0;
            if (playstyle == PlayType.P1 || playstyle == PlayType.DP)
            {
                styleVal = Utils.ReadInt32(Offsets.PlaySettings, 0);
                gaugeVal = Utils.ReadInt32(Offsets.PlaySettings, word);
                assistVal = Utils.ReadInt32(Offsets.PlaySettings, word * 2);
                rangeVal = Utils.ReadInt32(Offsets.PlaySettings, word * 4);
                if (playstyle == PlayType.DP) /* Style for DP P2 side has its own value and isn't shared with P2 settings */
                {
                    style2Val = Utils.ReadInt32(Offsets.PlaySettings, word * 5);
                }
            }
            else if (playstyle == PlayType.P2) /* P2 settings are separate from P1 */
            {
                styleVal = Utils.ReadInt32(Offsets.PlaySettings, word * 12);
                gaugeVal = Utils.ReadInt32(Offsets.PlaySettings, word * 13);
                assistVal = Utils.ReadInt32(Offsets.PlaySettings, word * 14);
                rangeVal = Utils.ReadInt32(Offsets.PlaySettings, word * 16);
            }
            int flipVal = Utils.ReadInt32(Offsets.PlaySettings, word * 3);
            int battleVal = Utils.ReadInt32(Offsets.PlaySettings, word * 6);
            int HranVal = Utils.ReadInt32(Offsets.PlaySettings, word * 7);

            switch (styleVal)
            {
                case 0: style = "OFF"; break;
                case 1: style = "RANDOM"; break;
                case 2: style = "R-RANDOM"; break;
                case 3: style = "S-RANDOM"; break;
                case 4: style = "MIRROR"; break;
                case 5: style = "SYNCHRONIZE RANDOM"; break;
                case 6: style = "SYMMETRY RANDOM"; break;
            }
            switch (style2Val)
            {
                case 0: style2 = "OFF"; break;
                case 1: style2 = "RANDOM"; break;
                case 2: style2 = "R-RANDOM"; break;
                case 3: style2 = "S-RANDOM"; break;
                case 4: style2 = "MIRROR"; break;
                case 5: style2 = "SYNCHRONIZE RANDOM"; break;
                case 6: style2 = "SYMMETRY RANDOM"; break;
            }

            switch (gaugeVal)
            {
                case 0: gauge = "OFF"; break;
                case 1: gauge = "ASSIST EASY"; break;
                case 2: gauge = "EASY"; break;
                case 3: gauge = "HARD"; break;
                case 4: gauge = "EX HARD"; break;
            }

            switch (assistVal)
            {
                case 0: assist = "OFF"; break;
                case 1: assist = "AUTO SCRATCH"; break;
                case 2: assist = "5KEYS"; break;
                case 3: assist = "LEGACY NOTE"; break;
                case 4: assist = "KEY ASSIST"; break;
                case 5: assist = "ANY KEY"; break;
            }

            switch (rangeVal)
            {
                case 0: range = "OFF"; break;
                case 1: range = "SUDDEN+"; break;
                case 2: range = "HIDDEN+"; break;
                case 3: range = "SUD+ & HID+"; break;
                case 4: range = "LIFT"; break;
                case 5: range = "LIFT & SUD+"; break;
            }
            flip = flipVal == 1;
            battle = battleVal == 1;
            Hran = HranVal == 1;
        }
    }
}
