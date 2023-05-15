using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KitSymes.GTRP.Internal
{
    public class ByteConverter
    {
        public static byte[] GetBytes(string str)
        {
            if (str is null)
                // Add the null check Byte
                return new byte[1] { 0 };

            List<byte> bytes = new List<byte>();

            // Add the null check Byte
            bytes.Add(1);
            // Add the length of the String
            bytes.AddRange(BitConverter.GetBytes(str.Length));
            // Add all the Characters in the String
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
            if (endPoint is null)
                // Add the null check Byte
                return new byte[1] { 0 };

            List<byte> bytes = new List<byte>();

            // Add the null check Byte
            bytes.Add(1);
            bytes.AddRange(GetBytes(endPoint.Address.ToString()));
            bytes.AddRange(BitConverter.GetBytes(endPoint.Port));

            return bytes.ToArray();
        }
        public static byte[] GetBytes(RSAParameters parameters)
        {
            List<byte> bytes = new List<byte>();

            byte nullCheck = 0;

            // If the parameter is not null, set the bit flag and add its data to the List
            if (parameters.D != null) nullCheck |= 1 << 0;
            if (parameters.DP != null) nullCheck |= 1 << 1;
            if (parameters.DQ != null) nullCheck |= 1 << 2;
            if (parameters.Exponent != null) nullCheck |= 1 << 3;
            if (parameters.InverseQ != null) nullCheck |= 1 << 4;
            if (parameters.Modulus != null) nullCheck |= 1 << 5;
            if (parameters.P != null) nullCheck |= 1 << 6;
            if (parameters.Q != null) nullCheck |= 1 << 7;

            bytes.Add(nullCheck);

            if (parameters.D != null) bytes.AddRange(SerialiseArgument(parameters.D));
            if (parameters.DP != null) bytes.AddRange(SerialiseArgument(parameters.DP));
            if (parameters.DQ != null) bytes.AddRange(SerialiseArgument(parameters.DQ));
            if (parameters.Exponent != null) bytes.AddRange(SerialiseArgument(parameters.Exponent));
            if (parameters.InverseQ != null) bytes.AddRange(SerialiseArgument(parameters.InverseQ));
            if (parameters.Modulus != null) bytes.AddRange(SerialiseArgument(parameters.Modulus));
            if (parameters.P != null) bytes.AddRange(SerialiseArgument(parameters.P));
            if (parameters.Q != null) bytes.AddRange(SerialiseArgument(parameters.Q));

            return bytes.ToArray();
        }

        public static string ToString(byte[] bytes, out int bytesUsed, int pointer = 0)
        {
            if (bytes.Length < pointer + sizeof(byte))
                throw new IndexOutOfRangeException($"Cannot convert bytes to String: Not enough bytes for a null check");

            bool isNull = ((bytes[pointer] >> 0) & 1) == 0;
            pointer += sizeof(byte);
            bytesUsed = sizeof(byte);

            if (isNull)
                return null;

            if (bytes.Length < pointer + sizeof(int))
                throw new IndexOutOfRangeException($"Cannot convert bytes to String: Should be at least 4 bytes from {pointer}, [{bytes.Length} total]");

            int length = BitConverter.ToInt32(bytes, pointer);
            bytesUsed += sizeof(int);
            pointer += sizeof(int);

            StringBuilder str = new StringBuilder();

            for (int i = 0; i < length; i++)
            {
                char c = BitConverter.ToChar(bytes, pointer + i * sizeof(char));
                str.Append(c);
                bytesUsed += sizeof(char);
            }

            return str.ToString();
        }
        public static Vector3 ToVector3(byte[] bytes, out int bytesUsed, int pointer = 0)
        {
            if (bytes.Length < pointer + 3 * sizeof(float))
                throw new IndexOutOfRangeException($"Cannot convert bytes to Vector3: Should be at least 12 bytes from {pointer}, [{bytes.Length} total]");

            bytesUsed = 3 * sizeof(float);

            return new Vector3(
                BitConverter.ToSingle(bytes, pointer),
                BitConverter.ToSingle(bytes, pointer + sizeof(float)),
                BitConverter.ToSingle(bytes, pointer + sizeof(float) * 2)
                );
        }
        public static Quaternion ToQuaternion(byte[] bytes, out int bytesUsed, int pointer = 0)
        {
            if (bytes.Length < pointer + 4 * sizeof(float))
                throw new IndexOutOfRangeException($"Cannot convert bytes to Quaternion: Should be at least 16 bytes from {pointer}, [{bytes.Length} total]");
            bytesUsed = 4 * sizeof(float);

            return new Quaternion(
                BitConverter.ToSingle(bytes, pointer),
                BitConverter.ToSingle(bytes, pointer + sizeof(float)),
                BitConverter.ToSingle(bytes, pointer + sizeof(float) * 2),
                BitConverter.ToSingle(bytes, pointer + sizeof(float) * 3)
                );
        }
        public static DateTime ToDateTime(byte[] bytes, out int bytesUsed, int pointer = 0)
        {
            if (bytes.Length < pointer + sizeof(long))
                throw new IndexOutOfRangeException($"Cannot convert bytes to DateTime: Should be at least 8 bytes from {pointer}, [{bytes.Length} total]");

            bytesUsed = sizeof(long);
            return DateTime.FromBinary(BitConverter.ToInt64(bytes, pointer));
        }
        public static IPEndPoint ToIPEndPoint(byte[] bytes, out int bytesUsed, int pointer = 0)
        {
            if (bytes.Length < pointer + sizeof(byte))
                throw new IndexOutOfRangeException($"Cannot convert bytes to ToIPEndPoint: Not enough bytes for a null check");

            bool isNull = ((bytes[pointer] >> 0) & 1) == 0;
            pointer += sizeof(byte);
            bytesUsed = sizeof(byte);

            if (isNull)
                return null;

            string str = ToString(bytes, out int strBytesUsed, pointer);
            pointer += strBytesUsed;
            bytesUsed += strBytesUsed;

            int port = BitConverter.ToInt32(bytes, pointer);
            pointer += sizeof(int);
            bytesUsed += sizeof(int);

            return new IPEndPoint(IPAddress.Parse(str), port);
        }
        public static RSAParameters ToRSAParameters(byte[] bytes, out int bytesUsed, int pointer = 0)
        {
            int initialPointer = pointer;
            RSAParameters parameters;

            byte nullCheck = bytes[pointer];
            pointer += sizeof(byte);

            if (((nullCheck >> 0) & 1) != 0) parameters.D = (byte[])DeserialiseArgument<byte[]>(bytes, ref pointer); else parameters.D = null;
            if (((nullCheck >> 1) & 1) != 0) parameters.DP = (byte[])DeserialiseArgument<byte[]>(bytes, ref pointer); else parameters.DP = null;
            if (((nullCheck >> 2) & 1) != 0) parameters.DQ = (byte[])DeserialiseArgument<byte[]>(bytes, ref pointer); else parameters.DQ = null;
            if (((nullCheck >> 3) & 1) != 0) parameters.Exponent = (byte[])DeserialiseArgument<byte[]>(bytes, ref pointer); else parameters.Exponent = null;
            if (((nullCheck >> 4) & 1) != 0) parameters.InverseQ = (byte[])DeserialiseArgument<byte[]>(bytes, ref pointer); else parameters.InverseQ = null;
            if (((nullCheck >> 5) & 1) != 0) parameters.Modulus = (byte[])DeserialiseArgument<byte[]>(bytes, ref pointer); else parameters.Modulus = null;
            if (((nullCheck >> 6) & 1) != 0) parameters.P = (byte[])DeserialiseArgument<byte[]>(bytes, ref pointer); else parameters.P = null;
            if (((nullCheck >> 7) & 1) != 0) parameters.Q = (byte[])DeserialiseArgument<byte[]>(bytes, ref pointer); else parameters.Q = null;

            bytesUsed = initialPointer - pointer;
            return parameters;
        }

        public static byte[] SerialiseArgument(object obj)
        {
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
                list.AddRange(SerialiseArgument(array.Length));
                //MethodInfo method = typeof(ByteConverter).GetMethod(nameof(ByteConverter.SerialiseArgument));
                //MethodInfo generic = method.MakeGenericMethod(array.GetType().GetElementType());
                for (int i = 0; i < array.Length; i++)
                {
                    list.AddRange(SerialiseArgument(array.GetValue(i)));
                    //list.AddRange((byte[])generic.Invoke(null, new object[] { array.GetValue(i) }));
                }
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
            else if (obj.GetType() == typeof(RSAParameters))
                return ByteConverter.GetBytes((RSAParameters)obj);
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

        public static object DeserialiseArgument<T>(byte[] bytes, ref int pointer)
        {
            // Primitives
            if (typeof(T) == typeof(bool))
            {
                bool obj = BitConverter.ToBoolean(bytes, pointer);
                pointer += sizeof(bool);
                return obj;
            }
            else if (typeof(T) == typeof(short))
            {
                short obj = BitConverter.ToInt16(bytes, pointer);
                pointer += sizeof(short);
                return obj;
            }
            else if (typeof(T) == typeof(int))
            {
                int obj = BitConverter.ToInt32(bytes, pointer);
                pointer += sizeof(int);
                return obj;
            }
            else if (typeof(T) == typeof(long))
            {
                long obj = BitConverter.ToInt64(bytes, pointer);
                pointer += sizeof(long);
                return obj;
            }
            else if (typeof(T) == typeof(ushort))
            {
                ushort obj = BitConverter.ToUInt16(bytes, pointer);
                pointer += sizeof(ushort);
                return obj;
            }
            else if (typeof(T) == typeof(uint))
            {
                uint obj = BitConverter.ToUInt32(bytes, pointer);
                pointer += sizeof(uint);
                return obj;
            }
            else if (typeof(T) == typeof(ulong))
            {
                ulong obj = BitConverter.ToUInt64(bytes, pointer);
                pointer += sizeof(ulong);
                return obj;
            }
            else if (typeof(T) == typeof(float))
            {
                float obj = BitConverter.ToSingle(bytes, pointer);
                pointer += sizeof(float);
                return obj;
            }
            else if (typeof(T) == typeof(double))
            {
                double obj = BitConverter.ToDouble(bytes, pointer);
                pointer += sizeof(double);
                return obj;
            }
            else if (typeof(T) == typeof(char))
            {
                char obj = BitConverter.ToChar(bytes, pointer);
                pointer += sizeof(char);
                return obj;
            }
            else if (typeof(T) == typeof(byte))
            {
                byte obj = bytes[pointer];
                pointer += sizeof(byte);
                return obj;
            }
            else if (typeof(T) == typeof(string))
            {
                string str = ByteConverter.ToString(bytes, out int used, pointer);
                pointer += used;
                return str;
            }
            else if (typeof(T).IsArray)
            {
                int length = BitConverter.ToInt32(bytes, pointer);
                pointer += sizeof(int);
                Type arrayType = typeof(T).GetElementType();
                Array array = Array.CreateInstance(arrayType, length);
                MethodInfo method = typeof(ByteConverter).GetMethod(nameof(ByteConverter.DeserialiseArgument));
                MethodInfo generic = method.MakeGenericMethod(array.GetType().GetElementType());
                for (int i = 0; i < length; i++)
                {
                    object[] parameters = new object[] { bytes, pointer };
                    array.SetValue(generic.Invoke(null, parameters), i);
                    pointer = (int)parameters[1];
                }
                return array;
            }
            // Plugin Provided
            else if (typeof(T) == typeof(Vector3))
            {
                Vector3 obj = ByteConverter.ToVector3(bytes, out int used, pointer);
                pointer += used;
                return obj;
            }
            else if (typeof(T) == typeof(Quaternion))
            {
                Quaternion obj = ByteConverter.ToQuaternion(bytes, out int used, pointer);
                pointer += used;
                return obj;
            }
            else if (typeof(T) == typeof(DateTime))
            {
                DateTime obj = ByteConverter.ToDateTime(bytes, out int used, pointer);
                pointer += used;
                return obj;
            }
            else if (typeof(T) == typeof(IPEndPoint))
            {
                IPEndPoint endpoint = ByteConverter.ToIPEndPoint(bytes, out int used, pointer);
                pointer += used;
                return endpoint;
            }
            else if (typeof(T) == typeof(RSAParameters))
            {
                RSAParameters parameters = ByteConverter.ToRSAParameters(bytes, out int used, pointer);
                pointer += used;
                return parameters;
            }
            // Add Custom Here
            /*else if (typeof(T) == typeof(YourType))
            {
                YourType obj = ByteConverter.ToYourType(bytes, pointer);
                pointer += <number of bytes YourType encoded takes up>;
                return obj;
            }*/
            else
            {
                Debug.LogWarning("Packet Deserialisation: Unsupported type " + typeof(T));
                return null;
            }
        }
    }
}
