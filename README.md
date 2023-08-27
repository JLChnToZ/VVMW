# VizVid
Welcome! VizVid is a general-purpose video player wrapper for use in VRChat. It aims to cover many use cases, from watch-together video/live stream player in lounges, to large event venue for music performances, or even booths for exhibitions or showcases. Due to its target customers, it has a flexible architecture, just like a factory made electronic but with a easy to open back lid, make it easier to let users mess them around for their needs.

## Features
- Basic playback, seeking controls
- Pre-defined playlists & user queue list
- Quest (Android) client specific URLs (only available on pre-defined URLs, play lists and API)
- Low latency mode, (tested with RTSP/RTMP streams)
- Local mode (toggleable syncing with other users within instance before uploading)
- Modulized screen, audio & UI architecture, support multiple instances
- Both on-screen & separated interfaces available
- (Almost) one-click to change interface colors
- Local pickupable & scaleable screen
- Wrist band (VR) / keyboard (desktop) resync button & volume controls
- Custom shader with various display modes built-in (Stretch, Contain, Cover, Stereographic Video Source), can be configurated on material options
- Localization system with auto language detection (English, Traditional Chinese, Japanese & Korean)
- Locked UI with [Udon Auth](https://xtl.booth.pm/items/3826907).
- Basic [Audio Link](https://github.com/llealloo/vrc-udon-audio-link) support, which will auto switch audio source when playing.
- Basic [LTCGI](https://ltcgi.dev/) integration, provided CustomRenderTexture for use.
- Simple API for [your own udons] integration.

## Documentation
Please refer to [another readme](./Packages/idv.jlchntoz.vvmw/README.md) for details.

## Installation
This player component is still in heavy work in progress (especially for UI), so it is not recommend to use in any public world yet. But if you still want to try out, you may use following methods:

- Via GitHub Releases: [Click here](https://github.com/JLChnToZ/VVMW/releases/latest).
- Via Booth: Coming soon, will be available once everything is ready.
- Via VCC:
  1. Ensure you have installed VRChat Creator Companion, if not, [download here](https://vrchat.com/download/vcc).
  2. Go to [my package listings landing page](https://xtlcdn.github.io/vpm/), click "Add to VCC" button under the banner and follow instructions.
  3. You can then go to "Manage Project" of your own world project, click on the "+" button to add the player component.
  4. Enjoy!

## License
[MIT](./Packages/idv.jlchntoz.vvmw/LICENSE)