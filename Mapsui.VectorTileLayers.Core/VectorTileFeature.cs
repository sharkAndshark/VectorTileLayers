﻿using BruTile;
using Mapsui.Extensions;
using Mapsui.Logging;
using Mapsui.Styles;
using Mapsui.VectorTileLayers.Core.Enums;
using Mapsui.VectorTileLayers.Core.Interfaces;
using Mapsui.VectorTileLayers.Core.Primitives;
using Mapsui.VectorTileLayers.Core.Styles;
using System;
using System.Collections.Generic;

namespace Mapsui.VectorTileLayers.Core
{
    /// <summary>
    /// VectorTileFeature holds all buckets for a given vector tile
    /// </summary>
    /// <remarks>
    /// There are only buckets for a styles, if there are vector elements 
    /// for the style on this zoom level. So each VectorTileFeature belongs tile 
    /// in a given zoom level.
    /// </remarks>
    public class VectorTileFeature : IFeature, ITileDataSink
    {
        private readonly IEnumerable<IVectorTileStyle> _styles;
        private readonly TileInfo _tileInfo;
        private readonly float _scale;
        private readonly EvaluationContext _context;
        private readonly Dictionary<IVectorTileStyle, IBucket> _buckets = new Dictionary<IVectorTileStyle, IBucket>();

        public VectorTileFeature(TileInfo tileInfo, int tileSize, float tileSizeOfData, IStyle style)
        {
            _styles = ((VectorTileStyle)((StyleCollection)style)[0]).VectorTileStyles;
            _scale = tileSize / tileSizeOfData;
            _tileInfo = tileInfo;
            _context = new EvaluationContext(_tileInfo.Index.Level);
            Extent = _tileInfo.Extent.ToMRect();
        }

        public Dictionary<IVectorTileStyle, IBucket> Buckets => _buckets;

        public TileInfo TileInfo => _tileInfo;

        public MRect Extent { get; }

        public ICollection<IStyle> Styles => null;

        public IEnumerable<string> Fields => null;

        public IDictionary<IStyle, object> RenderedGeometry => new Dictionary<IStyle, object>();

        public object this[string key] { get => null; set => throw new NotImplementedException(); }

        public void Dispose()
        {
            foreach (var bucket in _buckets)
            {
                bucket.Value.Dispose();
            }
        }

        /// <summary>
        /// Function to fill the buckets with the right geometries
        /// </summary>
        /// <param name="element">VectorElement, which contains the geometry</param>
        public void Process(VectorElement element)
        {
            element.Scale(_scale);

            // Now process this element and check, for which style layers it is ok
            foreach (var style in _styles)
            {
                // Is this element a line or polygon and is this style relevant or is it outside the zoom range
                //if (!element.IsPoint && (!style.IsVisible || style.MinZoom > _tileInfo.Index.Level || style.MaxZoom < _tileInfo.Index.Level))
                //    continue;

                // Is this style layer relevant for this feature?
                if (style.SourceLayer != element.Layer)
                    continue;

                // Fullfill element filter for this style layer
                if (!style.Filter.Evaluate(element))
                    continue;

                // Check for different types
                switch (style.Type)
                {
                    case StyleType.Symbol:
                        // Feature is a symbol
                        if (!_buckets.ContainsKey(style))
                            _buckets[style] = new SymbolBucket(style);
                        ((SymbolBucket)_buckets[style]).AddElement(element, _context);
                        break;
                    case StyleType.Line:
                        // Element is a line
                        if (element.IsLine && element.Count > 0)
                        {
                            if (!_buckets.ContainsKey(style))
                                _buckets[style] = new LineBucket();
                            ((LineBucket)_buckets[style]).AddElement(element);
                        }
                        else
                        {
                            // This are things like height of a building
                            // We don't use this up to now
                            Logger.Log(LogLevel.Information, $"Unknown element found. Tags [{element.Tags.ToString()}");
                        }
                        break;
                    case StyleType.Fill:
                        // Element is a fill
                        if (element.IsPolygon && element.Count > 0)
                        {
                            if (!_buckets.ContainsKey(style))
                                _buckets[style] = new FillBucket();
                            ((FillBucket)_buckets[style]).AddElement(element);
                        }
                        else
                        {
                            // This are things like height of a building
                            // We don't use this up to now
                            Logger.Log(LogLevel.Information, $"Unknown element found. Tags [{element.Tags}");
                        }
                        break;
                    default:
                        Logger.Log(LogLevel.Information, $"Element with unknown style found. Tags [{element.Tags}");
                        break;
                }
            }
        }

        public void Completed(QueryResult result)
        {
            if (result == QueryResult.Succes)
            {
                List<IVectorTileStyle> remove = new List<IVectorTileStyle>();

                // Delete empty buckets
                foreach (var bucket in _buckets)
                {
                    if (bucket.Value is FillBucket fillBucket)
                    {
                        if (fillBucket.Paths.Count == 0)
                        {
                            // Bucket is empty
                            remove.Add(bucket.Key);
                        }
                        else
                        {
                            // Simplyfy path
                            fillBucket.SimplifyPaths();
                        }
                    }

                    if (bucket.Value is LineBucket lineBucket)
                    {
                        if (lineBucket.Path.PointCount == 0)
                        {
                            // Bucket is empty
                            remove.Add(bucket.Key);
                        }
                        else
                        {
                            // Simplyfy path
                            lineBucket.SimplifyPath();
                        }
                    }
                }

                if (remove.Count == 0)
                    return;

                foreach(var layer in remove)
                {
                    _buckets.Remove(layer);
                }
            }
        }

        public void CoordinateVisitor(Action<double, double, CoordinateSetter> visit)
        {
            throw new NotImplementedException();
        }
    }
}
