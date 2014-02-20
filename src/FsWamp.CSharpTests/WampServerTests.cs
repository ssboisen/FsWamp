using System.Threading.Tasks;
using NUnit.Framework;

namespace FsWamp.CSharpTests
{
    [TestFixture]
    public class WampServerTests
    {
        [Test]
        public async Task CanStartServer()
        {
            using (var wampServer = new WampServer("localhost", 16000))
            {
                wampServer.Start();

                using (var wampClient = new WampClient("localhost", 16000))
                {
                    var delay = Task.Delay(15000);
                    var connect = wampClient.Connect();

                    var t = await Task.WhenAny(delay, connect);

                    Assert.That(t.Id, Is.EqualTo(connect.Id), "Couldn't connect");
                }
            }

        }

        [Test]
        public async Task CanStopServer()
        {
            using (var wampServer = new WampServer("localhost", 16000))
            {
                wampServer.Start();

                using (var wampClient = new WampClient("localhost", 16000))
                {
                    var delay = Task.Delay(5000);
                    var connect = wampClient.Connect();

                    var t = await Task.WhenAny(delay, connect);

                    Assert.That(t.Id, Is.EqualTo(connect.Id), "Couldn't connect");
                }

                wampServer.Stop();

                using (var wampClient = new WampClient("localhost", 16000))
                {
                    var delay = Task.Delay(5000);
                    var connect = wampClient.Connect();

                    var t = await Task.WhenAny(delay, connect);

                    Assert.That(t.Id, Is.EqualTo(delay.Id), "Shouldn't have been able to connect");
                }
            }

        }
    }
}
