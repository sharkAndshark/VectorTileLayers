﻿using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Logging;
using Mapsui.Rendering;
using Mapsui.Rendering.Skia.SkiaStyles;
using Mapsui.Styles;
using Mapsui.VectorTileLayers.Core.Extensions;
using Mapsui.VectorTileLayers.Core.Interfaces;
using Mapsui.VectorTileLayers.Core.Primitives;
using Mapsui.VectorTileLayers.Core.Styles;
using SkiaSharp;
using System;

namespace Mapsui.VectorTileLayers.Core.Renderer
{
    public class VectorTileStyleRenderer : ISkiaStyleRenderer
    {
#if DEBUG
        SKPaint testPaintRect = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 4 ,Color = SKColors.Red };
        SKPaint testPaintTextStroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 4, TextSize = 40, Color = SKColors.White };
        SKPaint testPaintTextFill = new SKPaint { Style = SKPaintStyle.StrokeAndFill, TextSize = 40, Color = SKColors.Red };
#endif

        private SKRect clipRect = new SKRect(0, 0, 512, 512);

        public VectorTileStyleRenderer()
        {
        }

        public bool Draw(SKCanvas canvas, IReadOnlyViewport viewport, ILayer layer, IFeature feature, IStyle style, ISymbolCache symbolCache)
        {
            try
            {
                var vectorTileFeature = (VectorTileFeature)feature;
                var vectorTileStyle = (VectorTileStyle)style;
                var vectorTileLayer = (IVectorTileLayer)layer;
                var zoomLevel = (int)viewport.Resolution.ToZoomLevel();
                var extent = vectorTileFeature.TileInfo.Extent.ToMRect();
                var index = vectorTileFeature.TileInfo.Index;

                canvas.Save();

                var scale = CreateMatrix(canvas, viewport, extent);

                canvas.ClipRect(clipRect);

                var context = new EvaluationContext((float)viewport.Resolution.ToZoomLevel() - 1, 1f / scale);
                var strokeLimit = 1 / scale;

                foreach (var vectorStyle in ((VectorTileStyle)style).VectorTileStyles)
                {
                    if (!vectorTileFeature.Buckets.ContainsKey(vectorStyle))
                        continue;

                    vectorStyle.Update(context);

                    var bucket = vectorTileFeature.Buckets[vectorStyle];

                    if (bucket is LineBucket lineBucket)
                    {
                        if (!lineBucket.Path.Bounds.IntersectsWith(canvas.LocalClipBounds))
                            continue;

                        foreach (var paint in vectorStyle.Paints)
                        {
                            var skPaint = paint.CreatePaint(context);
                            
                            if (skPaint.StrokeWidth < strokeLimit)
                                continue;

                            canvas.DrawPath(lineBucket.Path, skPaint);
                        }
                    }
                    if (bucket is FillBucket fillBucket)
                    {
                        foreach (var paint in vectorStyle.Paints)
                        {
                            var skPaint = paint.CreatePaint(context);

                            if (skPaint.IsStroke)
                            {
                                if (skPaint.StrokeWidth < strokeLimit)
                                    continue;

                                if (fillBucket.Path.Bounds.IntersectsWith(canvas.LocalClipBounds))
                                {
                                    canvas.DrawPath(fillBucket.Path, skPaint);
                                }
                            }
                            else
                            {
                                foreach (var path in fillBucket.Paths)
                                {
                                    if (path.Bounds.IntersectsWith(canvas.LocalClipBounds))
                                    {
                                        canvas.DrawPath(path, skPaint);
                                    }
                                }
                            }
                        }
                    }
                }

#if DEBUG
                canvas.DrawRect(clipRect, testPaintRect);
                canvas.DrawText($"Tile {index.Col}/{index.Row}/{index.Level}", new SKPoint(20, 50), testPaintTextStroke);
                canvas.DrawText($"Tile {index.Col}/{index.Row}/{index.Level}", new SKPoint(20, 50), testPaintTextFill);
#endif

                canvas.Restore();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex.Message, ex);

                return false;
            }

            return true;
        }

        private float CreateMatrix(SKCanvas canvas, IReadOnlyViewport viewport, MRect extent)
        {
            var destinationTopLeft = viewport.WorldToScreen(extent.TopLeft);
            var destinationTopRight = viewport.WorldToScreen(extent.TopRight);

            var dx = destinationTopRight.X - destinationTopLeft.X;
            var dy = destinationTopRight.Y - destinationTopLeft.Y;

            var scale = (float)Math.Sqrt(dx * dx + dy * dy) / 512f;

            canvas.Translate((float)destinationTopLeft.X, (float)destinationTopLeft.Y);
            if (viewport.IsRotated)
                canvas.RotateDegrees((float)viewport.Rotation);
            canvas.Scale(scale);

            return scale;
        }
    }
}
