using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using KitSymes.GTRP;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ConnectionTests
{
    /*// A Test behaves as an ordinary method
    [Test]
    public void TestStub()
    {
        // Use the Assert class to test conditions
    }

    // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
    // `yield return null;` to skip a frame.
    [UnityTest]
    public IEnumerator TempTestsWithEnumeratorPasses()
    {
        // Use the Assert class to test conditions.
        // Use yield to skip a frame.
        yield return null;
    }*/

    [Test]
    public void TestCanConnect()
    {
        NetworkManager manager = new NetworkManager();
        manager.ServerStart();
        RunAsyncMethodSync(() => manager.ClientStart());
        manager.ClientStop();

        manager.ServerStop();
    }

    [Test]
    public void TestCantConnect()
    {
        LogAssert.Expect(LogType.Error, "Client could not connect");

        NetworkManager manager = new NetworkManager();
        Assert.Throws<ClientException>(() => RunAsyncMethodSync(() => manager.ClientStart()));
        manager.ClientStop();
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
