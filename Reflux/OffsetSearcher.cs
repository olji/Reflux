using System;
using System.Linq;

namespace Reflux
{
    public static class OffsetSearcher
    {
        // Movement between versions is generally on scale of tens of kilobytes, but 2MB shouldn't be too
        // taxing and gives a wide net to catch the pattern in memory in case it jumps massively
        public static int InitialSearchSpace = 2000000;
        public static int MaxSearchSpace = (int)3e8;
        public static OffsetsCollection SearchNewOffsets()
        {
            Console.WriteLine("Starting offset search mode");
            Console.WriteLine();
            OffsetsCollection newOffsets = new OffsetsCollection();
            newOffsets.SongList = FetchAndSearch(Offsets.SongList, "SongList", MergeByteRepresentations("5.1.1.".ToBytes()));
            newOffsets.UnlockData = FetchAndSearch(Offsets.UnlockData, "UnlockData", MergeByteRepresentations(1000.ToBytes(), 1.ToBytes(), 462.ToBytes()));
            newOffsets.DataMap = FetchAndSearch(Offsets.DataMap, "DataMap", MergeByteRepresentations(0x7FFF.ToBytes(), 0.ToBytes()), -3 * 8); // Back 3 steps in 8-byte address space

            Console.WriteLine("Play Sleepless Days SPA, either fully or exit after hitting 50-ish notes or more, then come back here");
            var (address, judgeInfo) = QueryJudgeInfo();
            newOffsets.JudgeData = address;
            newOffsets.PlayData = FetchAndSearch(Offsets.PlayData, "PlayData", MergeByteRepresentations(25094.ToBytes(), 3.ToBytes(), (judgeInfo.pgreat * 2 + judgeInfo.great).ToBytes()));
            var currentSongAddress = FetchAndSearch(Offsets.CurrentSong, "CurrentSong", MergeByteRepresentations(25094.ToBytes(), 3.ToBytes()));
            if (currentSongAddress == newOffsets.PlayData) // Ended up finding the same place for both, so CurrentSong is currently pointing at play data
            {
                currentSongAddress = FetchAndSearch(Offsets.CurrentSong, "CurrentSong_Phase2", MergeByteRepresentations(25094.ToBytes(), 3.ToBytes()), 0, currentSongAddress);
            }
            newOffsets.CurrentSong = currentSongAddress;

            // Get settings last, as those will depend on if you play P1 or P2, which we can get from after judge data is set
            Console.WriteLine("Set the following settings and then press enter: RANDOM EXHARD OFF SUDDEN+");
            Console.ReadLine();
            var settingsAddress = FetchAndSearch(Offsets.PlaySettings, "PlaySettings_1", MergeByteRepresentations(1.ToBytes(), 4.ToBytes(), 0.ToBytes(), 0.ToBytes(), 1.ToBytes()));
            Console.WriteLine("Now set the following settings and then press enter: MIRROR EASY AUTO-SCRATCH HIDDEN+");
            Console.ReadLine();
            var newResult = FetchAndSearch(Offsets.PlaySettings, "PlaySettings_2", MergeByteRepresentations(4.ToBytes(), 2.ToBytes(), 1.ToBytes(), 0.ToBytes(), 2.ToBytes()));
            if (newResult != settingsAddress)
            {
                throw new Exception("Found a different result on the second search");
            }
            newOffsets.PlaySettings = settingsAddress;
            return newOffsets;
        }
        static (long address, Judge judgeInfo) QueryJudgeInfo()
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
            long address = -1;
            try
            {

                address = FetchAndSearch(Offsets.JudgeData, "JudgeData_P1", MergeByteRepresentations(
                    j.pgreat.ToBytes(), j.great.ToBytes(), j.good.ToBytes(), j.bad.ToBytes(), j.poor.ToBytes(), // P1
                    0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), // P2
                    j.combobreak.ToBytes(), 0.ToBytes(), // Combobreaks
                    j.fast.ToBytes(), 0.ToBytes(), j.slow.ToBytes(), 0.ToBytes() // Fast/Slow
                    ));
            }
            catch
            {
                try
                {
                    address = FetchAndSearch(Offsets.JudgeData, "JudgeData_P2", MergeByteRepresentations(
                    0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), // P2
                    j.pgreat.ToBytes(), j.great.ToBytes(), j.good.ToBytes(), j.bad.ToBytes(), j.poor.ToBytes(), // P1
                    0.ToBytes(), j.combobreak.ToBytes(), // Combobreaks
                    0.ToBytes(), j.fast.ToBytes(), 0.ToBytes(), j.slow.ToBytes() // Fast/Slow
                    ));
                }
                catch
                {
                    throw new Exception("Unable to find judge data");
                }
            }
            return (address: address, judgeInfo: j);
        }
        static byte[] MergeByteRepresentations(params byte[][] data)
        {
            return data.SelectMany(x => x).ToArray();
        }
        static (long[] address, byte[] data) FetchDataSet(long address, int distanceFromCenter)
        {
            byte[] data = new byte[distanceFromCenter * 2];
            long[] addresses = new long[distanceFromCenter * 2];
            int datalen = distanceFromCenter * 2;
            addresses = addresses.Select((value, index) => address - distanceFromCenter + (1 * index)).ToArray(); // Since Enumerable.Range only accept Int32

            return (address: addresses, data: Utils.ReadRaw(address - distanceFromCenter, datalen));
        }
        static long FetchAndSearch(long offset, string offsetDescription, byte[] pattern, int offsetFromMatch = 0, long ignoreAddress = -1)
        {
            var searchSize = InitialSearchSpace;
            do
            {
                var buffer = FetchDataSet(offset, searchSize);
                var searchResult = Search(buffer, pattern, ignoreAddress);
                if (searchResult != -1)
                {
                    searchResult += offsetFromMatch; // Back 3 steps in 8-byte address space
                    return buffer.address[searchResult];
                }
                Console.WriteLine($"Unable to find {offsetDescription}, search range {searchSize / 1e6}MB -> {(searchSize * 2) / 1e6}");
                searchSize *= 2;
            } while (searchSize < MaxSearchSpace);
            throw new Exception($"Search space has exceeded maximum of {MaxSearchSpace / 1e6}MB");
        }
        /// <summary>
        /// Searches dataset for pattern, returns offset
        /// </summary>
        /// <param name="data"></param>
        /// <param name="pattern"></param>
        /// <returns>Returns offset, or -1 if no match</returns>
        /// <exception cref="ArgumentException"></exception>
        static int Search((long[] address, byte[] data) buffer, byte[] pattern, long ignoreAddress = -1)
        {
            if (buffer.data.Length < pattern.Length)
            {
                throw new ArgumentException("Pattern cannot be shorter than search space");
            }
            for (int i = 0; i < buffer.data.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer.data[i + j] != pattern[j]) { match = false; break; }
                    if (buffer.data[i + j] == pattern[j]) { match = true; }
                }
                if (buffer.address[i] != ignoreAddress && match) { return i; }
            }
            return -1;
        }
    }
}
