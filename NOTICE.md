# Notices

Smarter Playlist for Jellyfin

Copyright (C) 2026 poindexter12

This program is free software: you can redistribute it and/or modify it under the terms of the GNU
Affero General Public License as published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version. The full text is in [LICENSE](LICENSE).

---

## Third-party code

### Emby.SmartPlaylist.Plugin

Parts of the definition file system layer in
`Jellyfin.Plugin.SmarterPlaylist/SmarterPlaylistFileSystem.cs` originate from
[ppankiewicz/Emby.SmartPlaylist.Plugin](https://github.com/ppankiewicz/Emby.SmartPlaylist.Plugin) and
are used under the MIT licence. Specifically, the bodies of `GetSmarterPlaylistFilePath`,
`GetSmarterPlaylistFilePaths` and `GetAllSmarterPlaylistFilePaths` are that project's, carried
through a rename. The rest of the file — the path containment guards and `GetSmarterPlaylistPath` —
is not.

The MIT licence requires this notice to travel with the code:

```
MIT License

Copyright (c) 2019 ppankiewicz

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR OTHER DEALINGS IN THE SOFTWARE.
```

MIT-licensed code may be combined into an AGPL-3.0 project; the notice above is what that permission
is conditional on.

---

## Relationship to Jellyfin

This plugin is built against the Jellyfin server API and is not part of the Jellyfin project. Jellyfin
is licensed GPL-2.0-or-later, which may be used under GPL-3.0, and GPL-3.0 section 13 expressly
permits combining a GPL-3.0 work with an AGPL-3.0 one. The licence of this plugin therefore does not
conflict with the server it loads into.
