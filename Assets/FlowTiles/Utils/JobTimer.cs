using System;
using System.Diagnostics;
using Unity.Jobs;

namespace FlowTiles {

    public static class JobTimer {

        public static TimeSpan ExecuteAndTimeJob<T>(T job, bool printResult = true) where T : struct, IJob {
            
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            job.Schedule().Complete();
            stopwatch.Stop();

            var timespan = stopwatch.Elapsed;
            if (printResult) {
                UnityEngine.Debug.Log($"Calculated in {timespan:mm':'ss':'fff}");
            }
            return timespan;
        }

    }

}