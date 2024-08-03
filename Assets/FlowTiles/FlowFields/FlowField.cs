﻿using FlowTiles.Utils;
using System;
using Unity.Mathematics;

namespace FlowTiles.FlowFields {

    public struct FlowField {

        public int SectorIndex;
        public short Color;
        public int2 Size;
        public UnsafeField<float2> Directions;
        public TimeSpan GenerationTime;

        public void Dispose() {
            Directions.Dispose();
        }

        public float2 GetFlow (int x, int y) {
            return Directions[x + 1, y + 1];
        }

    }

}