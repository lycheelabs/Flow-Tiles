using System.Threading;
using Unity.Entities;

namespace FlowTiles {
    public static class ThreadSafety {

        private static int mainThreadId;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init() {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Throws InvalidOperationException if not on the main thread. Blocks main thread until ECS jobs are completed.
        /// </summary>
        public static void EnsureECSThreadSafety() {
            // Check main thread
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId) {
                throw new System.InvalidOperationException("ECS data modification must be done on the main thread!");
            }

            // Complete all jobs
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld.IsCreated) {
                defaultWorld.EntityManager.CompleteAllTrackedJobs();
            }
        }

    }

}