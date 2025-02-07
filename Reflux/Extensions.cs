using System;
using System.Linq;
using System.Text;

namespace Reflux
{
    public static class Extensions
    {
        public static byte[] ToBytes(this string str)
        {
            return Encoding.ASCII.GetBytes(str);
            //return str.SelectMany(c=>BitConverter.GetBytes(c)).ToArray();
        }
        public static byte[] ToBytes(this int i)
        {
            return BitConverter.GetBytes(i);
        }
        public static byte[] ToBytes(this int[] ints)
        {
            return ints.SelectMany(i => BitConverter.GetBytes(i)).ToArray();
        }
        public static byte[] ToBytes(this long i)
        {
            return BitConverter.GetBytes(i);
        }
        public static byte[] ToBytes(this long[] longs)
        {
            return longs.SelectMany(i => BitConverter.GetBytes(i)).ToArray();
        }
    }
}
