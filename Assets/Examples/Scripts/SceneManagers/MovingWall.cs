using Unity.Mathematics;
using UnityEngine;

namespace FlowTiles.Examples {
    public class MovingWall {

        private CostStamp Stamp;
        private int2 Corner;
        private int Direction;
        private int Length;
        private int DelayTicks;
        private PathableLevel Level;
        private int lastMovedTick;

        public MovingWall (int2 corner, int length, int direction, int delayTicks, PathableLevel level) {
            var values = new byte[length, 2];
            for (int i = 0; i < length; i++) {
                values[i, 0] = 255;
                values[i, 1] = 255;
            }

            Stamp = new CostStamp(values);
            Corner = corner;
            Length = length;
            Direction = direction;
            DelayTicks = delayTicks;
            Level = level;

            Place();
        }

        public void Update () {
            int tick = (int)(Time.time * 1);
            if (tick != lastMovedTick) {
                int offset = (tick - DelayTicks);
                if (offset > 0 && offset % 5 == 0) {
                    lastMovedTick = tick;
                    Move();
                }
            }
        }

        private void Move() {
            Clear();

            Corner.x += (Length / 2) * Direction;
            if (Corner.x >= Level.Size.x) {
                Corner.x -= Level.Size.x;
            }
            if (Corner.x <= -Length) {
                Corner.x += Level.Size.x;
            }

            Place();
        }

        private void Place () {
            Level.PlaceStamp(Corner.x, Corner.y, Stamp);
            Level.PlaceStamp(Corner.x - Level.Size.x, Corner.y, Stamp);
            Level.PlaceStamp(Corner.x + Level.Size.x, Corner.y, Stamp);
        }

        private void Clear() {
            Level.ClearStamp(Corner.x, Corner.y, Stamp);
            Level.ClearStamp(Corner.x - Level.Size.x, Corner.y, Stamp);
            Level.ClearStamp(Corner.x + Level.Size.x, Corner.y, Stamp);
        }

    }

}