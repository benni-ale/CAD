# DWG File Viewer

A containerized web application built with C# ASP.NET Core for viewing and managing DWG (AutoCAD drawing) files.

## Features

- 📐 Web-based interface for viewing DWG files
- 📋 File information display (size, creation date, modification date)
- ⬇️ Download DWG files directly from the browser
- 🐳 Fully containerized with Docker
- 🎨 Modern, responsive UI

## Prerequisites

- Docker and Docker Compose installed on your system
- (Optional) .NET 8.0 SDK if you want to run locally without Docker

## Quick Start

1. **Build and run with Docker Compose:**
   ```bash
   docker-compose up --build
   ```

2. **Access the application:**
   Open your browser and navigate to `http://localhost:8080`

3. **Add DWG files:**
   Place your DWG files in the `wwwroot/files` directory, or mount them via Docker volumes.

## Project Structure

```
.
├── Program.cs                 # Application entry point
├── Controllers/
│   └── DwgController.cs      # API controller for DWG file operations
├── wwwroot/
│   ├── index.html            # Web interface
│   └── files/                # Directory for DWG files
├── Dockerfile                # Docker image definition
├── docker-compose.yml        # Docker Compose configuration
└── DwgViewer.csproj         # Project file

```

## API Endpoints

- `GET /api/dwg/list` - List all DWG files
- `GET /api/dwg/info/{fileName}` - Get information about a specific file
- `GET /files/{fileName}` - Download a DWG file

## Docker Commands

### Build the image:
```bash
docker build -t dwg-viewer .
```

### Run the container:
```bash
docker run -d -p 8080:80 -v ./wwwroot/files:/app/wwwroot/files dwg-viewer
```

### Stop the container:
```bash
docker-compose down
```

## Development

To run locally without Docker:

```bash
dotnet restore
dotnet run
```

The application will be available at `http://localhost:5000` (or the port configured in `launchSettings.json`).

## Notes

- DWG files are binary AutoCAD drawing files. Full rendering requires specialized CAD software or libraries.
- The current implementation provides file management and download capabilities.
- For full DWG viewing capabilities, consider integrating libraries like:
  - Open Design SDK
  - Teigha.NET
  - Aspose.CAD

## License

This project is provided as-is for viewing and managing DWG files.

