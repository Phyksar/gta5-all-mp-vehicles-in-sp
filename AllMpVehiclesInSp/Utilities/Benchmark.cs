﻿using System;
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

        public TimeSpan Measure<TResult>(out TResult result, Func<TResult> action)
        {
            Stopwatch.Restart();
            result = action();
            Stopwatch.Stop();
            return Stopwatch.Elapsed;
        }
    }
}
