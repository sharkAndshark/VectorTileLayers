﻿using Mapsui.VectorTileLayers.Core.Extensions;
using Mapsui.VectorTileLayers.Core.Interfaces;
using Mapsui.VectorTileLayers.Core.Primitives;
using RBush;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mapsui.VectorTileLayers.Core.Styles
{
    public class VectorTileStyle : TileStyle
    {
        public VectorTileStyle(float minZoom, float maxZoom, IEnumerable<IVectorTileStyle> vectorStyles) : base(minZoom, maxZoom)
        {
            VectorTileStyles = new List<IVectorTileStyle>();

            foreach (var styleLayer in vectorStyles)
                ((List<IVectorTileStyle>)VectorTileStyles).Add(styleLayer);
        }

        public IEnumerable<IVectorTileStyle> VectorTileStyles { get; }

        public void UpdateStyles(IViewport viewport)
        {
            EvaluationContext context = new EvaluationContext((float)viewport.Resolution.ToZoomLevel());

            foreach (var vectorTileStyle in VectorTileStyles)
            {
                vectorTileStyle.Update(context);
            }
        }
    }
}
