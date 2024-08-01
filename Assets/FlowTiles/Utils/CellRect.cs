using Unity.Mathematics;

namespace FlowTiles {

    public struct CellRect {

        public int2 MinCell;
        public int2 MaxCell;

        public CellRect (int2 min, int2 max) {
            MinCell = min; 
            MaxCell = max;

            if (min.x > max.x || min.y > max.y) {
                throw new System.ArgumentException("The provided min boundary exceeds the max boundary");
            }
        }

        public int WidthCells => MaxCell.x - MinCell.x + 1;
        public int HeightCells => MaxCell.y - MinCell.y + 1;
        public int2 SizeCells => MaxCell - MinCell + 1;
        public int2 CentreCell => (MinCell + MaxCell) / 2;
        public float2 CentrePoint => (float2)(MinCell + MaxCell + 1) / 2f;

    }

}