namespace infinitas_statfetcher
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

        public void Fetch(long position)
        {
            short word = 4;

            var style = PlayType.P1;
            var p1pgreat = Utils.ReadInt32(position, 0, word);
            var p1great = Utils.ReadInt32(position, word, word);
            var p1good = Utils.ReadInt32(position, word * 2, word);
            var p1bad = Utils.ReadInt32(position, word * 3, word);
            var p1poor = Utils.ReadInt32(position, word * 4, word);
            var p2pgreat = Utils.ReadInt32(position, word * 5, word);
            var p2great = Utils.ReadInt32(position, word * 6, word);
            var p2good = Utils.ReadInt32(position, word * 7, word);
            var p2bad = Utils.ReadInt32(position, word * 8, word);
            var p2poor = Utils.ReadInt32(position, word * 9, word);
            var p1cb = Utils.ReadInt32(position, word * 10, word);
            var p2cb = Utils.ReadInt32(position, word * 11, word);
            var p1fast = Utils.ReadInt32(position, word * 12, word);
            var p2fast = Utils.ReadInt32(position, word * 13, word);
            var p1slow = Utils.ReadInt32(position, word * 14, word);
            var p2slow = Utils.ReadInt32(position, word * 15, word);
            var measure_end = Utils.ReadInt32(position, word * 16, word);

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
