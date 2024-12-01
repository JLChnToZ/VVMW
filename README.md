# VizVid

![Banner](Packages/idv.jlchntoz.vvmw/.tutorial/cover.png)

Welcome! VizVid is a general-purpose video player frontend for use in VRChat. It aims to cover many use cases, from watch-together video/live stream player in lounges, to large event venue for music performances, or even booths for exhibitions or showcases. Due to its target customers, it has a flexible architecture, just like a factory made electronic but with a easy to open back lid, make it easier to let users mess them around for their needs.

## Features
- Basic playback, seeking controls
- Pre-defined playlists & user queue list
- Playback history for user inputed URLs (since v1.0.32)
- Quest (Android) client specific URLs (only available on pre-defined URLs, play lists and API)
- PNG/JPEG Image Viewer (since v1.0.37)
- Low latency mode, (tested with RTSP/RTMP streams)
- Playback speed adjustment (since v1.1.0)
- Smart request handling, debounces switch video requests to avoid rate limit errors (since v1.1.0)
- Local mode (toggleable syncing with other users within instance before uploading)
- Modulized screen, audio & UI architecture, support multiple instances
- Both on-screen & separated interfaces available
- Optional extra alternative URL input for supporting cross-platform users (since v1.3.0)
- (Almost) one-click to change interface colors
- Supports both legacy UI and TextMeshPro setup (since v1.0.32)
- Local pickupable & scaleable screen
- Wrist band (VR) / keyboard (desktop) resync button & volume controls
- Auto plays when local user steps into specific region
- Auto fades out background music when video is playing
- Dedicated component assigns random stream link per-instance/user (since v1.3.0)
- Custom shader with various display modes built-in (Stretch, Contain, Cover, Stereographic Video Source), can be configurated on material options
- Luminance adjustment for screens using built-in materials (sice v1.1.0)
- Localization system with auto language detection (English, Chinese, Japanese & Korean)
- Locked UI with [Udon Auth](https://xtl.booth.pm/items/3826907).
- Basic [Audio Link](https://github.com/llealloo/vrc-udon-audio-link) support, which will auto switch audio source when playing, also reports player state (playback progress, volume, loop, shuffle, etc.) on newer version (1.0.0+).
- Basic [LTCGI](https://ltcgi.dev/) integration, provided CustomRenderTexture for use.
- Bundled a modified version of [YTTL](https://65536.booth.pm/items/4588619) to display video title from known sources.
- Simple API for [your own udons] integration.
- Privacy first - We guarantee we do not include features that requires dedicated server to work; Also, features requires external resources are not opt-in by default.

## Demo
Please visit the [official demo world](https://vrchat.com/home/world/wrld_7239d09c-7b25-43a5-8ccd-502d986b016a)!

## Documentation
- [English Manual](https://xtlcdn.github.io/VizVid/docs/)
- [日本語マニュアル](https://xtlcdn.github.io/VizVid/docs/index_ja.html)
- [中文說明文件](https://xtlcdn.github.io/VizVid/docs/index_zh.html)
- [API References](https://xtlcdn.github.io/VizVid/api/Global.html)

## Installation
You may use following methods:

- Via VCC (Recommend):
  1. Ensure you have installed VRChat Creator Companion, if not, [download here](https://vrchat.com/download/vcc).
  2. Go to [my package listings landing page](https://xtlcdn.github.io/vpm/), click "Add to VCC" button under the banner and follow instructions.
  3. You can then go to "Manage Project" of your own world project, click on the "+" button to add the player component.
  4. Enjoy!
- Via Command Line:  
  Alternatively, instead of VCC, if you are an advanced geek like to use command line, you may use a tool called [`vrc-get`](https://github.com/vrc-get/vrc-get):
  ```powershell
  cd path/to/your/world/project/folder
  vrc-get repo add https://xtlcdn.github.io/vpm/index.json
  vrc-get install idv.jlchntoz.vvmw
  ```
- Via Booth: [Click here](https://xtl.booth.pm/items/5056077).
- Via GitHub Releases: [Click here](https://github.com/JLChnToZ/VVMW/releases/latest).

## Issues
For any issues, please contact me on [Discord server](https://discord.gg/fkDueQMbj8) or [file an issue on GitHub](https://github.com/JLChnToZ/VVMW/issues/new) if you believe there is a bug.

## Credits
- **Vistanz** ([@JLChnToZ](https://x.com/JLChnToZ)) - Programming
- **山の豚** ([@yama_buta](https://x.com/yama_buta)) - Art

## Special Thanks
- **LR163** / **Cross** - Early Stage Functionality & (Live Streaming) Latency Test
- **HsiaoTzuOWO** - UI & Implementation Test
- **Yan**-K - UI / UX Consultant
- **六森** - Advertisement Materials & Demo World
- **水鳥waterbird** - Naming & Japanese Documentation Proofreading
- **Kuriko** - Japanese Documentation
- **[All GitHub Contributors](https://github.com/JLChnToZ/VVMW/graphs/contributors)**

## License
[MIT](./Packages/idv.jlchntoz.vvmw/LICENSE)

***

Made with :heart: in :hong_kong: :taiwan:.