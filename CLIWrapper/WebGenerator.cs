using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BizKit.InterfaceDataModel.Dto;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;
using Unmined.Core.Defaults;
using Unmined.Core.Metapacks;
using Unmined.Core.Model.Stylesheets;
using Unmined.Core.Render.Terrain;
using Unmined.Core.Stylesheets;
using Unmined.Core.Stylesheets.Builder;
using Unmined.Core.Tiles;
using Unmined.Core.Tiles.ImageSharp;
using Unmined.Core.Tiles.Terrain.Slice;
using Unmined.Level.DataSources;
using Unmined.Level.Registry.Blocks;
using Unmined.Level.Registry.Resolver;
using Unmined.Level.Registry.Tags;
using Unmined.Minecraft.Geometry;

namespace CLIWrapper
{
    public class WebGenerator
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "Web");

        private readonly string _origin;
        private readonly string _dest;
        private readonly string _name;
        private readonly bool _force;
        private readonly bool _isUnderground;
        private readonly string _metapack;

        public WebGenerator(string origin, string dest, string name, bool force = true, bool isUnderground = false, string metapack = "Default")
        {
            _origin = origin;
            _dest = dest;
            _name = name;
            _force = force;
            _isUnderground = isUnderground;
            _metapack = metapack;
        }

        public void DoProcess()
        {
            var now = DateTime.Now;
            Logger.Information("World path: {source}", _origin);
            var regionRect = RegionRect.Empty;

            var directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var dtoAutoFactory = new DtoAutoFactory();
            var blockTagRegistry = new BlockTagRegistry(new MetapackReader(dtoAutoFactory)
                .ReadFromFolder(Path.Combine(directoryName, "Metapacks", _metapack)).GetAllTags());
            var blockRegistry = new BlockRegistry();
            var blockTagAssociations = new BlockTagAssociations();
            var settingsProvider =
                new SliceGeneratorBlockSettingsProvider(blockTagAssociations);
            var blockFinder = new BlockFinder();
            var associationManager = new BlockTagAssociationManager(blockRegistry,
                blockTagRegistry, blockTagAssociations,
                blockFinder);
            var stylesheet = dtoAutoFactory.Create<IStylesheet>();
            DefaultStylesheet.CreateDefaultStyleSheet(dtoAutoFactory, stylesheet);

            var blockResolver = new BlockResolver(blockFinder, blockTagRegistry,
                blockTagAssociations);
            var blockStyleProvider = new TerrainRendererBlockStyleProvider(new TerrainRendererBlockStylesGenerator(
                new[]
                {
                    stylesheet
                }, blockResolver));

            var regionBufferSource = new RegionBufferSource(Path.Combine(_origin, "region"));
            var blockDataSource = new BlockDataSourceDimension(regionBufferSource,
                blockRegistry);
            var sliceTileGeneratorOptions = new SliceGeneratorOptions
            {
                UseUndergroundXRay = _isUnderground,
                MaxY = (short) (_isUnderground ? 122 : byte.MaxValue)
            };
            var blockRect = regionBufferSource.BoundsRegionRect.ToBlockRect();
            var regions = regionBufferSource.Regions;
            Logger.Information("World size: {count} regions", regions.Count);
            Logger.Information("World size: {w} x {h} blocks", blockRect.Width, blockRect.Height);
            Logger.Information("World rectangle: r{area}", regionBufferSource.BoundsRegionRect);
            if (!regionRect.IsEmpty)
                blockRect = blockRect.Intersect(regionRect.ToBlockRect());
            Logger.Information("Rendering rectangle: r{area}", blockRect.ToRegionRect());
            Logger.Information("Output path: {output}", _dest);

            var renderer = new TerrainRenderer(new TerrainRendererOptions
                {
                    NightMode = false,
                    TransparentUnderground = _isUnderground,
                    UnknownBlockStyle = new TerrainRendererBlockStyle
                    {
                        BaseColor = new Rgba32(0, 192, 0)
                    }
                }, blockStyleProvider,
                settingsProvider);
            var tileSize = 256;
            var intRect = blockRect.ToIntRect(tileSize);
            var renderedChunks = 0;
            var num1 = 0;
            var tilesTotal = regions.Count * (512 / tileSize) * (512 / tileSize);
            var tilesProcessed = 0;
            var tilesSkipped = 0;
            var tilesRendered = 0;
            var tileStore = GetTileStore(tileSize, 0);
            GenerateHtml(blockRect.ToRegionRect(), directoryName, _name, regions);
            Logger.Information("Rendering zoom level {zoom}", 0);
            foreach (var regionPoint1 in intRect.ToBlockRect(tileSize).ToRegionRect().EnumRegionsZx())
            {
                var regionPoint = regionPoint1;
                if (regions.ContainsKey(regionPoint))
                {
                    var num2 = renderedChunks;
                    Parallel.ForEach(
                        intRect.Intersect(regionPoint.ToBlockRect().ToIntRect(tileSize)).EnumPointsZx(),
                        RenderTile);
                    var num3 = renderedChunks;
                    if (num2 != num3)
                        ++num1;

                    void RenderTile(IntPoint tilePoint)
                    {
                        Interlocked.Increment(ref tilesProcessed);
                        var regionTimestamp = blockDataSource.GetRegionTimestamp(regionPoint);
                        if (!_force)
                        {
                            var tileMetadata = tileStore.GetTileMetadata(tilePoint);
                            if (tileMetadata != null &&
                                GetHash(
                                    regionTimestamp.ToString(CultureInfo.InvariantCulture)) ==
                                tileMetadata.SourceVersionHash && tileStore.HasTile(tilePoint))
                            {
                                ++tilesSkipped;
                                return;
                            }
                        }

                        Logger.Information(
                            "Rendering tile z{zoom} t{tile} [{progress}]", 0, tilePoint,
                            $"{(object) Math.Round(tilesProcessed / (double) tilesTotal * 100.0):F0}%");
                        var rgba32Array = new Rgba32[tileSize * tileSize];
                        for (var index = 0; index < rgba32Array.Length; ++index)
                            rgba32Array[index] = Rgba32.Black;
                        Interlocked.Add(ref renderedChunks,
                            renderer.Render(blockDataSource,
                                    blockRegistry,
                                    blockRect.Intersect(tilePoint.ToBlockRect(tileSize)),
                                    rgba32Array, tileSize, 0, 0, 0, sliceTileGeneratorOptions, CancellationToken.None)
                                .GetAwaiter().GetResult().RenderedChunks);
                        Interlocked.Increment(ref tilesRendered);
                        using (var image =
                            Image.LoadPixelData(rgba32Array, tileSize, tileSize))
                            tileStore.UpdateOrAddTileAsync(tilePoint, new ImageTile(tilePoint, tileSize, image))
                                .GetAwaiter().GetResult();
                        tileStore.SetTileMetadata(tilePoint, new TileMetadata
                        {
                            SourceVersionHash =
                                GetHash(
                                    regionTimestamp.ToString(CultureInfo.InvariantCulture)),
                            VersionHash =
                                GetHash(
                                    DateTime.Now.ToString(CultureInfo.InvariantCulture))
                        });
                    }
                }
            }

            var timeSpan1 = DateTime.Now - now;
            var tileRenderResult = RenderZoomOutTiles(intRect, tileSize,
                regions.Keys.ToArray());
            var timeSpan2 = DateTime.Now - now;
            Logger.Information("Finished");
            Logger.Information("{count} Terrain tiles", tilesTotal);
            Logger.Information("{count} Terrain tiles unchanged", tilesSkipped);
            Logger.Information("{count} Terrain tiles rendered", tilesRendered);
            Logger.Information(
                "Elapsed time: {time}, {chunkSpeed} chunks/s, {regionSpeed} regions/s",
                timeSpan1.ToString("d\\.hh\\:mm\\:ss"),
                ((int) (renderedChunks / timeSpan1.TotalSeconds)).ToString(),
                $"{(object) ((double) num1 / timeSpan1.TotalSeconds):F2}");
            Logger.Information("{count} Zoom-out tiles", tileRenderResult.TilesTotal);
            Logger.Information("{count} Zoom-out tiles unchanged", tileRenderResult.TilesSkipped);
            Logger.Information("{count} Zoom-out tiles rendered", tileRenderResult.TilesRendered);
            Logger.Information("Elapsed time total: {time}",
                timeSpan2.ToString("d\\.hh\\:mm\\:ss"));
        }

        private string GetZoomPath(int zoomLevel)
        {
            return Path.Combine("tiles", string.Format("zoom.{0}", zoomLevel));
        }

        private TileStore<ImageTile, TileMetadata> GetTileStore(
            int tileSize,
            int zoomLevel)
        {
            return new TileStore<ImageTile, TileMetadata>(tileSize,
                new ImageSerializer(ImageFormat.Jpeg),
                Path.Combine(_dest, GetZoomPath(zoomLevel)), ".jpg");
        }

        private ZoomOutTileRenderResult RenderZoomOutTiles(
            IntRect tileRect,
            int tileSize,
            IList<RegionPoint> regions)
        {
            var tileRenderResult = new ZoomOutTileRenderResult();
            for (var zoomLevel = -1; zoomLevel >= -6; --zoomLevel)
            {
                var intRect = tileRect.ZoomBy(zoomLevel);
                tileRenderResult.TilesTotal += intRect.Width * intRect.Height;
            }

            for (var index = -1; index >= -6; --index)
            {
                Logger.Information("Rendering zoom level {zoom}", index);
                var zoomLevel = index + 1;
                var tileStore = GetTileStore(tileSize, index);
                var srcTileStore = GetTileStore(tileSize, zoomLevel);
                foreach (var intPoint1 in tileRect.ZoomBy(index).EnumPointsZx())
                {
                    var tileRegionRect = intPoint1.ToBlockRect(tileSize).ZoomBy(Math.Abs(index)).ToRegionRect();
                    if (!regions.Any(r => tileRegionRect.Contains(r)))
                    {
                        --tileRenderResult.TilesTotal;
                    }
                    else
                    {
                        var srcTileRect = new IntRect(intPoint1.ZoomBy(1), new IntSize(2, 2));
                        var sourceVersionHash = GetSourceVersionHash(srcTileRect, srcTileStore);

                        if (!_force)
                        {
                            var tileMetadata = tileStore.GetTileMetadata(intPoint1);

                            if (tileMetadata != null && sourceVersionHash == tileMetadata.SourceVersionHash &&
                                tileStore.HasTile(intPoint1))
                            {
                                ++tileRenderResult.TilesSkipped;
                                continue;
                            }
                        }

                        Logger.Information("Rendering zoom-out tile z{zoom} t{tile}", index, intPoint1);
                        var image1 = new Image<Rgba32>(tileSize * 2, tileSize * 2);
                        image1.Mutate(ctx =>
                        {
                            foreach (var tilePoint in srcTileRect.EnumPointsZx())
                            {
                                var intPoint = tilePoint - (IntVector) srcTileRect.TopLeft;
                                var image = srcTileStore.GetTileAsync(tilePoint, CancellationToken.None)
                                    .GetAwaiter().GetResult()?.Image;
                                if (image == null)
                                    ctx.Fill(Rgba32.White,
                                        new Rectangle(intPoint.X * tileSize, intPoint.Z * tileSize,
                                            tileSize, tileSize));
                                else
                                    ctx.DrawImage(image,
                                        new Point(intPoint.X * tileSize, intPoint.Z * tileSize), 1f);
                            }

                            ctx.Resize(new Size(tileSize, tileSize), new BicubicResampler(),
                                false);
                        });
                        tileStore.UpdateOrAddTileAsync(intPoint1, new ImageTile(intPoint1, tileSize, image1))
                            .GetAwaiter().GetResult();
                        tileStore.SetTileMetadata(intPoint1, new TileMetadata
                        {
                            SourceVersionHash = sourceVersionHash,
                            VersionHash = GetHash(DateTime.Now.ToString(CultureInfo.InvariantCulture))
                        });
                        ++tileRenderResult.TilesRendered;
                    }
                }
            }

            return tileRenderResult;
        }

        private static string GetSourceVersionHash(
            IntRect srcTileRect,
            ITileStore<ImageTile, TileMetadata> srcTileStore)
        {
            var s = string.Empty;
            foreach (var tilePoint in srcTileRect.EnumPointsZx())
            {
                var tileMetadata = srcTileStore.GetTileMetadata(tilePoint);
                if (tileMetadata != null)
                    s = s + "," + tileMetadata.VersionHash;
            }

            return GetHash(s);
        }

        private static string GetHash(string s)
        {
            return Convert.ToBase64String(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(s)));
        }

        private void GenerateHtml(
            RegionRect mapRegionArea,
            string appDir,
            string worldName,
            Dictionary<RegionPoint, DateTime> regions)
        {
            Logger.Information("Generating HTML");
            if (!Directory.Exists(_dest))
                Directory.CreateDirectory(_dest);
            // using (var zipArchive = ZipFile.OpenRead(Path.Combine(appDir, "Templates", "Web.zip")))
            // {
            //     foreach (var entry in zipArchive.Entries)
            //     {
            //         if (entry.Length != 0L)
            //         {
            //             var str = Path.Combine(_dest, entry.FullName);
            //             var directoryName = Path.GetDirectoryName(str);
            //             if (directoryName != null && !Directory.Exists(directoryName))
            //                 Directory.CreateDirectory(directoryName);
            //             entry.ExtractToFile(str, true);
            //         }
            //     }
            // }

            var dimInfo = @"var UnminedMapProperties = {
                    minZoom: {minZoom},
                    maxZoom: {maxZoom},
                    defaultZoom: {defaultZoom},
                    imageFormat: ""{imageFormat}"",
                    minRegionX: {minRegionX},
                    minRegionZ: {minRegionZ},
                    maxRegionX: {maxRegionX},
                    maxRegionZ: {maxRegionZ},
                    worldName: ""{worldName}""
                    }";
            
            File.WriteAllText(Path.Combine(_dest, "unmined.map.regions.js"),
                "var UnminedMapRegions = [" +
                string.Join(",",
                    regions.Keys.Select(r =>
                        string.Format("{{x:{0},z:{1}}}", r.X, r.Z))) + "];" + Environment.NewLine);
            var path = Path.Combine(_dest, "unmined.map.properties.js");
            if (!File.Exists(path))
            {
                using var st = File.CreateText(path);
                st.Write(dimInfo);
                st.Close();
            }

            var players = Path.Combine(_dest, "lambda.players.js");
            var markers = Path.Combine(_dest, "lambda.markers.js");
            if (!File.Exists(players))
            {
                using var st = File.CreateText(players);
                st.Write(@"var LambdaPlayers = [];");
                st.Close();
            }

            if (!File.Exists(markers))
            {
                using var st = File.CreateText(markers);
                st.Write(@"var LambdaPOIs = [];
var LambdaInfra = [];");
                st.Close();
            }
            
            var str1 = dimInfo.Replace("{minZoom}", "-6").Replace("{maxZoom}", "0")
                .Replace("{defaultZoom}", "0").Replace("{imageFormat}", "jpg").Replace("{minRegionX}",
                    mapRegionArea.Left.ToString(CultureInfo.InvariantCulture));
            var num = mapRegionArea.Top;
            var newValue1 = num.ToString(CultureInfo.InvariantCulture);
            var str2 = str1.Replace("{minRegionZ}", newValue1);
            num = mapRegionArea.Right - 1;
            var newValue2 = num.ToString(CultureInfo.InvariantCulture);
            File.WriteAllText(path,
                str2.Replace("{maxRegionX}", newValue2)
                    .Replace("{maxRegionZ}",
                        (mapRegionArea.Bottom - 1).ToString(CultureInfo.InvariantCulture))
                    .Replace("{worldName}", worldName));
        }

        public class ZoomOutTileRenderResult
        {
            public int TilesRendered { get; set; }

            public int TilesTotal { get; set; }

            public int TilesSkipped { get; set; }
        }
    }
}