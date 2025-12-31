using Microsoft.AspNetCore.Mvc;
using System.IO;
using Aspose.CAD;
using Aspose.CAD.ImageOptions;
using Aspose.CAD.FileFormats.Cad;
using System.Text.Json;
using System.Collections.Generic;

namespace DwgViewer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DwgController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DwgController> _logger;

    public DwgController(IWebHostEnvironment environment, ILogger<DwgController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("list")]
    public IActionResult ListFiles()
    {
        try
        {
            var filesDirectory = Path.Combine(_environment.ContentRootPath, "wwwroot", "files");
            if (!Directory.Exists(filesDirectory))
            {
                Directory.CreateDirectory(filesDirectory);
            }

            var files = Directory.GetFiles(filesDirectory, "*.dwg")
                .Select(f => new
                {
                    name = Path.GetFileName(f),
                    size = new FileInfo(f).Length,
                    modified = System.IO.File.GetLastWriteTime(f)
                })
                .ToList();

            return Ok(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing DWG files");
            return StatusCode(500, new { error = "Failed to list files" });
        }
    }

    [HttpGet("info/{fileName}")]
    public IActionResult GetFileInfo(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "files", fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "File not found" });
            }

            var fileInfo = new FileInfo(filePath);
            return Ok(new
            {
                name = fileName,
                size = fileInfo.Length,
                modified = fileInfo.LastWriteTime,
                created = fileInfo.CreationTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file info");
            return StatusCode(500, new { error = "Failed to get file info" });
        }
    }

    [HttpGet("preview/{fileName}")]
    public IActionResult GetPreview(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "files", fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "File not found" });
            }

            // Create cache directory for preview images
            var cacheDir = Path.Combine(_environment.ContentRootPath, "wwwroot", "cache");
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            var cacheFilePath = Path.Combine(cacheDir, $"{Path.GetFileNameWithoutExtension(fileName)}.png");
            
            // Check if cached preview exists
            if (System.IO.File.Exists(cacheFilePath))
            {
                var cacheFileInfo = new FileInfo(cacheFilePath);
                var sourceFileInfo = new FileInfo(filePath);
                
                // Return cached version if source hasn't changed
                if (cacheFileInfo.LastWriteTime >= sourceFileInfo.LastWriteTime)
                {
                    return PhysicalFile(cacheFilePath, "image/png");
                }
            }

            // Convert DWG to PNG using Aspose.CAD
            // Copy to temp location if source is read-only (Docker volume mount)
            string tempFilePath = filePath;
            string? tempFile = null;
            
            try
            {
                // Try to create a temp copy if we can't write to the source directory
                var tempDir = Path.Combine(Path.GetTempPath(), "dwg-viewer");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                
                tempFile = Path.Combine(tempDir, Path.GetFileName(filePath));
                System.IO.File.Copy(filePath, tempFile, true);
                tempFilePath = tempFile;
            }
            catch
            {
                // If copy fails, try with original path
                tempFilePath = filePath;
            }

            try
            {
                using (var image = Image.Load(tempFilePath))
                {
                    var rasterizationOptions = new CadRasterizationOptions
                    {
                        PageWidth = 1600,
                        PageHeight = 1600,
                        BackgroundColor = Aspose.CAD.Color.White
                    };

                    var pngOptions = new PngOptions
                    {
                        VectorRasterizationOptions = rasterizationOptions
                    };

                    image.Save(cacheFilePath, pngOptions);
                }
            }
            finally
            {
                // Clean up temp file if created
                if (tempFile != null && System.IO.File.Exists(tempFile))
                {
                    try
                    {
                        System.IO.File.Delete(tempFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }

            return PhysicalFile(cacheFilePath, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating preview for {FileName}", fileName);
            return StatusCode(500, new { error = $"Failed to generate preview: {ex.Message}" });
        }
    }

    [HttpGet("section/{fileName}/{section}")]
    public IActionResult GetSection(string fileName, string section)
    {
        try
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "files", fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "File not found" });
            }

            var cacheDir = Path.Combine(_environment.ContentRootPath, "wwwroot", "cache");
            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            var cacheFilePath = Path.Combine(cacheDir, $"{Path.GetFileNameWithoutExtension(fileName)}_{section}.png");
            
            if (System.IO.File.Exists(cacheFilePath))
            {
                var cacheFileInfo = new FileInfo(cacheFilePath);
                var sourceFileInfo = new FileInfo(filePath);
                
                if (cacheFileInfo.LastWriteTime >= sourceFileInfo.LastWriteTime)
                {
                    return PhysicalFile(cacheFilePath, "image/png");
                }
            }

            string tempFilePath = filePath;
            string? tempFile = null;
            
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "dwg-viewer");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                
                tempFile = Path.Combine(tempDir, Path.GetFileName(filePath));
                System.IO.File.Copy(filePath, tempFile, true);
                tempFilePath = tempFile;
            }
            catch
            {
                tempFilePath = filePath;
            }

            try
            {
                using (var image = Image.Load(tempFilePath))
                {
                    var rasterizationOptions = new CadRasterizationOptions
                    {
                        PageWidth = 1600,
                        PageHeight = 1600,
                        BackgroundColor = Aspose.CAD.Color.White
                    };

                    // Per ora generiamo l'intera immagine, in futuro si potrebbero filtrare layer specifici
                    var pngOptions = new PngOptions
                    {
                        VectorRasterizationOptions = rasterizationOptions
                    };

                    image.Save(cacheFilePath, pngOptions);
                }
            }
            finally
            {
                if (tempFile != null && System.IO.File.Exists(tempFile))
                {
                    try
                    {
                        System.IO.File.Delete(tempFile);
                    }
                    catch { }
                }
            }

            return PhysicalFile(cacheFilePath, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating section preview for {FileName}, section {Section}", fileName, section);
            return StatusCode(500, new { error = $"Failed to generate section preview: {ex.Message}" });
        }
    }

    [HttpGet("geometry/{fileName}")]
    public IActionResult GetGeometry(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "files", fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "File not found" });
            }

            string tempFilePath = filePath;
            string? tempFile = null;
            
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "dwg-viewer");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                
                tempFile = Path.Combine(tempDir, Path.GetFileName(filePath));
                System.IO.File.Copy(filePath, tempFile, true);
                tempFilePath = tempFile;
            }
            catch
            {
                tempFilePath = filePath;
            }

            var floors = new List<object>();
            
            try
            {
                using (var image = Image.Load(tempFilePath))
                {
                    // Estrai entità dal DWG
                    var entities = ExtractEntities(image);
                    
                    // Organizza per piani (basato su posizione Y o layer)
                    var pianoTerzo = entities.Where(e => IsInFloor(e, "PIANO TERZO")).ToList();
                    var sottotetto = entities.Where(e => IsInFloor(e, "SOTTOTETTO")).ToList();

                    floors.Add(new
                    {
                        name = "PIANO TERZO",
                        y = 0.0,
                        height = 3.0,
                        walls = ConvertToWalls(pianoTerzo),
                        color = new { r = 0.29, g = 0.56, b = 0.89 }
                    });

                    floors.Add(new
                    {
                        name = "SOTTOTETTO",
                        y = 3.0,
                        height = 2.5,
                        walls = ConvertToWalls(sottotetto),
                        color = new { r = 0.48, g = 0.41, b = 0.93 }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not extract detailed geometry, using simplified version");
                // Fallback a geometria semplificata
                floors.Add(new
                {
                    name = "PIANO TERZO",
                    y = 0.0,
                    height = 3.0,
                    walls = new object[0],
                    color = new { r = 0.29, g = 0.56, b = 0.89 }
                });
                floors.Add(new
                {
                    name = "SOTTOTETTO",
                    y = 3.0,
                    height = 2.5,
                    walls = new object[0],
                    color = new { r = 0.48, g = 0.41, b = 0.93 }
                });
            }
            finally
            {
                if (tempFile != null && System.IO.File.Exists(tempFile))
                {
                    try { System.IO.File.Delete(tempFile); } catch { }
                }
            }

            var geometry = new
            {
                sections = new[]
                {
                    new { name = "stato-di-fatto", label = "STATO DI FATTO" },
                    new { name = "progetto", label = "PROGETTO" }
                },
                floors = floors,
                defaultHeight = 3.0
            };

            return Ok(geometry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting geometry for {FileName}", fileName);
            return StatusCode(500, new { error = $"Failed to get geometry: {ex.Message}" });
        }
    }

    private List<EntityData> ExtractEntities(Image image)
    {
        var entities = new List<EntityData>();
        
        try
        {
            // Usa reflection per accedere alle entità senza dipendere da CadImage
            // Prova a estrarre entità direttamente da Entities se disponibile
            try
            {
                var entitiesProperty = image.GetType().GetProperty("Entities");
                if (entitiesProperty != null)
                {
                    var entitiesCollection = entitiesProperty.GetValue(image);
                    if (entitiesCollection is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var entity in enumerable)
                        {
                            var entityData = ExtractEntityData(entity);
                            if (entityData != null && entityData.Points.Count > 0)
                            {
                                entities.Add(entityData);
                            }
                        }
                    }
                }
            }
            catch { }

            // Prova anche a estrarre dai blocchi
            try
            {
                var blocksProperty = image.GetType().GetProperty("Blocks");
                if (blocksProperty != null)
                {
                    var blocks = blocksProperty.GetValue(image);
                    if (blocks is System.Collections.IEnumerable blocksEnumerable)
                    {
                        foreach (var block in blocksEnumerable)
                        {
                            var entitiesProperty = block?.GetType().GetProperty("Entities");
                            if (entitiesProperty != null)
                            {
                                var blockEntities = entitiesProperty.GetValue(block);
                                if (blockEntities is System.Collections.IEnumerable blockEntitiesEnumerable)
                                {
                                    foreach (var entity in blockEntitiesEnumerable)
                                    {
                                        var entityData = ExtractEntityData(entity);
                                        if (entityData != null && entityData.Points.Count > 0)
                                        {
                                            entities.Add(entityData);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting entities from DWG: {Message}", ex.Message);
        }

        return entities;
    }

    private EntityData? ExtractEntityData(object entity)
    {
        try
        {
            var entityData = new EntityData();
            var entityType = entity.GetType().Name;
            entityData.Type = entityType;

            // Estrai layer name se disponibile
            try
            {
                var layerProperty = entity.GetType().GetProperty("LayerName");
                if (layerProperty != null)
                {
                    entityData.Layer = layerProperty.GetValue(entity)?.ToString() ?? "";
                }
            }
            catch { }

            // Estrai coordinate in base al tipo di entità
            switch (entityType)
            {
                case "CadLine":
                    ExtractLineCoordinates(entity, entityData);
                    break;
                case "CadPolyline":
                case "CadLwPolyline":
                    ExtractPolylineCoordinates(entity, entityData);
                    break;
                case "CadArc":
                    ExtractArcCoordinates(entity, entityData);
                    break;
                case "CadCircle":
                    ExtractCircleCoordinates(entity, entityData);
                    break;
                case "CadSpline":
                    ExtractSplineCoordinates(entity, entityData);
                    break;
                default:
                    // Prova a estrarre coordinate generiche
                    ExtractGenericCoordinates(entity, entityData);
                    break;
            }

            return entityData.Points.Count > 0 ? entityData : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error extracting entity data for type {Type}", entity.GetType().Name);
            return null;
        }
    }

    private void ExtractLineCoordinates(object line, EntityData entityData)
    {
        try
        {
            var startX = GetPropertyValue<double>(line, "StartPoint.X");
            var startY = GetPropertyValue<double>(line, "StartPoint.Y");
            var endX = GetPropertyValue<double>(line, "EndPoint.X");
            var endY = GetPropertyValue<double>(line, "EndPoint.Y");

            if (startX.HasValue && startY.HasValue && endX.HasValue && endY.HasValue)
            {
                entityData.Points.Add(new Point2D { X = startX.Value, Y = startY.Value });
                entityData.Points.Add(new Point2D { X = endX.Value, Y = endY.Value });
            }
        }
        catch { }
    }

    private void ExtractPolylineCoordinates(object polyline, EntityData entityData)
    {
        try
        {
            // Prova a ottenere i vertici della polilinea
            var verticesProperty = polyline.GetType().GetProperty("Vertices");
            if (verticesProperty != null)
            {
                var vertices = verticesProperty.GetValue(polyline);
                if (vertices is System.Collections.IEnumerable enumerable)
                {
                    foreach (var vertex in enumerable)
                    {
                        var x = GetPropertyValue<double>(vertex, "X");
                        var y = GetPropertyValue<double>(vertex, "Y");
                        if (x.HasValue && y.HasValue)
                        {
                            entityData.Points.Add(new Point2D { X = x.Value, Y = y.Value });
                        }
                    }
                }
            }
        }
        catch { }
    }

    private void ExtractArcCoordinates(object arc, EntityData entityData)
    {
        try
        {
            var centerX = GetPropertyValue<double>(arc, "Center.X");
            var centerY = GetPropertyValue<double>(arc, "Center.Y");
            var radius = GetPropertyValue<double>(arc, "Radius");
            var startAngle = GetPropertyValue<double>(arc, "StartAngle");
            var endAngle = GetPropertyValue<double>(arc, "EndAngle");

            if (centerX.HasValue && centerY.HasValue && radius.HasValue && 
                startAngle.HasValue && endAngle.HasValue)
            {
                // Approssima l'arco con segmenti
                int segments = 16;
                double angleStep = (endAngle.Value - startAngle.Value) / segments;
                
                for (int i = 0; i <= segments; i++)
                {
                    double angle = startAngle.Value + (angleStep * i);
                    double x = centerX.Value + radius.Value * Math.Cos(angle);
                    double y = centerY.Value + radius.Value * Math.Sin(angle);
                    entityData.Points.Add(new Point2D { X = x, Y = y });
                }
            }
        }
        catch { }
    }

    private void ExtractCircleCoordinates(object circle, EntityData entityData)
    {
        try
        {
            var centerX = GetPropertyValue<double>(circle, "Center.X");
            var centerY = GetPropertyValue<double>(circle, "Center.Y");
            var radius = GetPropertyValue<double>(circle, "Radius");

            if (centerX.HasValue && centerY.HasValue && radius.HasValue)
            {
                // Approssima il cerchio con segmenti
                int segments = 32;
                for (int i = 0; i <= segments; i++)
                {
                    double angle = 2 * Math.PI * i / segments;
                    double x = centerX.Value + radius.Value * Math.Cos(angle);
                    double y = centerY.Value + radius.Value * Math.Sin(angle);
                    entityData.Points.Add(new Point2D { X = x, Y = y });
                }
            }
        }
        catch { }
    }

    private void ExtractSplineCoordinates(object spline, EntityData entityData)
    {
        try
        {
            // Per le spline, estrai i punti di controllo
            var controlPointsProperty = spline.GetType().GetProperty("ControlPoints");
            if (controlPointsProperty != null)
            {
                var controlPoints = controlPointsProperty.GetValue(spline);
                if (controlPoints is System.Collections.IEnumerable enumerable)
                {
                    foreach (var point in enumerable)
                    {
                        var x = GetPropertyValue<double>(point, "X");
                        var y = GetPropertyValue<double>(point, "Y");
                        if (x.HasValue && y.HasValue)
                        {
                            entityData.Points.Add(new Point2D { X = x.Value, Y = y.Value });
                        }
                    }
                }
            }
        }
        catch { }
    }

    private void ExtractGenericCoordinates(object entity, EntityData entityData)
    {
        try
        {
            // Prova a estrarre coordinate generiche
            var startPoint = GetNestedProperty(entity, "StartPoint");
            var endPoint = GetNestedProperty(entity, "EndPoint");
            
            if (startPoint != null)
            {
                var x = GetPropertyValue<double>(startPoint, "X");
                var y = GetPropertyValue<double>(startPoint, "Y");
                if (x.HasValue && y.HasValue)
                {
                    entityData.Points.Add(new Point2D { X = x.Value, Y = y.Value });
                }
            }
            
            if (endPoint != null)
            {
                var x = GetPropertyValue<double>(endPoint, "X");
                var y = GetPropertyValue<double>(endPoint, "Y");
                if (x.HasValue && y.HasValue)
                {
                    entityData.Points.Add(new Point2D { X = x.Value, Y = y.Value });
                }
            }
        }
        catch { }
    }

    private T? GetPropertyValue<T>(object obj, string propertyPath) where T : struct
    {
        try
        {
            var parts = propertyPath.Split('.');
            object? current = obj;
            
            foreach (var part in parts)
            {
                if (current == null) return null;
                var prop = current.GetType().GetProperty(part);
                if (prop == null) return null;
                current = prop.GetValue(current);
            }
            
            if (current is T value)
                return value;
                
            return null;
        }
        catch
        {
            return null;
        }
    }

    private object? GetNestedProperty(object obj, string propertyName)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName);
            return prop?.GetValue(obj);
        }
        catch
        {
            return null;
        }
    }

    private bool IsInFloor(EntityData entity, string floorName)
    {
        // Determina il piano basandosi sul layer name o coordinate Y
        var layerUpper = entity.Layer.ToUpper();
        
        if (floorName == "PIANO TERZO")
        {
            return layerUpper.Contains("TERZO") || 
                   layerUpper.Contains("PIANO") ||
                   (!layerUpper.Contains("SOTTOTETTO") && !layerUpper.Contains("TETTO"));
        }
        else if (floorName == "SOTTOTETTO")
        {
            return layerUpper.Contains("SOTTOTETTO") || 
                   layerUpper.Contains("TETTO");
        }
        
        return false;
    }

    private object[] ConvertToWalls(List<EntityData> entities)
    {
        var walls = new List<object>();
        
        foreach (var entity in entities)
        {
            if (entity.Points.Count >= 2)
            {
                // Crea segmenti di parete da ogni entità
                for (int i = 0; i < entity.Points.Count - 1; i++)
                {
                    var p1 = entity.Points[i];
                    var p2 = entity.Points[i + 1];
                    
                    walls.Add(new
                    {
                        start = new { x = p1.X, y = p1.Y },
                        end = new { x = p2.X, y = p2.Y },
                        type = entity.Type,
                        layer = entity.Layer
                    });
                }
                
                // Chiudi la forma se è una polilinea chiusa
                if (entity.Points.Count > 2 && entity.Type.Contains("Polyline"))
                {
                    var p1 = entity.Points[entity.Points.Count - 1];
                    var p2 = entity.Points[0];
                    
                    walls.Add(new
                    {
                        start = new { x = p1.X, y = p1.Y },
                        end = new { x = p2.X, y = p2.Y },
                        type = entity.Type,
                        layer = entity.Layer
                    });
                }
            }
        }
        
        return walls.ToArray();
    }

    private class EntityData
    {
        public List<Point2D> Points { get; set; } = new();
        public string Type { get; set; } = "";
        public string Layer { get; set; } = "";
    }

    private class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}

