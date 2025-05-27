# RadioBrowser

Photino Blazor application screen:
![Screenshot of the script UI](/img/radioBrowser.Photino.jpg)

F# Script screen:
![Screenshot of the script UI](/img/radioBrowser.Script.jpg)

## Description

Photino Blazor application and F# script for playing radio stations from [radio browser](https://www.radio-browser.info/)

Works on Ubuntu 24.04

### Prerequisites

.Net SDK 

and for F# script FFMpeg

### Installation

Clone the repo:

```bash
git clone https://github.com/poimenov/RadioBrowser.git
```

To run F# script:

```bash
dotnet fsi RadioBrowser.fsx
```


If you run into problems in linux, you may need to install vlc and vlc dev related libraries

```bash
apt-get install libvlc-dev.
apt-get install vlc
```

If you still have issues you can refer to this [guide](https://code.videolan.org/videolan/LibVLCSharp/blob/3.x/docs/linux-setup.md)

```bash
sudo apt install libx11-dev
```

I tested this script on Ubuntu 22.04 with .Net SDK 8.0