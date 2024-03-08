using System;
using System.Diagnostics;

namespace Utilities
{
    public class Benchmark
    {
        private Stopwatch Stopwatch;

        public Benchmark(Stopwatch stopwatch)
        {
            Stopwatch = stopwatch;
        }

        public TimeSpan Measure(Action action)
        {
            Stopwatch.Reset();
            Stopwatch.Start();
            action();
            Stopwatch.Stop();
            return Stopwatch.Elapsed;
        }
    }
}
