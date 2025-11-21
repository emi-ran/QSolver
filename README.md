# QSolver

[Türkçe](README-tr.md) | English

QSolver is a Windows application that helps you capture and process questions from your screen. It sits in your system tray, ready to assist you whenever needed.

Version: 1.3.0

## Features

- **Screen Capture**: Easily capture any region of your screen with a simple click and drag interface
- **Smart Processing**: Quick analysis of captured questions with instant results
- **API Key Management**: Manage multiple API keys with validation support
- **API Key Validation**: Test your API keys to ensure they're working correctly with color-coded status indicators
- **System Tray Integration**: Always accessible but never in your way
- **Modern UI**: Clean and intuitive interface with smooth animations
- **Temporary Storage**: Automatically saves captures in a temporary folder

## Requirements

- Windows OS
- .NET 8.0 Runtime or later ([Download here](https://dotnet.microsoft.com/download/dotnet/8.0/runtime))
- Visual Studio 2022 (for development)
- Internet connection for AI services

## Installation

1. Make sure you have [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) installed
2. Download the latest release from the releases page
3. Extract the files to your preferred location
4. Run `QSolver.exe`
5. Configure your API keys from the system tray menu

## Usage

1. Click the QSolver icon in the system tray
2. Select "Soru Seç" (Select Question)
3. Click and drag to select the region containing your question
4. Wait for the processing animation
5. View the result and click "Onayla" (Confirm) when done

## Development

To build the project:

```bash
dotnet restore
dotnet build
```

To run in development mode:

```bash
dotnet run
```

To create a release build:

```bash
dotnet publish -c Release --self-contained false -p:PublishSingleFile=true
```

The output will be in `bin/Release/net8.0-windows/win-x64/publish/`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Special Thanks

I would like to express my special thanks to Bayazıt S. for creating the idea of this project. If he hadn't come up with the idea, I wouldn't have even thought of making such a program. I would like to thank him for his idea and for his support.
