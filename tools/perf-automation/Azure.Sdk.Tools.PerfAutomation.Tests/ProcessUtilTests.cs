
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Crank.Agent;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class ProcessUtilTests
    {
        private (string filename, string arguments) GetSleepCommand(int timeInSeconds)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ("timeout", $"/t {timeInSeconds}");
            }
            return ("sleep", $"{timeInSeconds}");
        }

        [Test]
        public async Task RunAsync()
        {
            (string filename, string arguments) = GetSleepCommand(1);

            ProcessResult result = await ProcessUtil.RunAsync(filename, arguments);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void RunAsync_Timeout()
        {
            (string filename, string arguments) = GetSleepCommand(2);

            TimeSpan timeout = TimeSpan.FromMilliseconds(100);
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await ProcessUtil.RunAsync(filename, arguments, timeout: timeout);
            });
        }

        [Test]
        public void RunAsync_Cancellation()
        {
            (string filename, string arguments) = GetSleepCommand(2);

            CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await ProcessUtil.RunAsync(filename, arguments, cancellationToken: cts.Token);
            });
        }
    }
}
