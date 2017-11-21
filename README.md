# FreeMote
Managed Emote tool libs.

## Components
### FreeMote
Basic functions. Decrypt or encrypt Emote PSB files.
### FreeMote [(SDK)](https://github.com/Project-AZUSA/FreeMote-SDK)
Special API libs for Emote engine, which takes _pure_ (unencrypted) PSB files as input.
### FreeMote.Psb
Parse Emote PSB format.
### FreeMote.PsBuild
Compile and decompile PSB files. Convert PSB among different platforms.
### FreeMote.Purify (Unreleased)
Infer and calculate the key used by Emote PSB file just from the PSB file (rather than get from engine).
### FreeMote.Render (Unrealistic)
Draw the Emote model (statically) without Emote engine.
### FreeMote.FreeLive (Unrealistic)
Emote <-> Live2D Conversion

## Tools
### EmoteConv (FreeMote.Tools.EmotePsbConverter)
Convert Emote PSB files. A managed version of `emote_conv`(by number201724).
### PsbDecompile (FreeMote.Tools.PsbDecompile)
Decompile PSB files. A managed version of `decompiler`(by number201724).
### PsBuild (FreeMote.Tools.PsBuild)
Compile Emote description json to PSB. A managed version of `pcc`(by number201724).
### [FreeMoteViewer](https://github.com/Project-AZUSA/FreeMote.NET#freemoteviewer) (FreeMote.Tools.Viewer)
Open and render Emote _pure_ PSB.

---
by **Ulysses** (wdwxy12345@gmail.com) from Project AZUSA

FreeMote is temporarily licensed under **LGPL**. Members from Project AZUSA can use it freely.

[Issue Report](https://github.com/UlyssesWu/FreeMote/issues) · [Pull Request](https://github.com/UlyssesWu/FreeMote/pulls) · [Wiki](https://github.com/Project-AZUSA/FreeMote/wiki)

[![Support Us](https://az743702.vo.msecnd.net/cdn/kofi2.png?v=0 "Buy Me a Coffee at ko-fi.com")](https://ko-fi.com/Ulysses)

## Thanks

* @9chu for reverse engineering help.
* @number201724 for PSB format.
* @nalsas (awatm) for Emote Editor help.
* @WcLyic (牧濑红莉栖) for some PSB samples and Emote Editor help.
* [MonoGame](https://github.com/MonoGame/MonoGame) for `DxtUtil` code. LICENSE: Ms-PL
* Singyuen Yip for `Adler32` code.
* @gdkchan for [DxtCodec](https://github.com/gdkchan/CEGTool/blob/master/CEGTool/DXTCodec.cs) code.
* @mfascia for [TexturePacker](https://github.com/mfascia/TexturePacker) code.
* All nuget references used in this project.