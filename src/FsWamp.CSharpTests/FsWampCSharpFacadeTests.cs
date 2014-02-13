using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace FsWamp.CSharpTests
{
    [TestFixture]
    public class FsWampCSharpFacadeTests
    {
        [Test]
        public async Task CanPerformRpc()
        {
            var csharpFacade = new CSharpFacade.Client("localhost", 16000);

            await csharpFacade.Connect();

            var calls = 10000d;
            var sw = Stopwatch.StartNew();
            while (calls-- > 0)
            {
                var res = await csharpFacade.Call("add", "5", "6");
            }
            sw.Stop();

            Console.WriteLine("Calls per second: {0}", 10000d / sw.Elapsed.TotalSeconds);

        }
    }
}
