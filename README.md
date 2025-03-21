# RadioBrowser

Search screen:
![Screenshot of the script UI](/img/radioBrowser.Search.jpg)

Favorites screen:
![Screenshot of the script UI](/img/radioBrowser.Favorites.jpg)

## Description

Script for playing radio stations from [radio browser](https://www.radio-browser.info/)

### Prerequisites

.Net SDK 

FFMpeg

### Installation

Clone the repo:
git clone https://github.com/poimenov/RadioBrowser.git

To run:

```bash
dotnet fsi radioBrowser.fsx
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