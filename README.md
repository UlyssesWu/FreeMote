# FreeMote
[![Build Status](https://ci.appveyor.com/api/projects/status/github/UlyssesWu/FreeMote?branch=master&svg=true)](https://ci.appveyor.com/project/UlyssesWu/freemote/build/artifacts)

Managed Emote tool libs.

## Components
### FreeMote
Basic functions. Decrypt or encrypt Emote PSB files.
### FreeMote [SDK](https://github.com/Project-AZUSA/FreeMote-SDK)
Special API libs for Emote engine, which take _pure_ (unencrypted) PSB files as input.
### FreeMote.Psb
Parse PSB format. Draw the Emote model (statically) without Emote engine.
### FreeMote.PsBuild
Compile and decompile PSB files. Convert PSB among different platforms.
### FreeMote.Plugins
External/Experimental features. Read [wiki](https://github.com/UlyssesWu/FreeMote/wiki) for usages.

* TLG: Encoding/decoding support via [**FreeMote.Tlg**](https://github.com/Project-AZUSA/TlgLib) (by Ulysses).
* LZ4: Compress/decompress support via [**LZ4.Frame**](https://github.com/UlyssesWu/LZ4.Frame) (by Ulysses).

### FreeMote.Purify (Unreleased)
Infer and calculate the key used by Emote PSB file just from the PSB file (rather than get from engine).
### FreeMote.FreeLive (Unrealistic)
Emote <-> Live2D Conversion

## Tools
### EmoteConv (FreeMote.Tools.EmotePsbConverter)
Convert Emote PSB files. A managed version of `emote_conv`(by number201724).
### PsbDecompile (FreeMote.Tools.PsbDecompile)
Decompile PSB files. A managed version of `decompiler`(by number201724).
### PsBuild (FreeMote.Tools.PsBuild)
Compile PSB description json to PSB. A managed version of `pcc`(by number201724).
### [FreeMoteViewer](https://github.com/Project-AZUSA/FreeMote.NET#freemoteviewer) (FreeMote.Tools.Viewer)
Open and render Emote _pure_ PSB.
### [FreeMote Editor](https://github.com/UlyssesWu/FreeMote.Editor) (FreeMote.Editor) (In Dev)
FreeMote GUI tool.

## Build
This project requires **VS 2017** and .NET 4.6-4.7 to build.

**FreeMote.Plugins** requires a [MyGet feed](https://www.myget.org/feed/monarchsolutions/package/nuget/FreeMote.Tlg) to get FreeMote.Tlg (TlgLib) reference. If you don't need FreeMote.Plugins, you can unload FreeMote.Plugins project and remove it from other projects' reference.

To install `FreeMote.Tlg` nuget package, switch your default project to FreeMote.Plugins and use nuget command:

`PM> Install-Package FreeMote.Tlg -Source https://www.myget.org/F/monarchsolutions/api/v3/index.json`

Or, you can add the nuget feed in your VS. (Recommended) 

---
by **Ulysses** (wdwxy12345@gmail.com) from Project AZUSA

FreeMote is licensed under **LGPL**. You can use FreeMote tools freely, but if you've made other tools using FreeMote, they should be open-source. 

[Issue Report](https://github.com/UlyssesWu/FreeMote/issues) · [Pull Request](https://github.com/UlyssesWu/FreeMote/pulls) · [Wiki](https://github.com/UlyssesWu/FreeMote/wiki)

[![Support Us](https://az743702.vo.msecnd.net/cdn/kofi2.png?v=0 "Buy Me a Coffee at ko-fi.com")](https://ko-fi.com/Ulysses)

## Thanks

* @9chu for reverse engineering help.
* @number201724 for PSB format.
* @nalsas (awatm) for Emote Editor help.
* @WcLyic for some PSB samples and Emote Editor help.
* [MonoGame](https://github.com/MonoGame/MonoGame) for `DxtUtil` code. LICENSE: Ms-PL
* Singyuen Yip for `Adler32` code.
* @gdkchan for [DxtCodec](https://github.com/gdkchan/CEGTool/blob/master/CEGTool/DXTCodec.cs) code.
* @mfascia for [TexturePacker](https://github.com/mfascia/TexturePacker) code.
* @morkt for [ImageTLG](https://github.com/morkt/GARbro/blob/master/ArcFormats/KiriKiri/ImageTLG.cs) code. LICENSE: MIT
* @[**HopelessHiro**](https://forums.fuwanovel.net/profile/25739-hoplesshiro/) as sponsor!
* All nuget references used in this project.
