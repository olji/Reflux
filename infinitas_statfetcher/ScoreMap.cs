using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace infinitas_statfetcher
{
    class ScoreMap
    {
        /// <summary>
        /// Representation of a node in INFINITAS score hashmap
        /// </summary>
        struct ListNode
        {
            public long next;
            public long prev;
            public int diff;
            public int song;
            public int playtype;
            public int uk2;
            public int score;
            public int misscount;
            public int uk3;
            public int uk4;
            public int lamp;
        }

        public struct ScoreData
        {
            public string songID;
            public Lamp[] lamp;
            public int[] score;
            public int[] misscount;

        }
        static Dictionary<string, ListNode> nodes = new Dictionary<string, ListNode>();
        public static Dictionary<string, ScoreData> Scores { get; private set; }
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
            Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);
        /// <summary>
        /// Parser of the hashmap for player top scores
        /// </summary>
        public static void LoadMap()
        {
            long datamap = Offsets.DataMap;
            long nullobj = Utils.ReadInt64(datamap, -16);
            long startaddress = Utils.ReadInt64(datamap, 0);
            long endaddress = Utils.ReadInt64(datamap, 8);
            byte[] buffer = new byte[endaddress - startaddress];

            int nRead = 0;
            ReadProcessMemory((int)Utils.handle, startaddress, buffer, buffer.Length, ref nRead);
            List<long> llist_startpoints = new List<long>();
            for(int i = 0; i < buffer.Length / 8; i++)
            {
                long addr = Utils.BytesToInt64(buffer, i * 8);
                if(addr != 0x494fdce0) {
                    llist_startpoints.Add(addr);
                }
            }

            buffer = new byte[64];
            foreach (var entrypoint in llist_startpoints)
            {
                ReadProcessMemory((int)Utils.handle, entrypoint, buffer, buffer.Length, ref nRead);
                ListNode entry = new ListNode() {
                    next = Utils.BytesToInt64(buffer, 0, 8), 
                    prev = Utils.BytesToInt64(buffer, 8, 8), 
                    diff = Utils.BytesToInt32(buffer, 16, 4), 
                    song = Utils.BytesToInt32(buffer, 20, 4), 
                    playtype = Utils.BytesToInt32(buffer, 24, 4), 
                    uk2 = Utils.BytesToInt32(buffer, 28, 4), 
                    score = Utils.BytesToInt32(buffer, 32, 4), 
                    misscount = Utils.BytesToInt32(buffer, 36, 4), 
                    uk3 = Utils.BytesToInt32(buffer, 40, 4), 
                    uk4 = Utils.BytesToInt32(buffer, 44, 4), 
                    lamp = Utils.BytesToInt32(buffer, 48, 4)
                };
                FollowLinkedList(entry);

            }

            Scores = new Dictionary<string, ScoreData>();
            /* Parse into more workable format */
            foreach(var node in nodes)
            {
                var sections = node.Key.Split('_');
                if (!Scores.ContainsKey(sections[0]))
                {
                    var scoredata = new ScoreData() { 
                        songID = sections[0], 
                        score = new int[10],
                        misscount = new int[10],
                        lamp = new Lamp[10]
                    };
                    Scores.Add(sections[0], scoredata);

                }
                var difficulty = int.Parse(sections[1]) + (int.Parse(sections[2]) * 5);
                var songScores = Scores[sections[0]];
                songScores.lamp[difficulty] = (Lamp)node.Value.lamp;
                songScores.score[difficulty] = node.Value.score;
                songScores.misscount[difficulty] = node.Value.misscount;
            }
        }
        static void FollowLinkedList(ListNode entrypoint)
        {
            var buffer = new byte[64];
            int nRead = 0;

            ReadProcessMemory((int)Utils.handle, entrypoint.next, buffer, buffer.Length, ref nRead);
            var traveller = new ListNode() { 
                next = Utils.BytesToInt64(buffer, 0, 8), 
                prev = Utils.BytesToInt64(buffer, 8, 8), 
                diff = Utils.BytesToInt32(buffer, 16, 4), 
                song = Utils.BytesToInt32(buffer, 20, 4), 
                playtype = Utils.BytesToInt32(buffer, 24, 4), 
                uk2 = Utils.BytesToInt32(buffer, 28, 4), 
                score = Utils.BytesToInt32(buffer, 32, 4), 
                misscount = Utils.BytesToInt32(buffer, 36, 4), 
                uk3 = Utils.BytesToInt32(buffer, 40, 4), 
                uk4 = Utils.BytesToInt32(buffer, 44, 4), 
                lamp = Utils.BytesToInt32(buffer, 48, 4)
            };

            while (traveller.song > 999)
            {
                var addr = traveller.next;

                string key = $"{traveller.song.ToString("D5")}_{traveller.diff}_{traveller.playtype}";
                if (nodes.ContainsKey(key))
                {
                    break;
                }
                else
                {
                    nodes.Add(key, traveller);
                }
                ReadProcessMemory((int)Utils.handle, addr, buffer, buffer.Length, ref nRead);
                traveller = new ListNode()
                {
                    next = Utils.BytesToInt64(buffer, 0, 8),
                    prev = Utils.BytesToInt64(buffer, 8, 8),
                    diff = Utils.BytesToInt32(buffer, 16, 4),
                    song = Utils.BytesToInt32(buffer, 20, 4),
                    playtype = Utils.BytesToInt32(buffer, 24, 4),
                    uk2 = Utils.BytesToInt32(buffer, 28, 4),
                    score = Utils.BytesToInt32(buffer, 32, 4),
                    misscount = Utils.BytesToInt32(buffer, 36, 4),
                    uk3 = Utils.BytesToInt32(buffer, 40, 4),
                    uk4 = Utils.BytesToInt32(buffer, 44, 4),
                    lamp = Utils.BytesToInt32(buffer, 48, 4)
                };
            }
        }
        //enum direction { forward, backward };
    }
}
