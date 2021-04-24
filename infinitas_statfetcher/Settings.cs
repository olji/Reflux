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

        public void Fetch(long position, PlayType playstyle)
        {
            int word = 4;

            int styleVal = 0;
            int gaugeVal = 0;
            int assistVal = 0;
            int rangeVal = 0;
            int style2Val = 0;
            if (playstyle == PlayType.P1 || playstyle == PlayType.DP)
            {
                styleVal = Utils.ReadInt32(position, 0, word);
                gaugeVal = Utils.ReadInt32(position, word, word);
                assistVal = Utils.ReadInt32(position, word * 2, word);
                rangeVal = Utils.ReadInt32(position, word * 4, word);
                if (playstyle == PlayType.DP)
                {
                    style2Val = Utils.ReadInt32(position, word * 5, word);
                }
            }
            else if (playstyle == PlayType.P2)
            {
                styleVal = Utils.ReadInt32(position, word * 12, word);
                gaugeVal = Utils.ReadInt32(position, word * 13, word);
                assistVal = Utils.ReadInt32(position, word * 14, word);
                rangeVal = Utils.ReadInt32(position, word * 16, word);
            }
            int flipVal = Utils.ReadInt32(position, word * 3, word);
            int battleVal = Utils.ReadInt32(position, word * 6, word);
            int HranVal = Utils.ReadInt32(position, word * 7, word);

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
