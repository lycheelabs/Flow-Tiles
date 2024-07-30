using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace FlowTiles.PortalGraphs {

    public struct Portal {

        public readonly int2 cell;
        public readonly int sector;

        public int color;
        public NativeList<PortalEdge> edges;


        public Portal(int2 cell, int sector) {
            this.cell = cell;
            this.sector = sector;

            color = -1;
            edges = new NativeList<PortalEdge>(10, Allocator.Persistent);
        }
    }

}