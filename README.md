# FreeMote
[![Build Status](https://ci.appveyor.com/api/projects/status/github/UlyssesWu/FreeMote?branch=master&svg=true)](https://ci.appveyor.com/project/UlyssesWu/freemote/build/artifacts)

Managed EMT/PSB tool libs.

[Download FreeMote Toolkit](https://github.com/UlyssesWu/FreeMote/releases)

## Components
### FreeMote
Basic functions. Decrypt or encrypt EMT PSB files.
### FreeMote [SDK](https://github.com/Project-AZUSA/FreeMote-SDK)
Special API libs for EMT engine, which take _pure_ (unencrypted) PSB files as input.
### FreeMote.Psb
Parse PSB format. Draw the EMT model (statically) without EMT engine.
### FreeMote.PsBuild
Compile and decompile PSB files. Convert PSB among different platforms. Recover EMT projects.
### FreeMote.Plugins
External/Experimental features. Read [wiki](https://github.com/UlyssesWu/FreeMote/wiki) for usages.

* Images: TLG encoding/decoding support via [**FreeMote.Tlg**](https://github.com/Project-AZUSA/TlgLib) (by Ulysses).
* Shells: Compression/decompression support.

### FreeMote.Purify (Unreleased)
Infer and calculate the key used by EMT PSB file just from the PSB file (rather than get from engine).
### DualVectorFoil (Unrealistic)
PSB <-> MOC conversion.

## Tools
### EmtConvert (FreeMote.Tools.EmtConvert)
Convert EMT PSB files.
### PsbDecompile (FreeMote.Tools.PsbDecompile)
Decompile PSB files.
### PsBuild (FreeMote.Tools.PsBuild)
Compile PSB description json to PSB.
### EmtMake (FreeMote.Tools.EmtMake) (Preview)
Decompile an EMT PSB to MMO project. **The output file is always licensed under [CC-BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). No commercial usage allowed!**
### FreeMote Viewer (FreeMote.Tools.Viewer)
Open and render EMT _pure_ PSB. This tool requires [FreeMote.NET](https://github.com/Project-AZUSA/FreeMote.NET#freemoteviewer).

## Build
This project requires **VS 2019** and .NET 4.6-4.7.2 to build.

**FreeMote.Plugins** requires a [MyGet feed](https://www.myget.org/feed/monarchsolutions/package/nuget/FreeMote.Tlg) to get external libs made by us. If you don't need FreeMote.Plugins, you can unload FreeMote.Plugins project and remove it from other projects' reference.

To install our own nuget packages, add this feed to VS:

`https://www.myget.org/F/monarchsolutions/api/v3/index.json`


---
by **Ulysses** (wdwxy12345@gmail.com) from Project AZUSA

<a rel="license" href="http://creativecommons.org/licenses/by-nc-sa/4.0/"><img alt="Creative Commons License" style="border-width:0" src="https://i.creativecommons.org/l/by-nc-sa/4.0/88x31.png" /></a><br />FreeMote is licensed under a <a rel="license" href="http://creativecommons.org/licenses/by-nc-sa/4.0/">Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License</a> (CC-BY-NC-SA 4.0).

It's required to attach the text of [FreeMote.LICENSE](https://github.com/UlyssesWu/FreeMote/blob/master/FreeMote/FreeMote.LICENSE.txt) with your release if you're using FreeMote libs.

Some outputs of FreeMote (mmo/psd etc.) are transformed from FreeMote code and are considered as **Adapted Material**. Therefore they're always licensed under **CC-BY-NC-SA 4.0**. [wiki](https://github.com/UlyssesWu/FreeMote/wiki/License)

[Issue Report](https://github.com/UlyssesWu/FreeMote/issues) · [Pull Request](https://github.com/UlyssesWu/FreeMote/pulls) · [Wiki](https://github.com/UlyssesWu/FreeMote/wiki)

[![Support Us](https://az743702.vo.msecnd.net/cdn/kofi2.png?v=0 "Buy Me a Coffee at ko-fi.com")](https://ko-fi.com/Ulysses)

## Thanks

* @9chu for so much help.
* @number201724 for PSB format references. LICENSE: MIT
* @WcLyic for PSB samples and Editor help.
* @nalsas (awatm) for Editor help.
* [MonoGame](https://github.com/MonoGame/MonoGame) for [DxtUtil](https://github.com/UlyssesWu/FreeMote/blob/master/FreeMote/DxtUtil.cs) code. LICENSE: Ms-PL
* Singyuen Yip for [Adler32](https://github.com/UlyssesWu/FreeMote/blob/master/FreeMote/Adler32.cs) code.
* @gdkchan for [DxtCodec](https://github.com/gdkchan/CEGTool/blob/master/CEGTool/DXTCodec.cs) code.
* @mfascia for [TexturePacker](https://github.com/mfascia/TexturePacker) code.
* @morkt for [ImageTLG](https://github.com/morkt/GARbro/blob/master/ArcFormats/KiriKiri/ImageTLG.cs) & [PspDecompression](https://github.com/morkt/GARbro/blob/master/ArcFormats/Will/ArcPulltop.cs) code. LICENSE: MIT
* @xdaniel & @FireyFly for [PostProcessing](https://github.com/xdanieldzd/GXTConvert/blob/master/GXTConvert/Conversion/PostProcessing.cs) code. LICENSE: MIT
* @Nyerguds for [BitmapHelper](https://stackoverflow.com/a/45100442) code.
* @[**HopelessHiro**](https://forums.fuwanovel.net/profile/25739-hoplesshiro/) as sponsor!
* All nuget references used in this project.
