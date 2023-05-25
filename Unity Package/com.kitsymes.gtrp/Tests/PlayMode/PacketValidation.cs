using KitSymes.GTRP;
using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;
using NUnit.Framework;
using System;
using System.Collections;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace KitSymes.GTRP.Tests
{
    public class PacketValidation
    {

        // Must be in Play Mode Tests for packet registry to work
        [Test]
        public void TestPacketConversion()
        {
            PacketTest original = new PacketTest()
            {
                testBool          = true,
                testShort         = 1,
                testInt           = 2,
                testLong          = 3,
                testUShort        = 4,
                testUInt          = 5,
                testULong         = 6,
                testFloat         = 7,
                testDouble        = 8,
                testChar          = '9',
                testByte          = 10,
                testString        = "11",
                testByteArray     = new byte[] { 255, 128, 1 },
                testVector3       = new Vector3(12, 13, 14),
                testQuaternion    = new Quaternion(15, 16, 17, 18),
                testDateTime      = DateTime.FromFileTime(19),
                testIPEndPoint    = new IPEndPoint(IPAddress.Parse("20.21.22.23"), 24),
                testRSAParameters = new RSAParameters() { Exponent = new byte[] { 1, 2, 3 } }
            };

            Packet reformed = PacketFormatter.Deserialise(PacketFormatter.Serialise(original));

            Assert.IsTrue(reformed is PacketTest);

            Assert.AreEqual(original.testBool,       ((PacketTest)reformed).testBool);
            Assert.AreEqual(original.testShort,      ((PacketTest)reformed).testShort);
            Assert.AreEqual(original.testInt,        ((PacketTest)reformed).testInt);
            Assert.AreEqual(original.testLong,       ((PacketTest)reformed).testLong);
            Assert.AreEqual(original.testUShort,     ((PacketTest)reformed).testUShort);
            Assert.AreEqual(original.testUInt,       ((PacketTest)reformed).testUInt);
            Assert.AreEqual(original.testULong,      ((PacketTest)reformed).testULong);
            Assert.AreEqual(original.testFloat,      ((PacketTest)reformed).testFloat);
            Assert.AreEqual(original.testDouble,     ((PacketTest)reformed).testDouble);
            Assert.AreEqual(original.testChar,       ((PacketTest)reformed).testChar);
            Assert.AreEqual(original.testByte,       ((PacketTest)reformed).testByte);
            Assert.AreEqual(original.testString,     ((PacketTest)reformed).testString);
            Assert.AreEqual(original.testByteArray,  ((PacketTest)reformed).testByteArray);
            Assert.AreEqual(original.testVector3,    ((PacketTest)reformed).testVector3);
            Assert.AreEqual(original.testQuaternion, ((PacketTest)reformed).testQuaternion);
            Assert.AreEqual(original.testDateTime,   ((PacketTest)reformed).testDateTime);
            Assert.AreEqual(original.testIPEndPoint, ((PacketTest)reformed).testIPEndPoint);

            Assert.AreEqual(original.testRSAParameters.D,        ((PacketTest)reformed).testRSAParameters.D);
            Assert.AreEqual(original.testRSAParameters.DP,       ((PacketTest)reformed).testRSAParameters.DP);
            Assert.AreEqual(original.testRSAParameters.DQ,       ((PacketTest)reformed).testRSAParameters.DQ);
            Assert.AreEqual(original.testRSAParameters.Exponent, ((PacketTest)reformed).testRSAParameters.Exponent);
            Assert.AreEqual(original.testRSAParameters.InverseQ, ((PacketTest)reformed).testRSAParameters.InverseQ);
            Assert.AreEqual(original.testRSAParameters.Modulus,  ((PacketTest)reformed).testRSAParameters.Modulus);
            Assert.AreEqual(original.testRSAParameters.P,        ((PacketTest)reformed).testRSAParameters.P);
            Assert.AreEqual(original.testRSAParameters.Q,        ((PacketTest)reformed).testRSAParameters.Q);
        }

        // Must be in Play Mode Tests for packet registry to work
        [Test]
        public void TestPacketConversionNull()
        {
            // As nothing is set, it will be the default value (either 0'd out or null)
            PacketTest original = new PacketTest();

            Packet reformed = PacketFormatter.Deserialise(PacketFormatter.Serialise(original));

            Assert.IsTrue(reformed is PacketTest);

            foreach (FieldInfo field in original.GetType().GetFields())
                Assert.AreEqual(field.GetValue(original), field.GetValue(reformed));
        }

        [UnityTest]
        public IEnumerator TestTCPEmpty()
        {
            bool pong = false;

            NetworkManager manager = new NetworkManager();
            try
            {
                manager.ServerStart();
                manager.RegisterClientPacketHandler<PacketPong>((a) =>
                {
                    pong = true;
                });
                RunAsyncMethodSync(() => manager.ClientStart());
                LocalClient lc = (LocalClient)manager.GetType().GetField("_clientLocalClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                lc.SceneLoaded();

                _ = lc.WriteTCP(new byte[0]);
                _ = lc.WriteTCP(new PacketPing());

                yield return new WaitUntil(() => pong || !manager.IsClientRunning());

                manager.ClientStop();
            }
            finally
            {
                manager.ServerStop();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestTCPMalformed()
        {
            LogAssert.Expect(LogType.Exception, "OverflowException");

            NetworkManager manager = new NetworkManager();
            try
            {
                manager.ServerStart();
                RunAsyncMethodSync(() => manager.ClientStart());
                LocalClient lc = (LocalClient)manager.GetType().GetField("_clientLocalClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                lc.SceneLoaded();

                RunAsyncMethodSync(() => lc.WriteTCP(BitConverter.GetBytes(uint.MaxValue)));
                RunAsyncMethodSync(() => lc.WriteTCP(new PacketPing()));

                yield return new WaitUntil(() => !manager.IsClientRunning());

                manager.ClientStop();
            }
            finally
            {
                manager.ServerStop();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestTCPNotAPacket()
        {
            LogAssert.Expect(LogType.Exception, "ArgumentException: Packet 100 not found");

            NetworkManager manager = new NetworkManager();
            try
            {
                manager.ServerStart();
                RunAsyncMethodSync(() => manager.ClientStart());
                LocalClient lc = (LocalClient)manager.GetType().GetField("_clientLocalClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                lc.SceneLoaded();

                byte[] bytes = new byte[104];

                byte[] length = BitConverter.GetBytes((uint)(bytes.Length - 4));
                for (int i = 0; i < length.Length; i++)
                    bytes[i] = length[i];

                byte[] packetID = BitConverter.GetBytes((uint)100);
                for (int i = 0; i < length.Length; i++)
                    bytes[i + length.Length] = packetID[i];

                RunAsyncMethodSync(() => lc.WriteTCP(bytes));

                yield return new WaitUntil(() => !manager.IsClientRunning());

                manager.ClientStop();
            }
            finally
            {
                manager.ServerStop();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestTCPTooSmallFakePacket()
        {
            LogAssert.Expect(LogType.Exception, "ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.\r\nParameter name: index");

            NetworkManager manager = new NetworkManager();
            try
            {
                manager.ServerStart();
                RunAsyncMethodSync(() => manager.ClientStart());
                LocalClient lc = (LocalClient)manager.GetType().GetField("_clientLocalClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                lc.SceneLoaded();

                byte[] bytes = new byte[14];

                byte[] length = BitConverter.GetBytes((uint)(bytes.Length - 4));
                for (int i = 0; i < length.Length; i++)
                    bytes[i] = length[i];

                byte[] packetID = BitConverter.GetBytes(PacketFormatter.packetToID[typeof(PacketSpawnObject)]);
                for (int i = 0; i < length.Length; i++)
                    bytes[i + length.Length] = packetID[i];

                RunAsyncMethodSync(() => lc.WriteTCP(bytes));

                yield return new WaitUntil(() => !manager.IsClientRunning());

                manager.ClientStop();
            }
            finally
            {
                manager.ServerStop();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestTCPTooBigFakePacket()
        {
            LogAssert.Expect(LogType.Exception, "ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.\r\nParameter name: index");
            bool pong = false;

            NetworkManager manager = new NetworkManager();
            try
            {
                manager.ServerStart();
                RunAsyncMethodSync(() => manager.ClientStart());
                manager.RegisterClientPacketHandler<PacketPong>((a) =>
                {
                    pong = true;
                });
                LocalClient lc = (LocalClient)manager.GetType().GetField("_clientLocalClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                lc.SceneLoaded();

                byte[] bytes = new byte[2004];

                byte[] length = BitConverter.GetBytes((uint)(bytes.Length - 4));
                for (int i = 0; i < length.Length; i++)
                    bytes[i] = length[i];

                byte[] packetID = BitConverter.GetBytes(PacketFormatter.packetToID[typeof(PacketSpawnObject)]);
                for (int i = 0; i < length.Length; i++)
                    bytes[i + length.Length] = packetID[i];

                RunAsyncMethodSync(() => lc.WriteTCP(bytes));

                RunAsyncMethodSync(() => lc.WriteTCP(new PacketPing()));

                yield return new WaitUntil(() => pong || !manager.IsClientRunning());

                manager.ClientStop();
            }
            finally
            {
                manager.ServerStop();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestTCPEncryption()
        {
            //LogAssert.Expect(LogType.Exception, "");
            bool pong = false;

            NetworkManager manager = new NetworkManager();
            try
            {
                manager.ServerStart();
                RunAsyncMethodSync(() => manager.ClientStart());
                manager.RegisterClientPacketHandler<PacketPong>((a) =>
                {
                    pong = true;
                });
                LocalClient lc = (LocalClient)manager.GetType().GetField("_clientLocalClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                lc.SceneLoaded();

                RunAsyncMethodSync(() => lc.WriteTCP(new PacketPing(), true));

                yield return new WaitUntil(() => pong || !manager.IsClientRunning());

                manager.ClientStop();
            }
            finally
            {
                manager.ServerStop();
            }
            yield return null;
        }

        // Based off of https://answers.unity.com/questions/1597151/async-unit-test-in-test-runner.html
        public static T RunAsyncMethodSync<T>(Func<Task<T>> asyncFunc)
        {
            return Task.Run(async () => await asyncFunc()).GetAwaiter().GetResult();
        }
        public static void RunAsyncMethodSync(Func<Task> asyncFunc)
        {
            Task.Run(async () => await asyncFunc()).GetAwaiter().GetResult();
        }

    }
}
