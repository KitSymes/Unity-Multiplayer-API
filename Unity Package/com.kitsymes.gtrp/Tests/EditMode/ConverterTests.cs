using KitSymes.GTRP.Internal;
using NUnit.Framework;
using System;
using System.Net;
using System.Security.Cryptography;
using UnityEngine;

namespace KitSymes.GTRP.Tests
{
    public class ConverterTests
    {
        [Test]
        public void TestStringConverter()
        {
            string original = "Testing String Serialisation";
            string different = original + " random garbage";
            string reformed = ByteConverter.ToString(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original, reformed);
            Assert.AreNotEqual(different, reformed);
        }

        [Test]
        public void TestStringNullConverter()
        {
            string original = null;
            string different = "Random garbage";
            string reformed = ByteConverter.ToString(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original, reformed);
            Assert.AreNotEqual(different, reformed);
        }

        [Test]
        public void TestVectorConverter()
        {
            Vector3 original = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
            Vector3 different = original + Vector3.one;
            Vector3 reformed = ByteConverter.ToVector3(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original, reformed);
            Assert.AreNotEqual(different, reformed);
        }

        [Test]
        public void TestQuaternionConverter()
        {
            Quaternion original = UnityEngine.Random.rotation;
            Quaternion different = new Quaternion(original.x + 1, original.y + 1, original.z + 1, original.w + 1);
            Quaternion reformed = ByteConverter.ToQuaternion(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original, reformed);
            Assert.AreNotEqual(different, reformed);
        }

        [Test]
        public void TestDateTimeConverter()
        {
            DateTime original = DateTime.Now;
            DateTime different = original.AddTicks(1);
            DateTime reformed = ByteConverter.ToDateTime(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original, reformed);
            Assert.AreNotEqual(different, reformed);
        }

        [Test]
        public void TestIPEndPointConverter()
        {
            IPEndPoint original = new IPEndPoint(IPAddress.Parse("12.34.56.78"), 90);
            IPEndPoint different = new IPEndPoint(IPAddress.Parse("20.21.22.23"), 24);
            IPEndPoint reformed = ByteConverter.ToIPEndPoint(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original, reformed);
            Assert.AreNotEqual(different, reformed);
        }

        [Test]
        public void TestIPEndPointNullConverter()
        {
            IPEndPoint original = null;
            IPEndPoint different = new IPEndPoint(IPAddress.Parse("20.21.22.23"), 24);
            IPEndPoint reformed = ByteConverter.ToIPEndPoint(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original, reformed);
            Assert.AreNotEqual(different, reformed);
        }

        [Test]
        public void TestRSAParametersConverter()
        {
            RSAParameters original = new RSAParameters() { Exponent = new byte[] { 1, 2, 3 } };
            RSAParameters reformed = ByteConverter.ToRSAParameters(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original.D, reformed.D);
            Assert.AreEqual(original.DP, reformed.DP);
            Assert.AreEqual(original.DQ, reformed.DQ);
            Assert.AreEqual(original.Exponent, reformed.Exponent);
            Assert.AreEqual(original.InverseQ, reformed.InverseQ);
            Assert.AreEqual(original.Modulus, reformed.Modulus);
            Assert.AreEqual(original.P, reformed.P);
            Assert.AreEqual(original.Q, reformed.Q);
        }

        [Test]
        public void TestRSAParametersNullArgumentsConverter()
        {
            RSAParameters original = new RSAParameters() { };
            RSAParameters reformed = ByteConverter.ToRSAParameters(ByteConverter.GetBytes(original), out _);

            Assert.AreEqual(original.D, reformed.D);
            Assert.AreEqual(original.DP, reformed.DP);
            Assert.AreEqual(original.DQ, reformed.DQ);
            Assert.AreEqual(original.Exponent, reformed.Exponent);
            Assert.AreEqual(original.InverseQ, reformed.InverseQ);
            Assert.AreEqual(original.Modulus, reformed.Modulus);
            Assert.AreEqual(original.P, reformed.P);
            Assert.AreEqual(original.Q, reformed.Q);
        }
    }
}
