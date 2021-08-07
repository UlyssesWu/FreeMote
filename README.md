# FreeMote
[![Build Status](https://ci.appveyor.com/api/projects/status/github/UlyssesWu/FreeMote?branch=master&svg=true)](https://ci.appveyor.com/project/UlyssesWu/freemote/build/artifacts)

Managed EMT/PSB tool libs.

[Download FreeMote Toolkit](https://github.com/UlyssesWu/FreeMote/releases)

## About PSB
FreeMote is a set of tool/libs for `M2 Packaged Struct Binary` file format. The file header usually starts with `PSB`/`PSZ`/`mdf`, 
and the file extensions usually are `.psb|.psz|.mdf|.pimg|.scn|.mmo|.emtbytes|.mtn|.dpak`.

However, there are some other file formats using the same extensions. They are NOT supported:
* `.psb`: PlayStation Binary (PS3) | PhotoShop Big (Photoshop)
* `.mdf`: Mirror Disc File (Alcohol 120%) | Primary Data File (MSSQL)
* `.mtn`: Motion File (Live2D)

Before submitting an issue or asking a question, please check your PSB file header with a hex editor.

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

* Images: TLG encoding/decoding support via [**FreeMote.Tlg**](https://github.com/Project-AZUSA/TlgLib) (by Ulysses). (x64)
* Shells: Compression/decompression support.
* Audio: Experimental support for audio used in PSB.

### FreeMote.Purify (Unreleased)
Infer and calculate the key used by EMT PSB file just from the PSB file (rather than get from engine).

## Tools
### EmtConvert (FreeMote.Tools.EmtConvert)
Convert EMT PSB files.
### PsbDecompile (FreeMote.Tools.PsbDecompile)
Decompile PSB files to json files and resources.
### PsBuild (FreeMote.Tools.PsBuild)
Compile PSB json files and resources to PSB.
### EmtMake (FreeMote.Tools.EmtMake) (Preview)
Decompile an EMT PSB to MMO project. **The output file is always licensed under [CC-BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). No commercial usage allowed!**
### FreeMote Viewer (FreeMote.Tools.Viewer)
Open and render EMT _pure_ PSB. This tool is powered by [FreeMote.NET](https://github.com/Project-AZUSA/FreeMote.NET#freemoteviewer).

## Build
This project requires **VS 2019** and .NET **4.7.2 - 4.8** to build.

**FreeMote.Plugins** / **FreeMote.Plugins.x64** require a [MyGet feed](https://www.myget.org/feed/monarchsolutions/package/nuget/FreeMote.Tlg) to get external libs made by us. If you don't need FreeMote Plugins, you can unload Plugins projects and remove them from other projects' reference.

To install our own nuget packages, add this feed to VS:

`https://www.myget.org/F/monarchsolutions/api/v3/index.json`


## Test
Get PSB samples for test and research from [FreeMote.Samples](https://github.com/Dual-Vector-Foil/FreeMote.Samples).

Thanks for everyone who provided these samples!

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
* @morkt for [ImageTLG](https://github.com/morkt/GARbro/blob/master/ArcFormats/KiriKiri/ImageTLG.cs), [ArcPSB](https://github.com/morkt/GARbro/blob/master/ArcFormats/Emote/ArcPSB.cs), [PspDecompression](https://github.com/morkt/GARbro/blob/master/ArcFormats/Will/ArcPulltop.cs) code. LICENSE: MIT
* @xdaniel & @FireyFly for [PostProcessing](https://github.com/xdanieldzd/GXTConvert/blob/master/GXTConvert/Conversion/PostProcessing.cs) code. LICENSE: MIT
* @Nyerguds for [BitmapHelper](https://stackoverflow.com/a/45100442) code.
* @[**HopelessHiro**](https://forums.fuwanovel.net/profile/25739-hoplesshiro/), @skilittle as sponsors!
* [vgmstream](https://github.com/vgmstream/vgmstream) and @SilicaAndPina for VAG related code.
* @mafaca for [AstcDecoder](https://github.com/mafaca/UtinyRipper/blob/master/uTinyRipperGUI/ThirdParty/Texture%20converters/AstcDecoder.cs) code. LICENSE: MIT
* All nuget references used in this project.
