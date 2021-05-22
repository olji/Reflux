namespace Reflux
{
    class Judge
    {
        public PlayType playtype;
        public int pgreat;
        public int great;
        public int good;
        public int bad;
        public int poor;
        public int fast;
        public int slow;
        public int combobreak;
        public bool prematureEnd;

        public bool PFC { get { return (good + bad + poor) == 0; } }
        /// <summary>
        /// Fetch all the judge information from the play
        /// </summary>
        public void Fetch()
        {
            short word = 4;

            var style = PlayType.P1;
            var p1pgreat = Utils.ReadInt32(Offsets.JudgeData, 0);
            var p1great = Utils.ReadInt32(Offsets.JudgeData, word);
            var p1good = Utils.ReadInt32(Offsets.JudgeData, word * 2);
            var p1bad = Utils.ReadInt32(Offsets.JudgeData, word * 3);
            var p1poor = Utils.ReadInt32(Offsets.JudgeData, word * 4);
            var p2pgreat = Utils.ReadInt32(Offsets.JudgeData, word * 5);
            var p2great = Utils.ReadInt32(Offsets.JudgeData, word * 6);
            var p2good = Utils.ReadInt32(Offsets.JudgeData, word * 7);
            var p2bad = Utils.ReadInt32(Offsets.JudgeData, word * 8);
            var p2poor = Utils.ReadInt32(Offsets.JudgeData, word * 9);
            var p1cb = Utils.ReadInt32(Offsets.JudgeData, word * 10);
            var p2cb = Utils.ReadInt32(Offsets.JudgeData, word * 11);
            var p1fast = Utils.ReadInt32(Offsets.JudgeData, word * 12);
            var p2fast = Utils.ReadInt32(Offsets.JudgeData, word * 13);
            var p1slow = Utils.ReadInt32(Offsets.JudgeData, word * 14);
            var p2slow = Utils.ReadInt32(Offsets.JudgeData, word * 15);
            var measure_end = Utils.ReadInt32(Offsets.JudgeData, word * 16);


            if (p1pgreat + p1great + p1good + p1bad + p1poor == 0)
            {
                style = PlayType.P2;
            }
            else if (p2pgreat + p2great + p2good + p2bad + p2poor > 0)
            {
                style = PlayType.DP;
            }

            playtype = style;
            pgreat = p1pgreat + p2pgreat;
            great = p1great + p2great;
            good = p1good + p2good;
            bad = p1bad + p2bad;
            poor = p1poor + p2poor;
            fast = p1fast + p2fast;
            slow = p1slow + p2slow;
            combobreak = p1cb + p2cb;
            prematureEnd = measure_end != 0;
        }
    }
}
