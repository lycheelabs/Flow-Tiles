﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Mathematics;

public class Boundaries
{
    //Top left corner (minimum corner)
    public GridTile Min { get; set; }

    //Bottom right corner (maximum corner)
    public GridTile Max { get; set; }

    public float2 CentrePoint => new float2(Min.x + Max.x, Min.y + Max.y) / 2f;

}
