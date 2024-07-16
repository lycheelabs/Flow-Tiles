using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FlowField
{
    public static class FlowCalculationController
    {
        public static (NativeArray<double2> direction, NativeArray<double> gradient, TimeSpan performance) 
            RequestCalculation(float[,] speeds, Vector2Int source, int width, int height)
        {
            var size = (width + 2) * (height + 2);
            var map = new NativeArray<double>(size, Allocator.TempJob);

            for (var x = 0; x <= width + 1; x++)
            {
                for (var y = 0; y <= height + 1; y++)
                {
                    map[y * (width + 2) + x] = speeds[x, y];
                }
            }
            
            
            var direction = new NativeArray<double2>(size, Allocator.Persistent);
            var distance = new NativeArray<double>(size, Allocator.Persistent);
            var goal = new NativeArray<double2>(size, Allocator.Persistent);

            var job = new FlowCalculationJob()
            {
                Map = map,
                Height = height,
                Width = width,
                Source = source,

                Direction = direction,
                Distance = distance,
                Goal = goal
            };

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            var handle = job.Schedule();
            handle.Complete();
            
            stopwatch.Stop();
            Debug.Log($"FlowCalculationController finished in {stopwatch.Elapsed:mm':'ss':'fff} for {width}x{height}");

            goal.Dispose();
            map.Dispose();

            return (direction, distance, stopwatch.Elapsed);
        }
    }
}