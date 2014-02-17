using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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
                    if (count % 1000 == 0)
                        Console.WriteLine("Calls per second: {0}", count / sw.Elapsed.TotalSeconds);
                }
                sw.Stop();

                Console.WriteLine("Total Calls per second: {0}", count / sw.Elapsed.TotalSeconds);
            }
        }

        [Test]
        public async Task CanPerformRpcUsingAPrefix()
        {
            using (var csharpFacade = new WampClient("localhost", 16000))
            {
                await csharpFacade.Connect();
                await csharpFacade.Prefix("calc", "http://localhost/simple/calc#");
                var call = csharpFacade.Call("calc:addd", "5", "6");
                var delay = Task.Delay(5000);
                var r = await Task.WhenAny(call, delay);

                Assert.That(r.Id, Is.Not.EqualTo(delay.Id), "Timed out!");
                Assert.That(call.Result, Is.EqualTo("11"), "Wrong result");
            }
        }

        [Test]
        [Ignore("Can be fixed by having mutable clientwebsocket - but I don't know if the disconnect reconnect scenario is what I want")]
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
                Assert.That(res2, Is.EqualTo("11"));
            }
        }

        [Test]
        public async Task CanSubscribeToAndPublishEvents()
        {
            using (var csharpFacade = new WampClient("localhost", 16000))
            {
                await csharpFacade.Connect();

                var observable = csharpFacade.Subscribe("publishTopic").Replay();
                await Task.Delay(200);

                using (observable.Connect())
                {
                    await csharpFacade.Publish("publishTopic", "selfpublishing");

                    var res = await observable.Take(1);

                    Assert.That(res, Is.EqualTo("\"selfpublishing\""));
                }
            }
        }

        [Test]
        public async Task WhenSubscribedAndThenUnsubscribeNoLongerReceivePublishedMessages()
        {
            using (var client1 = new WampClient("localhost", 16000))
            using (var client2 = new WampClient("localhost", 16000))
            {
                await client1.Connect();
                await client2.Connect();

                var client1Obs = client1.Subscribe("publishTopic").Replay();

                using (client1Obs.Connect())
                {
                    await client2.Publish("publishTopic", "selfpublishing");
                    await client1.Unsubscribe("publishTopic");
                    await client2.Publish("publishTopic", "selfpublishing");

                    var obsTask = client1Obs.Take(2).ToTask();
                    var delay = Task.Delay(5000);

                    var t = await Task.WhenAny(obsTask, delay);

                    Assert.That(delay.Id, Is.EqualTo(t.Id), "Should have timed out");
                }
            }
        }

        [Test]
        public async Task WhenSubscribedAndThenUnsubscribeNoLongerReceivePublishedMessages_DualTest()
        {
            using (var client1 = new WampClient("localhost", 16000))
            using (var client2 = new WampClient("localhost", 16000))
            {
                await client1.Connect();
                await client2.Connect();

                var client1Obs = client1.Subscribe("publishTopic").Replay();
                await Task.Delay(200);

                using (client1Obs.Connect())
                {
                    await client2.Publish("publishTopic", "selfpublishing");
                    await client2.Publish("publishTopic", "selfpublishing");

                    var obsTask = client1Obs.Take(2).ToTask();
                    var delay = Task.Delay(5000);

                    var t = await Task.WhenAny(obsTask, delay);

                    Assert.That(delay.Id, Is.Not.EqualTo(t.Id), "Timed out!");
                }
            }
        }


        [Test]
        public async Task CanSubscribeToFromOneClientAndPublishEventFromAnotherClient()
        {
            using (var client1 = new WampClient("localhost", 16000))
            using (var client2 = new WampClient("localhost", 16000))
            {
                await client1.Connect();
                await client2.Connect();

                var client1Obs = client1.Subscribe("publishTopic").Replay();
                await Task.Delay(200);

                using (client1Obs.Connect())
                {
                    await client2.Publish("publishTopic", "selfpublishing");
                    var obsTask = client1Obs.Take(1).ToTask();
                    var delay = Task.Delay(5000);
                    var t = await Task.WhenAny(obsTask, delay);

                    Assert.That(delay.Id, Is.Not.EqualTo(t.Id), "Timed out");
                    Assert.That(obsTask.Result, Is.EqualTo("\"selfpublishing\""));
                }
            }
        }

        [Test]
        public async Task CanPublishAndReceiveNullEvents()
        {
            using (var client1 = new WampClient("localhost", 16000))
            using (var client2 = new WampClient("localhost", 16000))
            {
                await client1.Connect();
                await client2.Connect();

                var client1Obs = client1.Subscribe("publishTopic").Replay();
                await Task.Delay(200);

                using (client1Obs.Connect())
                {
                    await client2.Publish("publishTopic");
                    var obsTask = client1Obs.Take(1).ToTask();
                    var delay = Task.Delay(5000);
                    var t = await Task.WhenAny(obsTask, delay);

                    Assert.That(delay.Id, Is.Not.EqualTo(t.Id), "Timed out");
                    Assert.That(obsTask.Result, Is.EqualTo("null"));
                }
            }
        }

        [Test]
        public async Task CanPublishAndReceiveRegularStringEvents()
        {
            using (var client1 = new WampClient("localhost", 16000))
            using (var client2 = new WampClient("localhost", 16000))
            {
                await client1.Connect();
                await client2.Connect();

                var client1Obs = client1.Subscribe("publishTopic").Replay();
                await Task.Delay(200);

                using (client1Obs.Connect())
                {
                    await client2.Publish("publishTopic", "plain-old-string");
                    var obsTask = client1Obs.Take(1).ToTask();
                    var delay = Task.Delay(5000);
                    var t = await Task.WhenAny(obsTask, delay);

                    Assert.That(delay.Id, Is.Not.EqualTo(t.Id), "Timed out");
                    Assert.That(obsTask.Result, Is.EqualTo("\"plain-old-string\""));
                }
            }
        }

        [Test]
        public async Task CanPublishAndReceiveComplexObjectsEvents()
        {
            using (var client1 = new WampClient("localhost", 16000))
            using (var client2 = new WampClient("localhost", 16000))
            {
                await client1.Connect();
                await client2.Connect();

                var client1Obs = client1.Subscribe("publishTopic").Replay();
                await Task.Delay(200);

                using (client1Obs.Connect())
                {
                    await client2.Publish("publishTopic", "{ \"age\": 2 }");
                    var obsTask = client1Obs.Take(1).ToTask();
                    var delay = Task.Delay(5000);
                    var t = await Task.WhenAny(obsTask, delay);

                    Assert.That(delay.Id, Is.Not.EqualTo(t.Id), "Timed out");
                    Assert.That(obsTask.Result, Is.EqualTo("{ \"age\": 2 }"));
                }
            }
        }
    }
}
