using Unity.Collections;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct CostField {

        public const byte WALL = 255;
        public const byte OPEN = 1;

        public int2 size;
        public NativeArray<byte> Costs;

        public CostField(int2 size) {
            this.size = size;
            Costs = new NativeArray<byte> (size.x * size.y, Allocator.Persistent);
        }

        public byte GetCost(int2 cell) {
            return Costs[cell.x + cell.y * size.x];
        }

        public void Initialise(Map map, int2 corner) {
            for (int x = 0; x < size.x; x++) {
                for (var y = 0; y < size.y; y++) {
                    var index = x + y * size.x;

                    var mapCell = corner + new int2(x, y);
                    var blocked = map.Obstacles[mapCell.y][mapCell.x];
                    if (blocked) Costs[index] = WALL;
                    else Costs[index] = OPEN;
                }
            }
        }

    }

}