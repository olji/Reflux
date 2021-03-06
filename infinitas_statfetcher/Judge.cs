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
        public int notesJudged;
        public bool prematureEnd;

        public void Fetch(long judgedata, long noteprogress)
        {
            short word = 4;

            var style = PlayType.P1;
            var p1pgreat = Utils.ReadInt32(judgedata, 0, word);
            var p1great = Utils.ReadInt32(judgedata, word, word);
            var p1good = Utils.ReadInt32(judgedata, word * 2, word);
            var p1bad = Utils.ReadInt32(judgedata, word * 3, word);
            var p1poor = Utils.ReadInt32(judgedata, word * 4, word);
            var p2pgreat = Utils.ReadInt32(judgedata, word * 5, word);
            var p2great = Utils.ReadInt32(judgedata, word * 6, word);
            var p2good = Utils.ReadInt32(judgedata, word * 7, word);
            var p2bad = Utils.ReadInt32(judgedata, word * 8, word);
            var p2poor = Utils.ReadInt32(judgedata, word * 9, word);
            var p1cb = Utils.ReadInt32(judgedata, word * 10, word);
            var p2cb = Utils.ReadInt32(judgedata, word * 11, word);
            var p1fast = Utils.ReadInt32(judgedata, word * 12, word);
            var p2fast = Utils.ReadInt32(judgedata, word * 13, word);
            var p1slow = Utils.ReadInt32(judgedata, word * 14, word);
            var p2slow = Utils.ReadInt32(judgedata, word * 15, word);

            var notes_judged = Utils.ReadInt32(noteprogress, 0, word);
            var notes_total = Utils.ReadInt32(noteprogress, word, word);

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
            notesJudged = notes_judged;
            prematureEnd = notes_judged < notes_total;
        }
    }
}
