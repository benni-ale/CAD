using Microsoft.AspNetCore.Mvc;
using System.IO;
using Aspose.CAD;
using Aspose.CAD.ImageOptions;

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

            // Per ora restituiamo dati semplificati
            // In futuro si potrebbero estrarre le entità reali dal DWG
            var geometry = new
            {
                sections = new[]
                {
                    new { name = "stato-di-fatto", label = "STATO DI FATTO", floors = new[] { "PIANO TERZO", "SOTTOTETTO" } },
                    new { name = "progetto", label = "PROGETTO", floors = new[] { "PIANO TERZO", "SOTTOTETTO" } }
                },
                defaultHeight = 3.0, // Altezza standard in metri
                previewUrl = $"/api/dwg/preview/{Uri.EscapeDataString(fileName)}"
            };

            return Ok(geometry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting geometry for {FileName}", fileName);
            return StatusCode(500, new { error = $"Failed to get geometry: {ex.Message}" });
        }
    }
}

