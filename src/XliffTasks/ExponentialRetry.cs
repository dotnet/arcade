using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace XliffTasks
{
    public class ExponentialRetry
    {
        public static void ExecuteWithRetryIOException(
           Action action,
           int maxRetryCount)
        {
            var count = 1;
            foreach (var t in Intervals)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException e)
                {
                    if (count == maxRetryCount)
                        throw new AggregateException($"Retry failed after {count} times", e);
                    count++;
                }
                Thread.Sleep(t);
            }
            throw new Exception("Timer should not be exhausted");
        }

        private static IEnumerable<TimeSpan> Intervals
        {
            get
            {
                var milliseconds = 5;
                while (true)
                {
                    yield return TimeSpan.FromMilliseconds(milliseconds);
                    milliseconds *= 2;
                }
            }
        }
    }
}
