using System;
using System.Collections.Generic;
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
            Console.WriteLine("Starting offset search mode, press ENTER to continue");
            Console.ReadLine();
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
            if (judgeInfo.playtype == PlayType.P2)
            {
                settingsAddress -= Settings.P2_offset;
            }
            newOffsets.PlaySettings = settingsAddress;
            return newOffsets;
        }
        static (long address, Judge judgeInfo) QueryJudgeInfo()
        {
            var j = new Judge();
            j.pgreat = QueryNumber("Enter pgreat count: ");
            j.great = QueryNumber("Enter great count: ");
            j.good = QueryNumber("Enter good count: ");
            j.bad = QueryNumber("Enter bad count: ");
            j.poor = QueryNumber("Enter poor count: ");
            j.combobreak = QueryNumber("Enter combobreak count: ");
            j.fast = QueryNumber("Enter fast count: ");
            j.slow = QueryNumber("Enter slow count: ");

            j.playtype = PlayType.P1;
            var result = FetchAndSearchAlternating(Offsets.JudgeData, new[] { "JudgeData_P1", "JudgeData_P2" },
                new[] {
                    // P1
                    MergeByteRepresentations(
                    j.pgreat.ToBytes(), j.great.ToBytes(), j.good.ToBytes(), j.bad.ToBytes(), j.poor.ToBytes(), // P1
                    0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), // P2
                    j.combobreak.ToBytes(), 0.ToBytes(), // Combobreaks
                    j.fast.ToBytes(), 0.ToBytes(), j.slow.ToBytes(), 0.ToBytes()), // Fast/Slow
                    // P2
                    MergeByteRepresentations(
                    0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), 0.ToBytes(), // P2
                    j.pgreat.ToBytes(), j.great.ToBytes(), j.good.ToBytes(), j.bad.ToBytes(), j.poor.ToBytes(), // P1
                    0.ToBytes(), j.combobreak.ToBytes(), // Combobreaks
                    0.ToBytes(), j.fast.ToBytes(), 0.ToBytes(), j.slow.ToBytes() // Fast/Slow
                    ) });
            long address = result.address;
            if (result.matchingIndex == 1)
            {
                j.playtype = PlayType.P2;
            }

            return (address: result.address, judgeInfo: j);
        }
        static int QueryNumber(string query)
        {
            bool parseSuccess = false;
            Console.WriteLine(query);
            int result = int.MaxValue;
            do
            {
                try
                {
                    result = int.Parse(Console.ReadLine());
                    parseSuccess = true;
                }
                catch { Console.WriteLine("Doesn't seem like a number, try again: "); }
            } while (!parseSuccess);
            return result;
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
        static (long address, int matchingIndex) FetchAndSearchAlternating(long offset, IEnumerable<string> offsetDescriptions, IEnumerable<byte[]> patterns, int offsetFromMatch = 0, long ignoreAddress = -1)
        {
            var searchSize = InitialSearchSpace;
            do
            {
                var buffer = FetchDataSet(offset, searchSize);
                int searchResult = -1;
                for (int i = 0; i < patterns.Count(); i++)
                {
                    var pattern = patterns.ElementAt(i);
                    var description = offsetDescriptions.ElementAt(i);
                    searchResult = Search(buffer, pattern, ignoreAddress);
                    if (searchResult != -1)
                    {
                        searchResult += offsetFromMatch; // Back 3 steps in 8-byte address space
                        return (buffer.address[searchResult], i);
                    }
                    Console.WriteLine($"Unable to find {description}");
                }
                Console.WriteLine($"Unable to find any match, search range {searchSize / 1e6}MB -> {(searchSize * 2) / 1e6}");
                searchSize *= 2;
            } while (searchSize < MaxSearchSpace);
            throw new Exception($"Search space has exceeded maximum of {MaxSearchSpace / 1e6}MB");
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
