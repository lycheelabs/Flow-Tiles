using System.Collections.Generic;
using System.Diagnostics;

namespace FlowTiles.Utils {

    public static class TimerMessage {

        private static Stack<Stopwatch> instances = new Stack<Stopwatch>();

        public static void Start() {
            var instance = new Stopwatch();
            instance.Start();
            instances.Push(instance);
        }

        public static void Stop (string message) {
            if (instances.Count > 0) {
                var instance = instances.Pop();
                instance.Stop();
                var ms = (int)instance.Elapsed.TotalMilliseconds;
                UnityEngine.Debug.Log(string.Format("{0} in: {1} ms", message, ms));
            }
        }

    }

}