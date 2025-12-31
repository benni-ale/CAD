using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure static files
builder.Services.Configure<StaticFileOptions>(options =>
{
    var provider = new FileExtensionContentTypeProvider();
    provider.Mappings[".dwg"] = "application/acad";
    options.ContentTypeProvider = provider;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure default files (must come before UseStaticFiles)
app.UseDefaultFiles();

// Configure static files
app.UseStaticFiles();

app.UseRouting();
app.MapControllers();

// Serve the DWG file from the root directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files")),
    RequestPath = "/files"
});

// Serve cached preview images
var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cache");
if (!Directory.Exists(cacheDir))
{
    Directory.CreateDirectory(cacheDir);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(cacheDir),
    RequestPath = "/cache"
});

app.Run();

