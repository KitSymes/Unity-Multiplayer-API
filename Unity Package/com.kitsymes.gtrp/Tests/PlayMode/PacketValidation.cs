using System;
using System.Collections;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using KitSymes.GTRP;
using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PacketValidation
{
    /*// A Test behaves as an ordinary method
    [Test]
    public void PacketValidationSimplePasses()
    {
        // Use the Assert class to test conditions
    }

    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator PacketValidationWithEnumeratorPasses()
    {
        // Use the Assert class to test conditions.
        // Use yield to skip a frame.
        yield return null;
    }*/

    [Test]
    public void TestPacketConversion()
    {
        PacketTest original = new PacketTest()
        {
            testBool = true,
            testShort = 1,
            testInt = 2,
            testLong = 3,
            testUShort = 4,
            testUInt = 5,
            testULong = 6,
            testFloat = 7,
            testDouble = 8,
            testChar = '9',
            testByte = 10,
            testString = "11",
            testByteArray = new byte[] { 255, 128, 1},
            testVector3 = new Vector3(12, 13, 14),
            testQuaternion = new Quaternion(15, 16, 17, 18),
            testDateTime = DateTime.FromFileTime(19),
            testIPEndPoint = new IPEndPoint(IPAddress.Parse("20.21.22.23"), 24)
        };

        Packet reformed = PacketFormatter.Deserialise(PacketFormatter.Serialise(original));

        Assert.IsTrue(reformed is PacketTest);

        foreach (FieldInfo field in original.GetType().GetFields())
            Assert.AreEqual(field.GetValue(original), field.GetValue(reformed));
    }

    [UnityTest]
    public IEnumerator TestEmptyTCP()
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
            LocalClient lc = (LocalClient)manager.GetType().GetField("_localClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

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
    public IEnumerator TestMalformedTCP()
    {
        LogAssert.Expect(LogType.Exception, "ClientException: Invalid packetBuffer, read 8 bytes when it should be 255");

        NetworkManager manager = new NetworkManager();
        try
        {
            manager.ServerStart();
            RunAsyncMethodSync(() => manager.ClientStart());
            LocalClient lc = (LocalClient)manager.GetType().GetField("_localClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

            RunAsyncMethodSync(() => lc.WriteTCP(BitConverter.GetBytes((uint)255)));
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
    public IEnumerator TestNotAPacketTCP()
    {
        LogAssert.Expect(LogType.Exception, "ArgumentException: Packet 100 not found");

        NetworkManager manager = new NetworkManager();
        try
        {
            manager.ServerStart();
            RunAsyncMethodSync(() => manager.ClientStart());
            LocalClient lc = (LocalClient)manager.GetType().GetField("_localClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

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
    public IEnumerator TestTooSmallFakePacketTCP()
    {
        LogAssert.Expect(LogType.Exception,
            "ArgumentException: Destination array is not long enough to copy all the items in the collection. Check array index and length.\r\nParameter name: value");

        NetworkManager manager = new NetworkManager();
        try
        {
            manager.ServerStart();
            RunAsyncMethodSync(() => manager.ClientStart());
            LocalClient lc = (LocalClient)manager.GetType().GetField("_localClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

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
    public IEnumerator TestTooBigFakePacketTCP()
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
            LocalClient lc = (LocalClient)manager.GetType().GetField("_localClient", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

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
