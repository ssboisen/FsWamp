﻿using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace FsWamp.CSharpTests
{
    [TestFixture]
    public class FsWampCSharpFacadeTests
    {
        [Test]
        [TestCase(10d)]
        [TestCase(100d)]
        [TestCase(1000d)]
        [TestCase(10000d)]
        [TestCase(25000d)]
        public async Task CanPerformRpc(double calls)
        {
            using (var csharpFacade = new WampClient("localhost", 16000))
            {
                await csharpFacade.Connect();
                var sw = Stopwatch.StartNew();
                var count = 0;
                while (count++ < calls)
                {
                    var res = await csharpFacade.Call("add", "5", "6");
                    if(count % 1000 == 0)
                        Console.WriteLine("Calls per second: {0}", count / sw.Elapsed.TotalSeconds);
                }
                sw.Stop();

                Console.WriteLine("Total Calls per second: {0}", count / sw.Elapsed.TotalSeconds);
            }
        }

        [Test]
        [Ignore("Doesn't work at the moment, disconnect not happening")]
        public async Task CanConnectDisconnectAndConnectAgain()
        {
            using (var csharpFacade = new WampClient("localhost", 16000))
            {
                await csharpFacade.Connect();

                var res = await csharpFacade.Call("add", "5", "6");
                Assert.That(res, Is.EqualTo("11"));
                Console.WriteLine("Got result");
                await csharpFacade.Disconnect();
                Console.WriteLine("Disconnected");
                await csharpFacade.Connect();
                Console.WriteLine("Reconnected");
                var res2 = await csharpFacade.Call("add", "5", "6");
                Console.WriteLine("Got result two");
                Assert.That(res2,Is.EqualTo("11"));
            }
        }

        [Test]
        public async Task CanSubscribeToEvents()
        {
            using (var csharpFacade = new WampClient("localhost", 16000))
            {
                await csharpFacade.Connect();

                var observable = csharpFacade.Subscribe("seTopic");

                observable.ObserveOn(TaskPoolScheduler.Default).Subscribe(t => Console.WriteLine("Got content: {0}", t));

                await Task.Delay(5000);
            }
        }
    }
}
