# RadioBrowser Player

A lightweight, cross-platform desktop radio player built with F# and .NET 8.0. Discover and listen to thousands of internet radio stations, manage your favorites, and never lose track of what you're listening to.

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![F#](https://img.shields.io/badge/F%23-8.0-378BBA?logo=fsharp)](https://fsharp.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

![Screenshot of the script UI](/img/radioBrowser.Photino.jpg)

## ✨ Features

*   **Global Radio Discovery**: Search for radio stations by name, country, or tag using the integrated [RadioBrowser](https://www.radio-browser.info/) API.
*   **Smart Favorites**: Add stations to your personal favorites list for instant access.
*   **Now Playing**: See the current track title (via Icecast metadata) for supported stations.
*   **Listening History**: Automatically keeps a log of all stations you've played.
*   **Modern, Native UI**: A clean and responsive user interface built with Blazor and Fluent UI, running in a lightweight native window via Photino.
*   **Lightweight & Fast**: No heavy Electron shell. The app is a small, fast .NET executable.
*   **Cross-Platform**: Runs on Windows and Linux (maybe on macOS too, but I haven't had a chance to test it).

## 🛠️ Built With

*   **[.NET 8.0](https://dotnet.microsoft.com/ru-ru/download/dotnet/8.0)**: The core runtime and framework.
*   **[F#](https://fsharp.org/)**: The primary programming language.
*   **[Photino.Blazor](https://github.com/tryphotino/photino.Blazor)**: A lightweight native window to host the Blazor UI.
*   **[Fun.Blazor](https://github.com/slaveOftime/Fun.Blazor)**: A famous F#-first DSL for building Blazor UI components.
*   **[fluentui-blazor](https://github.com/microsoft/fluentui-blazor)**: Microsoft's official Fluent UI Blazor components for a modern design.
*   **[LiteDB](https://www.litedb.org/)**: A serverless, embedded .NET NoSQL database for storing favorites.

### Installation

1.  **Download the latest release** from the [Releases](https://github.com/poimenov/RadioBrowser/releases) page. If you don't have [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) runtime installed, use self-contained version

2. Unpack and run it.


### From Source Code
#### Prerequisites

*   [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

#### Build Steps

1.  **Clone the repository**:
    ```bash
    git clone https://github.com/poimenov/RadioBrowser.git
    cd RadioBrowser
    ```

2.  **Restore dependencies**:
    ```bash
    dotnet restore
    ```

3.  **Build the project**:
    ```bash
    dotnet build --configuration Release
    ```

---

**Enjoy the tunes!** 🎵
