using System;
using System.Net;
using System.Security.Cryptography;
using UnityEngine;

namespace KitSymes.GTRP.Packets
{
    public class PacketTest : Packet
    {
        public bool testBool;
        public short testShort;
        public int testInt;
        public long testLong;
        public ushort testUShort;
        public uint testUInt;
        public ulong testULong;
        public float testFloat;
        public double testDouble;
        public char testChar;
        public byte testByte;
        public string testString;
        public byte[] testByteArray;
        public Vector3 testVector3;
        public Quaternion testQuaternion;
        public DateTime testDateTime;
        public IPEndPoint testIPEndPoint;
        public RSAParameters testRSAParameters;
    }
}
