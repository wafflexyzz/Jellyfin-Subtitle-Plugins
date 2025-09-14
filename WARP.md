# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Overview

This repository contains multiple Jellyfin subtitle provider plugins that enable downloading subtitles from various sources:

- **Jellyfin.Plugin.OpenSubtitles** - Downloads subtitles from OpenSubtitles.org
- **Jellyfin.Plugin.Addic7ed** - Downloads subtitles from Addic7ed
- **Jellyfin.Plugin.Podnapisi** - Downloads subtitles from Podnapisi.net
- **Jellyfin.Plugin.TheSubDB** - Downloads subtitles from TheSubDB
- **Jellyfin.Plugin.Subscene** - Downloads subtitles from Subscene
- **OpenSubtitlesHandler** - Shared library for OpenSubtitles API communication

## Prerequisites

- .NET Core SDK (compatible with .NET Standard 2.1)
- Jellyfin Server 10.6.0 or later (for testing)

## Build Commands

### Build Entire Solution
```sh
dotnet build Jellyfin.Plugin.Subtitles.sln --configuration Release
```

### Build Individual Plugin
```sh
# Replace <PluginName> with the specific plugin directory
dotnet build Jellyfin.Plugin.<PluginName>/Jellyfin.Plugin.<PluginName>.csproj --configuration Release
```

### Publish Plugin for Distribution
```sh
# This creates the DLL files ready for Jellyfin
dotnet publish --configuration Release --output bin
```

### Clean Build Artifacts
```sh
dotnet clean
```

## Architecture

### Plugin Structure
Each subtitle plugin follows a consistent pattern:

1. **Plugin.cs** - Main plugin entry point extending `BasePlugin<PluginConfiguration>`
   - Registers the plugin with Jellyfin
   - Provides configuration UI pages
   - Manages plugin lifecycle

2. **{Provider}Downloader.cs** - Implements `ISubtitleProvider` interface
   - Handles subtitle search and download logic
   - Manages API authentication and rate limiting
   - Processes subtitle file formats

3. **Configuration/** - Plugin settings
   - `PluginConfiguration.cs` - Configuration model
   - `configPage.html` - Web UI for plugin settings (embedded resource)

4. **Models/** - Data transfer objects (varies by plugin)
   - API response models
   - Search parameter models

### Key Interfaces and Dependencies

All plugins depend on:
- `MediaBrowser.Controller.Subtitles.ISubtitleProvider` - Core interface for subtitle providers
- `MediaBrowser.Controller.Providers.SubtitleSearchRequest` - Search request model
- `MediaBrowser.Model.Providers.RemoteSubtitleInfo` - Subtitle metadata model

The plugins reference Jellyfin packages:
- `Jellyfin.Controller` (10.6.0+)
- `Jellyfin.Common` (10.6.0+)

### OpenSubtitlesHandler Library

This shared library provides:
- XML-RPC client for OpenSubtitles API
- Movie hash calculation (OpenSubtitles hash algorithm)
- Response models for all API methods
- Compression/decompression utilities

## Code Style

The repository uses `.editorconfig` for consistent formatting:

- **Indentation**: 4 spaces (2 for YAML/XML)
- **Line endings**: LF
- **Charset**: UTF-8
- **C# conventions**:
  - `var` usage preferred for apparent types
  - PascalCase for public members
  - camelCase with `_` prefix for private fields
  - Braces on new lines
  - Sort system directives first

Apply formatting:
```sh
dotnet format
```

## Testing

Currently, the repository does not include unit tests. When adding tests:

1. Create test projects following naming convention: `Jellyfin.Plugin.{PluginName}.Tests`
2. Run tests with:
   ```sh
   dotnet test
   ```

## Development Workflow

### Installing Plugin for Testing

1. Build the plugin
2. Copy the output DLL files to your Jellyfin plugins directory:
   - Linux: `~/.local/share/jellyfin/plugins/`
   - Windows: `%AppData%\Jellyfin\Server\plugins\`
   - Docker: Mount to `/config/plugins/`

3. Restart Jellyfin server
4. Configure the plugin in Jellyfin's Dashboard > Plugins

### Debugging

For debugging within Visual Studio/VS Code:
1. Set up a local Jellyfin development environment
2. Reference the plugin projects in Jellyfin's solution
3. Set breakpoints in the plugin code
4. Run Jellyfin in debug mode

### Common Development Tasks

**Adding a new subtitle provider:**
1. Create new project directory `Jellyfin.Plugin.{ProviderName}`
2. Copy structure from existing plugin
3. Implement `ISubtitleProvider` interface
4. Add project to solution file
5. Update `build.yaml` if needed

**Updating API endpoints:**
- Modify the downloader class methods
- Update any API URL constants
- Test thoroughly with rate limiting in mind

**Adding configuration options:**
1. Update `PluginConfiguration.cs` with new properties
2. Modify `configPage.html` to add UI controls
3. Update downloader to use new configuration

## Important Notes

- Most subtitle APIs require authentication - ensure proper API key/credentials handling
- Implement rate limiting to avoid API bans
- Handle network failures gracefully
- Respect subtitle provider terms of service
- Use async/await patterns consistently
- Log errors appropriately using ILogger

## Plugin Publishing

Each plugin needs a `build.yaml` manifest for the Jellyfin plugin repository:
- Update version numbers in both `.csproj` and `build.yaml`
- Ensure GUIDs match between `Plugin.cs` and `build.yaml`
- List all required DLL artifacts