# FreeMote
[![Build Status](https://ci.appveyor.com/api/projects/status/github/UlyssesWu/FreeMote?branch=master&svg=true)](https://ci.appveyor.com/project/UlyssesWu/freemote/build/artifacts)

Managed Emote/PSB tool libs.

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
### FreeMote.FreeLive (Unrealistic)
EMT <-> L2D Conversion

## Tools
### EmtConvert (FreeMote.Tools.EmtConvert)
Convert EMT PSB files. A managed version of `emote_conv`(by number201724).
### PsbDecompile (FreeMote.Tools.PsbDecompile)
Decompile PSB files. A managed version of `decompiler`(by number201724).
### PsBuild (FreeMote.Tools.PsBuild)
Compile PSB description json to PSB. A managed version of `pcc`(by number201724).
### EmtMake (FreeMote.Tools.EmtMake) (Preview)
Decompile an EMT PSB to MMO project. **The output file is always licensed under [CC-BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). No commercial usage allowed!**
### FreeMote Viewer (FreeMote.Tools.Viewer)
Open and render EMT _pure_ PSB. This tool requires [FreeMote.NET](https://github.com/Project-AZUSA/FreeMote.NET#freemoteviewer).
### [FreeMote Editor](https://github.com/UlyssesWu/FreeMote.Editor) (FreeMote.Editor) (In Dev)
FreeMote GUI tool.

## Build
This project requires **VS 2017** and .NET 4.6-4.7 to build.

**FreeMote.Plugins** requires a [MyGet feed](https://www.myget.org/feed/monarchsolutions/package/nuget/FreeMote.Tlg) to get Project AZUSA libs as reference. If you don't need FreeMote.Plugins, you can unload FreeMote.Plugins project and remove it from other projects' reference.

To install our own nuget packages, add this feed to VS:

`https://www.myget.org/F/monarchsolutions/api/v3/index.json`


---
by **Ulysses** (wdwxy12345@gmail.com) from Project AZUSA

FreeMote is licensed under **LGPL**.

Additional clauses: If the input is not made by yourself, you are NOT allowed to use the output for any commercial purposes.

[Issue Report](https://github.com/UlyssesWu/FreeMote/issues) · [Pull Request](https://github.com/UlyssesWu/FreeMote/pulls) · [Wiki](https://github.com/UlyssesWu/FreeMote/wiki)

[![Support Us](https://az743702.vo.msecnd.net/cdn/kofi2.png?v=0 "Buy Me a Coffee at ko-fi.com")](https://ko-fi.com/Ulysses)

## Thanks

* @9chu for reverse engineering help.
* @number201724 for PSB format references. LICENSE: MIT
* @nalsas (awatm) for EMT Editor help.
* @WcLyic for some PSB samples and EMT Editor help.
* [MonoGame](https://github.com/MonoGame/MonoGame) for `DxtUtil` code. LICENSE: Ms-PL
* Singyuen Yip for `Adler32` code.
* @gdkchan for [DxtCodec](https://github.com/gdkchan/CEGTool/blob/master/CEGTool/DXTCodec.cs) code.
* @mfascia for [TexturePacker](https://github.com/mfascia/TexturePacker) code.
* @morkt for [ImageTLG](https://github.com/morkt/GARbro/blob/master/ArcFormats/KiriKiri/ImageTLG.cs) code. LICENSE: MIT
* @[**HopelessHiro**](https://forums.fuwanovel.net/profile/25739-hoplesshiro/) as sponsor!
* All nuget references used in this project.
