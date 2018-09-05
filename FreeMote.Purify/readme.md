# FreeMote.Purify

A tool lib to *infer* the key of an EMT PSB file. It doesn't need any other file(e.g. DLL), and can do inference without brute force. It can get the key very fast and is universal for most EMT PSB files. It's NOT based on already known keys.

# Feature
* Support EMT PSB v2-v4 (only works with *EMT* PSB, not for normal PSB)
* Very fast because it's all from inference rather than brute force
* Can handle both header & body(string) encryption

It's unreleased (in a standalone repo) for now. It implements `IPsbKeyProvider` and can be used as a FreeMote plugin.

---
by Ulysses, wdwxy12345@gmail.com


