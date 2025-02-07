using System;
using System.Linq;

namespace Reflux
{
    public class OffsetSearcher
    {
        // Movement between versions is generally on scale of tens of kilobytes, but 2MB shouldn't be too
        // taxing and gives a wide net to catch the pattern in memory in case it jumps massively
        public static int SearchSpace = 2000000;
        public OffsetSearcher()
        {

        }
        public OffsetsCollection SearchNewOffsets()
        {
            OffsetsCollection newOffsets = new OffsetsCollection();
            var buffer = FetchDataSet(Offsets.SongList, SearchSpace); // 2MB 
            newOffsets.SongList = buffer.address[Search(buffer.data, MergeByteRepresentations("5.1.1.".ToBytes()))];
            buffer = FetchDataSet(Offsets.UnlockData, SearchSpace); // 2MB 
            newOffsets.UnlockData = buffer.address[Search(buffer.data, MergeByteRepresentations(1000.ToBytes(), 1.ToBytes(), 462.ToBytes()))];
            buffer = FetchDataSet(Offsets.DataMap, SearchSpace); // 2MB 
            var ix = Search(buffer.data, MergeByteRepresentations(0x3FFF.ToBytes()));
            //ix -= 3*8; // Back 3 steps in 8-byte address space
            newOffsets.DataMap = buffer.address[ix] - 3 * 8; // Back 3 steps in 8-byte address space

            Console.WriteLine("Play Sleepless Days SPA, either fully or exit after hitting 50-ish notes or more, then come back here");
            var (address, judgeInfo) = QueryJudgeInfo();
            newOffsets.JudgeData = address;
            buffer = FetchDataSet(Offsets.PlayData, SearchSpace); // 2MB 
            newOffsets.PlayData = buffer.address[Search(buffer.data, MergeByteRepresentations(25094.ToBytes(), 3.ToBytes(), (judgeInfo.pgreat * 2 + judgeInfo.great).ToBytes()))];
            buffer = FetchDataSet(Offsets.CurrentSong, SearchSpace); // 2MB 
            var currentSongIndex = Search(buffer.data, MergeByteRepresentations(25094.ToBytes(), 3.ToBytes()));
            if (buffer.address[currentSongIndex] == newOffsets.PlayData)
            {
                currentSongIndex = Search(buffer.data, MergeByteRepresentations(25094.ToBytes(), 3.ToBytes(), (judgeInfo.pgreat * 2 + judgeInfo.great).ToBytes()), currentSongIndex);
            }
            newOffsets.CurrentSong = buffer.address[currentSongIndex];

            // Get settings last, as those will depend on if you play P1 or P2, which we can get from after judge data is set
            Console.WriteLine("Set the following settings for a moment: RANDOM EXHARD OFF SUDDEN+");
            long settingsAddress = -1;
            do
            {
                buffer = FetchDataSet(Offsets.PlaySettings, SearchSpace); // 2MB 
                var index = Search(buffer.data, MergeByteRepresentations(1.ToBytes(), 4.ToBytes(), 0.ToBytes(), 0.ToBytes(), 1.ToBytes()));
                if (index != -1)
                {
                    Console.WriteLine("Now set the following settings for a moment: MIRROR EASY AUTO-SCRATCH HIDDEN+");
                    do
                    {
                        buffer = FetchDataSet(Offsets.PlaySettings, SearchSpace); // 2MB 
                        var newindex = Search(buffer.data, MergeByteRepresentations(4.ToBytes(), 2.ToBytes(), 1.ToBytes(), 0.ToBytes(), 2.ToBytes()));
                        if (newindex != -1 && newindex == index)
                        {
                            settingsAddress = buffer.address[index];
                            break;
                        }
                    } while (true);
                }

            } while (settingsAddress == -1);
            newOffsets.PlaySettings = settingsAddress;
            return newOffsets;
        }
        (long address, Judge judgeInfo) QueryJudgeInfo()
        {
            var j = new Judge();
            Console.WriteLine("Enter pgreat count: ");
            bool parseSuccess = false;
            do
            {
                try
                {
                    j.pgreat = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);
            parseSuccess = false;
            Console.WriteLine("Enter greats count: ");
            do
            {
                try
                {
                    j.great = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);
            parseSuccess = false;
            Console.WriteLine("Enter good count: ");
            do
            {
                try
                {
                    j.good = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);
            parseSuccess = false;
            Console.WriteLine("Enter bad count: ");
            do
            {
                try
                {
                    j.bad = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);
            parseSuccess = false;
            Console.WriteLine("Enter poor count: ");
            do
            {
                try
                {
                    j.poor = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);
            parseSuccess = false;
            Console.WriteLine("Enter combobreak count: ");
            do
            {
                try
                {
                    j.combobreak = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);
            parseSuccess = false;
            Console.WriteLine("Enter fast count: ");
            do
            {
                try
                {
                    j.fast = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);
            parseSuccess = false;
            Console.WriteLine("Enter slow count: ");
            do
            {
                try
                {
                    j.slow = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);

            j.playtype = PlayType.P1;
            var (address, data) = FetchDataSet(Offsets.JudgeData, SearchSpace); // 2MB 
            var index = Search(data, MergeByteRepresentations(
                j.pgreat.ToBytes(), j.great.ToBytes(), j.good.ToBytes(), j.bad.ToBytes(), j.poor.ToBytes(), // P1
                0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), // P2
                j.combobreak.ToBytes(), 0.ToBytes(), // Combobreaks
                j.fast.ToBytes(), 0.ToBytes(), j.slow.ToBytes(), 0.ToBytes() // Fast/Slow
                ));

            if (index == -1) // try P2
            {
                j.playtype = PlayType.P2;
                index = Search(data, MergeByteRepresentations(
                0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), // P2
                j.pgreat.ToBytes(), j.great.ToBytes(), j.good.ToBytes(), j.bad.ToBytes(), j.poor.ToBytes(), // P1
                0.ToBytes(), j.combobreak.ToBytes(), // Combobreaks
                0.ToBytes(), j.fast.ToBytes(), 0.ToBytes(), j.slow.ToBytes() // Fast/Slow
                ));
                if (index == -1) { Console.WriteLine("Unable to find judge data"); return (address: -1, judgeInfo: new Judge()); }
            }
            return (address: address[index], judgeInfo: j);
        }
        byte[] MergeByteRepresentations(params byte[][] data)
        {
            return data.SelectMany(x => x).ToArray();
        }
        (long[] address, byte[] data) FetchDataSet(long address, int distanceFromCenter)
        {
            byte[] data = new byte[distanceFromCenter * 2];
            long[] addresses = new long[distanceFromCenter * 2];
            int datalen = distanceFromCenter * 2;
            addresses = addresses.Select((value, index) => address - distanceFromCenter + (1 * index)).ToArray(); // Since Enumerable.Range only accept Int32

            return (address: addresses, data: Utils.ReadRaw(address - distanceFromCenter, datalen));
        }
        /// <summary>
        /// Searches dataset for pattern, returns offset
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pattern"></param>
        /// <returns>Returns offset, or -1 if no match</returns>
        /// <exception cref="ArgumentException"></exception>
        int Search(byte[] data, byte[] pattern, int ignoreIndex = -1)
        {
            if (data.Length < pattern.Length)
            {
                throw new ArgumentException("Pattern cannot be shorter than search space");
            }
            for (int i = 0; i < data.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j]) { match = false; break; }
                    if (data[i + j] == pattern[j]) { match = true; }
                }
                if (i != ignoreIndex && match) { return i; }
            }
            return -1;
        }
    }
}
