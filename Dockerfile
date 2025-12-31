# Use the official .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Use the official .NET runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create directory for DWG files
RUN mkdir -p /app/wwwroot/files

# Copy the published application
COPY --from=build /app/publish .

# Expose port 80
EXPOSE 80

# Set the entry point
ENTRYPOINT ["dotnet", "DwgViewer.dll"]

