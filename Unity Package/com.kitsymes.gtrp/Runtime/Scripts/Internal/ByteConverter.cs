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

        public static byte[] SerialiseObject(object obj)
        {
            if (obj == null)
                return new byte[0];
            // Primitives
            if (obj.GetType() == typeof(bool))
                return BitConverter.GetBytes((bool)obj);
            else if (obj.GetType() == typeof(short))
                return BitConverter.GetBytes((short)obj);
            else if (obj.GetType() == typeof(int))
                return BitConverter.GetBytes((int)obj);
            else if (obj.GetType() == typeof(long))
                return BitConverter.GetBytes((long)obj);
            else if (obj.GetType() == typeof(ushort))
                return BitConverter.GetBytes((ushort)obj);
            else if (obj.GetType() == typeof(uint))
                return BitConverter.GetBytes((uint)obj);
            else if (obj.GetType() == typeof(ulong))
                return BitConverter.GetBytes((ulong)obj);
            else if (obj.GetType() == typeof(float))
                return BitConverter.GetBytes((float)obj);
            else if (obj.GetType() == typeof(double))
                return BitConverter.GetBytes((double)obj);
            else if (obj.GetType() == typeof(char))
                return BitConverter.GetBytes((char)obj);
            else if (obj.GetType() == typeof(byte))
                return new byte[1] { (byte)obj };
            else if (obj.GetType() == typeof(string))
                return ByteConverter.GetBytes((string)obj);
            else if (obj.GetType().IsArray)
            {
                List<byte> list = new List<byte>();
                Array array = obj as Array;
                list.AddRange(SerialiseObject(array.Length));
                for (int i = 0; i < array.Length; i++)
                    list.AddRange(SerialiseObject(array.GetValue(i)));
                return list.ToArray();
            }
            // Plugin Provided
            else if (obj.GetType() == typeof(Vector3))
                return ByteConverter.GetBytes((Vector3)obj);
            else if (obj.GetType() == typeof(Quaternion))
                return ByteConverter.GetBytes((Quaternion)obj);
            else if (obj.GetType() == typeof(DateTime))
                return ByteConverter.GetBytes((DateTime)obj);
            else if (obj.GetType() == typeof(IPEndPoint))
                return ByteConverter.GetBytes((IPEndPoint)obj);
            // Add Custom Here
            /*else if (obj.GetType() == typeof(YourType))
                // Add a custom overload to ByteConverter
                return ByteConverter.GetBytes((YourType)obj);*/
            else
            {
                Debug.LogWarning("Packet Serialisation: Unsupported type " + obj.GetType());
                return new byte[0];
            }
        }
        public static object DeserialiseObject(Type expectedType, byte[] bytes, ref int pointer)
        {
            // Primitives
            if (expectedType == typeof(bool))
            {
                bool obj = BitConverter.ToBoolean(bytes, pointer);
                pointer += 1;
                return obj;
            }
            else if (expectedType == typeof(short))
            {
                short obj = BitConverter.ToInt16(bytes, pointer);
                pointer += 2;
                return obj;
            }
            else if (expectedType == typeof(int))
            {
                int obj = BitConverter.ToInt32(bytes, pointer);
                pointer += 4;
                return obj;
            }
            else if (expectedType == typeof(long))
            {
                long obj = BitConverter.ToInt64(bytes, pointer);
                pointer += 8;
                return obj;
            }
            else if (expectedType == typeof(ushort))
            {
                ushort obj = BitConverter.ToUInt16(bytes, pointer);
                pointer += 2;
                return obj;
            }
            else if (expectedType == typeof(uint))
            {
                uint obj = BitConverter.ToUInt32(bytes, pointer);
                pointer += 4;
                return obj;
            }
            else if (expectedType == typeof(ulong))
            {
                ulong obj = BitConverter.ToUInt64(bytes, pointer);
                pointer += 8;
                return obj;
            }
            else if (expectedType == typeof(float))
            {
                float obj = BitConverter.ToSingle(bytes, pointer);
                pointer += 4;
                return obj;
            }
            else if (expectedType == typeof(double))
            {
                double obj = BitConverter.ToDouble(bytes, pointer);
                pointer += 8;
                return obj;
            }
            else if (expectedType == typeof(char))
            {
                char obj = BitConverter.ToChar(bytes, pointer);
                pointer += 2;
                return obj;
            }
            else if (expectedType == typeof(byte))
            {
                byte obj = bytes[pointer];
                pointer += 1;
                return obj;
            }
            else if (expectedType == typeof(string))
            {
                string str = ByteConverter.ToString(bytes, pointer);
                pointer += 4 + str.Length * 2;
                return str;
            }
            else if (expectedType.IsArray)
            {
                int length = BitConverter.ToInt32(bytes, pointer);
                pointer += 4;
                Type arrayType = expectedType.GetElementType();
                Array array = Array.CreateInstance(arrayType, length);
                for (int i = 0; i < length; i++)
                    array.SetValue(DeserialiseObject(arrayType, bytes, ref pointer), i);
                return array;
            }
            // Plugin Provided
            else if (expectedType == typeof(Vector3))
            {
                Vector3 obj = ByteConverter.ToVector3(bytes, pointer);
                pointer += 12;
                return obj;
            }
            else if (expectedType == typeof(Quaternion))
            {
                Quaternion obj = ByteConverter.ToQuaternion(bytes, pointer);
                pointer += 16;
                return obj;
            }
            else if (expectedType == typeof(DateTime))
            {
                DateTime obj = ByteConverter.ToDateTime(bytes, pointer);
                pointer += 8;
                return obj;
            }
            else if (expectedType == typeof(IPEndPoint))
            {
                IPEndPoint endpoint = ByteConverter.ToIPEndPoint(bytes, out int bitesUsed, pointer);
                pointer += bitesUsed;
                return endpoint;
            }
            // Add Custom Here
            /*else if (expectedType == typeof(YourType))
            {
                YourType obj = ByteConverter.ToYourType(bytes, pointer);
                pointer += <number of bytes YourType encoded takes up>;
                return obj;
            }*/
            else
            {
                Debug.LogWarning("Packet Deserialisation: Unsupported type " + expectedType);
                return null;
            }
        }
    }
}
