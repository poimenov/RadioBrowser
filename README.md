# MusicSearchDownload

Script for searching and downloading music from z3.fm (sript.fsx)
![Screenshot of the script UI](/img/screen.jpg)

Script for searching and downloading music from youtube (fromYoutube.fsx)
![Screenshot of the script UI](/img/screen1.jpg)

Script for searching and downloading music from spotify (fromSpotify.fsx)
![Screenshot of the script UI](/img/screen2.jpg)

Script for [radio browser](https://www.radio-browser.info/) (radioBrowser.fsx)
![Screenshot of the script UI](/img/screen3.jpg)

## Description

Scripts for searching and downloading music

### Prerequisites

.Net SDK 

FFMpeg

### Installation

Clone the repo:
git clone https://github.com/poimenov/MusicSearchDownload.git

To run:

```bash
dotnet fsi script.fsx
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