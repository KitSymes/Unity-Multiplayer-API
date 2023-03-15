using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;

namespace KitSymes.GTRP.Internal
{
    public class ByteConverter
    {
        public static byte[] GetBytes(string str)
        {
            List<byte> bytes = new List<byte>();

            bytes.AddRange(BitConverter.GetBytes(str.Length));
            foreach (char c in str)
                bytes.AddRange(BitConverter.GetBytes(c));

            return bytes.ToArray();
        }
        public static byte[] GetBytes(Vector3 vector)
        {
            List<byte> bytes = new List<byte>();

            bytes.AddRange(BitConverter.GetBytes(vector.x));
            bytes.AddRange(BitConverter.GetBytes(vector.y));
            bytes.AddRange(BitConverter.GetBytes(vector.z));

            return bytes.ToArray();
        }
        public static byte[] GetBytes(Quaternion quaternion)
        {
            List<byte> bytes = new List<byte>();

            bytes.AddRange(BitConverter.GetBytes(quaternion.x));
            bytes.AddRange(BitConverter.GetBytes(quaternion.y));
            bytes.AddRange(BitConverter.GetBytes(quaternion.z));
            bytes.AddRange(BitConverter.GetBytes(quaternion.w));

            return bytes.ToArray();
        }
        public static byte[] GetBytes(DateTime timestamp)
        {
            return BitConverter.GetBytes(timestamp.ToBinary());
        }
        public static byte[] GetBytes(IPEndPoint endPoint)
        {
            List<byte> bytes = new List<byte>();

            bytes.AddRange(GetBytes(endPoint.Address.ToString()));
            bytes.AddRange(BitConverter.GetBytes(endPoint.Port));

            return bytes.ToArray();
        }

        public static string ToString(byte[] bytes, int pointer = 0)
        {
            if (bytes.Length < pointer + 4)
                throw new IndexOutOfRangeException($"Cannot convert bytes to String: Should be at least 4 bytes from {pointer}, [{bytes.Length} total]");

            int length = BitConverter.ToInt32(bytes, pointer);
            StringBuilder str = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                char c = BitConverter.ToChar(bytes, pointer + 4 + i * 2);
                str.Append(c);
            }

            return str.ToString();
        }
        public static Vector3 ToVector3(byte[] bytes, int pointer = 0)
        {
            if (bytes.Length < pointer + 12)
                throw new IndexOutOfRangeException($"Cannot convert bytes to Vector3: Should be at least 12 bytes from {pointer}, [{bytes.Length} total]");

            return new Vector3(
                BitConverter.ToSingle(bytes, pointer + 0),
                BitConverter.ToSingle(bytes, pointer + 4),
                BitConverter.ToSingle(bytes, pointer + 8)
                );
        }
        public static Quaternion ToQuaternion(byte[] bytes, int pointer = 0)
        {
            if (bytes.Length < pointer + 16)
                throw new IndexOutOfRangeException($"Cannot convert bytes to Quaternion: Should be at least 16 bytes from {pointer}, [{bytes.Length} total]");

            return new Quaternion(
                BitConverter.ToSingle(bytes, pointer + 0),
                BitConverter.ToSingle(bytes, pointer + 4),
                BitConverter.ToSingle(bytes, pointer + 8),
                BitConverter.ToSingle(bytes, pointer + 12)
                );
        }
        public static DateTime ToDateTime(byte[] bytes, int pointer = 0)
        {
            if (bytes.Length < pointer + 8)
                throw new IndexOutOfRangeException($"Cannot convert bytes to DateTime: Should be at least 8 bytes from {pointer}, [{bytes.Length} total]");

            return DateTime.FromBinary(BitConverter.ToInt64(bytes, pointer));
        }
        public static IPEndPoint ToIPEndPoint(byte[] bytes, out int bytesUsed, int pointer = 0)
        {
            string str = ToString(bytes, pointer);
            int port = BitConverter.ToInt32(bytes, pointer + 4 + str.Length * 2);
            // + 4 for string length int, + 4 for port
            bytesUsed = str.Length * 2 + 8;
            return new IPEndPoint(IPAddress.Parse(str), port);
        }
    }
}
