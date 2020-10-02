using FakeTimes;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tests
{
    [CollectionDefinition("TimeInterceptionTests", DisableParallelization = true)]
    public class TimeInterceptionTests
    {
        private readonly FakeTime _fakeTime;

        public TimeInterceptionTests()
        {
            _fakeTime = new FakeTime();
            _fakeTime.InitialDateTime = new DateTime(2020, 9, 27);
        }

        [Fact]

        public async Task DateTimeNow()
        {
            await _fakeTime.Isolate(async () =>
            {
                Assert.Equal(new DateTime(2020, 9, 27), DateTime.Now);
                Assert.Equal(new DateTime(2020, 9, 27), DateTime.Now);
            });
        }

        [Fact]
        public async Task TaskDelay()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await _fakeTime.Isolate(async () =>
            {
                var delay = Task.Delay(TimeSpan.FromSeconds(10));

                _fakeTime.Tick(TimeSpan.FromSeconds(10));

                await delay;
            });

            stopWatch.Stop();

            Assert.True(stopWatch.ElapsedMilliseconds < 500, $"Dalay is not faked. Time consumed: {stopWatch.Elapsed}");
        }

        [Fact]
        public async Task SerialTaskDelay()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await _fakeTime.Isolate(async () =>
            {
                var delay1 = AsyncMethod1();
                var delay2 = AsyncMethod2();

                _fakeTime.Tick(TimeSpan.FromSeconds(11));

                await Task.WhenAll(delay1, delay2);
                //await delay1;
                //await delay2;
            });

            stopWatch.Stop();

            Assert.True(stopWatch.ElapsedMilliseconds < 500, $"Dalay is not faked. Time consumed: {stopWatch.Elapsed}");
        }

        private async Task AsyncMethod1()
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        private async Task AsyncMethod2()
        {
            await Task.Delay(TimeSpan.FromSeconds(11));
        }

        [Fact]
        public async Task ThreadSleep()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            await _fakeTime.Isolate(async () =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(10));
            });

            stopWatch.Stop();
            Assert.True(stopWatch.ElapsedMilliseconds < 500, $"Sleep is not faked. Time consumed: {stopWatch.Elapsed}");
        }
    }
}
