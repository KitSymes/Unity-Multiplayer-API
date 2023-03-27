using KitSymes.GTRP.Internal;
using NUnit.Framework;
using System;
using UnityEngine;

public class ConverterTests
{
    [Test]
    public void TestVectorConverter()
    {
        Vector3 original = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        Vector3 different = original + Vector3.one;
        Vector3 reformed = ByteConverter.ToVector3(ByteConverter.GetBytes(original));

        Assert.AreEqual(original, reformed);
        Assert.AreNotEqual(different, reformed);
    }

    [Test]
    public void TestQuaternionConverter()
    {
        Quaternion original = UnityEngine.Random.rotation;
        Quaternion different = new Quaternion(original.x + 1, original.y + 1, original.z + 1, original.w + 1);
        Quaternion reformed = ByteConverter.ToQuaternion(ByteConverter.GetBytes(original));

        Assert.AreEqual(original, reformed);
        Assert.AreNotEqual(different, reformed);
    }

    [Test]
    public void TestDateTimeConverter()
    {
        DateTime original = DateTime.Now;
        DateTime different = original.AddTicks(1);
        DateTime reformed = ByteConverter.ToDateTime(ByteConverter.GetBytes(original));
        Assert.AreEqual(original, reformed);
        Assert.AreNotEqual(different, reformed);
    }

    /*
    [Test]
    public void TestConverter()
    {
        object original = new ();
        object different = original - 1;
        object reformed = ByteConverter.To(ByteConverter.GetBytes(original))

        Assert.AreEqual(original, reformed);
        Assert.AreNotEqual(different, reformed);
    }
    */
}
