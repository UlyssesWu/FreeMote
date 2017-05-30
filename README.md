# FreeMote
Managed Emote tool libs.

## Components
### FreeMote
Basic functions. Decrypt or encrypt Emote PSB files.
### FreeMote [(SDK)](https://github.com/Project-AZUSA/FreeMote-SDK)
Special API libs for Emote engine, which takes unencrypted PSB files as input.
### FreeMote.Psb
Parse Emote PSB format.
### FreeMote.PsBuild (In Dev)
Compile and decompile PSB files.
### FreeMote.Purify (Unreleased)
Infer and calculate the key used by Emote PSB file just from the PSB file (rather than get from engine).
### FreeMote.Render (Unrealistic)
Draw the Emote model (statically) without Emote engine.
### FreeMote.FreeLive (Unrealistic)
Emote <-> Live2D Conversion

## Tools
### EmoteConv (FreeMote.Tools.EmotePsbConverter)
Convert Emote PSB files. A managed version of `emote_conv`(by number201724).
### EmoteMeasurer (FreeMote.Tools.EmoteMeasurer) (In Dev)
Measure specific positions of models to help Emote PSB version migration.

---
by **Ulysses** (wdwxy12345@gmail.com) from Project AZUSA

FreeMote is temporarily licensed under LGPL. Members from Project AZUSA can use it freely.

[![Support Us](https://az743702.vo.msecnd.net/cdn/kofi2.png?v=0 "Buy Me a Coffee at ko-fi.com")](https://ko-fi.com/Ulysses)

## Thanks

* @9chu for reverse engineering help.
* @number201724 for psb format.
* @nalsas (awatm) for Emote Editor help.
* Singyuen Yip for `Adler32` code.
