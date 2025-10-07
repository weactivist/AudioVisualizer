# Audio Visualizer

A lightweight Windows audio visualizer that reacts to system sound by displaying a gradient-moving ellipse on the screen. Includes tray icon controls for customization.

## Features

- Real-time audio direction visualization (left/right panning)
- Color customization via context menu
- Adjustable visualizer size via context menu
- Click-through, always-on-top overlay
- Tray icon with exit functionality

## Installation

1. Clone this repository:
```
git clone https://github.com/weactivist/AudioVisualizer.git
```
2. Open the project in Visual Studio or VS Code.
3. Build and run:
```
dotnet run
```
4. For a standalone executable, publish for Windows:
```
dotnet publish -c Release -r win-x64 --self-contained true
```

The output will be in bin\Release\net8.0\win-x64\publish\.

## Usage

- The overlay will automatically appear on screen.
- Right-click the tray icon to:
  - Change the visualizer color
  - Adjust the size
  - Exit the app

## Dependencies
- .NET 8.0
- NAudio (for audio capture)
