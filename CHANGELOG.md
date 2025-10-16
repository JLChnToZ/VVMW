# CHANGELOG

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
## 1.4.10 - 2025-10-16
### Fixed
- Screen configurator not work well when used alone and core is a prefab instance
- Progress bar "flashing" effects disappeared since last fix of status text

### Changed
- Removed emoji fallback font for smaller build size
- Enhanced video screen blitting logic

## 1.4.9 - 2025-09-27
### Fixed
- Status text not working in play mode if progress bar isn't present

### Added
- Getting Started Guide (Simplified Documentation)

## 1.4.9-beta.2 - 2025-09-23
### Added
- Prefab files missed in previous release

### Fixed
- Compiler in previous release

## 1.4.9-beta.1 - 2025-09-23
### Added
- 2 new presets for exhibition usage (local only, arbitrary URL input is disabled, auto play on near)
- 2 new audio source presets (separaed stereo, 5.1 surround)

### Fixed
- Default playlist dropdown bugged when unchecking queuelist
- Fragmented aspect ratio calculation result in some cases

### Changed
- Auto Play On Near now also works without frontend handler (playlist/queue list/history features)
- Light volumes will by default set dynamic to true if the screen is under pickupable object when created
- Reworked inspector UI

## 1.4.8 - 2025-09-02
### Added
- Option to auto apply color config on build
- Option to auto detect aspect ratio on shader (PC only)

## 1.4.7 - 2025-07-24
### Added
- Instant VRC Light Volumes V2 setup and support

### Fixed
- Enabling lock state while playing won't lock progress bar ([#71](https://github.com/JLChnToZ/VVMW/issues/71))
- Audio controller unable to function

## 1.4.6 - 2025-07-05
Nothing changed since last beta version.

## 1.4.6-beta.2 - 2025-07-02
### Added
- Selective rendering on all video shaders for mirrors and/or VRC cameras.

## 1.4.6-beta.1 - 2025-06-27
### Fixed
- Unable to compile on older VRCSDK (despite updating is recommended).

### Added
- Add Audio Controller allows external scripts to control individual audio volumes without interference with video player volume control.

### Changed
- Auto Play on Near now partially works when synchronization enabled, alongside with some new options.

## 1.4.5 - 2025-06-01
### Added
- Support for half stereo mode
- A proper changelog

### Changed
- Removed hard dependency to VPM Resolver ([#66](https://github.com/JLChnToZ/VVMW/issues/66))

## 1.4.5-beta.3 - 2025-05-28
### Changed
- Adjusted enable/disable logic in light volume adaptor

## 1.4.5-beta.2 - 2025-05-27
### Added
- Dedicated updater for [VRC Light Volumes](https://github.com/REDSIM/VRCLightVolumes/)
- Mipmap support ([#41](https://github.com/JLChnToZ/VVMW/issues/41))

## 1.4.5-beta.1 - 2025-04-24
### Added
- Speed adjustment on non-supported modes (UI only, no actual effects)
- `InputFilterBase` class for filtering/replacing user input URLs before loading/enqueueing
  - Abstract class without logic; world creators must implement custom filtering logic
  - Limited to pre-entered or editor-generated URLs due to VRChat security restrictions

## 1.4.4 - 2025-03-28
### Fixed
- Issue preventing user input URL playback when "auto play on idle" is enabled during playlist playback

## 1.4.3 - 2025-03-26
### Changed
- Ensured proper dependency constraints using semantic versioning ([#62](https://github.com/JLChnToZ/VVMW/issues/62))

## 1.4.2 - 2025-03-20
### Fixed
- Visual bug on "Next On" label and tooltip for enqueue options

### Changed
- Internal refactoring (no functional impact)

## 1.4.1 - 2025-03-09
### Improved
- Enhanced synchronization handling for buffering under poor network conditions or high-bitrate videos
  - Reduced instances of player stalling and potential empty screen issues at playback start

## 1.4.0 - 2025-02-16
### Added
- Split confirm URL button into two: interrupt current playback or enqueue
- Indicator for enqueued URLs
- Configurable display order for playlist/queue lists ([#14](https://github.com/JLChnToZ/VVMW/issues/14))
- Screen configurator component for easier manual screen setup
- Support for backward-placed pickup screens with updated shader styling

### Changed
- Rearranged UI properties in inspector for future compatibility
- Breaking change: UI handler update requires re-setting up unpacked video player UI prefabs

## 1.4.0-beta.4 - 2025-02-02
### Fixed
- Bug preventing spawned playlist entries from aligning in VRC client

## 1.4.0-beta.3 - 2025-02-02
### Added
- Full setup flow for ascending display order playlists and queue lists
- Drafted documentation for new features

## 1.4.0-beta.2 - 2025-01-31
### Fixed
- Compiler error

## 1.4.0-beta.1 - 2025-01-31
### Added
- Split confirm URL button into two: interrupt or enqueue
- Enqueued URL indicator
- Support for dual-sided queue list/playlist (program-only, no quick setup flow) ([#14](https://github.com/JLChnToZ/VVMW/issues/14))
- Enhanced backward-placed pickup screen support with shader styling
- Screen configurator component for manual screen setup
- Support for unpublished gimmick integration

### Changed
- Rearranged UI properties in inspector (breaks existing UI if unpacked)

## 1.3.5 - 2024-12-28
### Added
- Support for "repeat all" in queue list
- Auto play playlist on idle option

### Fixed
- Compilation issue in non-Windows Unity editor ([#55](https://github.com/JLChnToZ/VVMW/issues/55))

## 1.3.5-beta.1 - 2024-12-26
### Added
- Support for "repeat all" in queue list
- Auto play playlist on idle option

### Fixed
- Compilation issue in non-Windows Unity editor ([#55](https://github.com/JLChnToZ/VVMW/issues/55))

## 1.3.4 - 2024-12-01
### Added
- Gizmos showing screen/audio targets when selecting core
- Updated documentation links

### Changed
- YTTL update

## 1.3.4-beta.2 - 2024-11-29
### Fixed
- Minor bug preventing YTTL from loading video info when definition is not loaded

## 1.3.4-beta.1 - 2024-11-28
### Added
- Gizmos showing screen/audio targets when selecting core

### Changed
- YTTL update

## 1.3.3 - 2024-11-23
### Fixed
- Compilation failure when AudioLink is absent ([#51](https://github.com/JLChnToZ/VVMW/issues/51))

## 1.3.2 - 2024-11-23
### Added
- One-click setup support for LTCGI integration
- `<summary>` tags to all public APIs for better documentation

### Fixed
- Reference error on first-time platform switching ([#50](https://github.com/JLChnToZ/VVMW/issues/50))

### Changed
- Refactored to hide internal fields/methods (breaking change for advanced integrations)

## 1.3.2-beta.2 - 2024-11-21
### Fixed
- Reference error on first-time platform switching ([#50](https://github.com/JLChnToZ/VVMW/issues/50))

### Changed
- Updated localized documentation

## 1.3.2-beta.1 - 2024-11-19
### Added
- One-click setup support for LTCGI integration

## 1.3.1 - 2024-11-17
### Fixed
- Missing reference when AudioLink is unavailable ([#49](https://github.com/JLChnToZ/VVMW/issues/49))

## 1.3.0 - 2024-11-17
### Added
- Player persistence for saving/loading overlay control and volume slider states
- Stream key assigner for per-individual or per-instance unique stream keys
- Dual-input field UI for ad-hoc PC and Quest streaming URLs
- Simplified Chinese translation for new features ([#48](https://github.com/JLChnToZ/VVMW/issues/48))

### Changed
- Split large component source code into smaller partial files

## 1.3.0-beta.5 - 2024-11-15
### Fixed
- Compatibility issues with non-beta VRChat clients

## 1.3.0-beta.4 - 2024-11-15
### Added
- Player persistence for overlay control and volume slider states
- Alpha clip option for unlit shader

## 1.3.0-beta.3 - 2024-11-09
### Changed
- Split large component source code into smaller partial files
- Updated documentation
- Updated Simplified Chinese localization ([#48](https://github.com/JLChnToZ/VVMW/issues/48))

## 1.3.0-beta.2 - 2024-11-07
### Added
- Enhanced editor flow for stream key assigner
- Documentation for new features (English only)

### Fixed
- Time code not displaying in dual-input UI variant

## 1.3.0-beta.1 - 2024-11-05
### Added
- Stream key assigner for per-individual or per-instance unique stream keys
- Dual-input field UI for ad-hoc PC and Quest streaming URLs

## 1.2.1 - 2024-10-01
### Fixed
- Re-added missing language selection dropdown
- Fixed audio source setup button

### Changed
- Cleaned up assets and defined symbols moved to external package

## 1.2.0 - 2024-09-18
### Changed
- Moved non-core VizVid logic and scripts to [vrcw-foundation](https://github.com/JLChnToZ/vrcw-foundation)

### Fixed
- Inspector custom player properties display bug on Overlay Control

## 1.1.9 - 2024-09-14
### Changed
- Progress bar on overlay UI now "breathes" when loading and dims when not loaded or in error
- AudioLink integration auto re-registers audio source if player was disabled during playback
- Player switches to corresponding language when VRChat system language changes

## 1.1.8 - 2024-09-08
### Changed
- Reduced trigger area for video screen overlay UI when inactive

### Fixed
- Self-update failure in projects using non-official package managers (e.g., VRC-Get/ALCOM)

## 1.1.7 - 2024-09-03
### Fixed
- Bug preventing auto-detection of image mode for URLs with query strings

### Changed
- Updated Yama BUTA's icon in credits

## 1.1.6 - 2024-08-12
### Fixed
- Incorrect/missing language keys
- Missed text field in player backend select during TMPro migration

### Changed
- Added asterisks (*) to unsaved playlist editors with icons and keyboard shortcuts
- Internal refactoring for future features

## 1.1.5 - 2024-08-09
### Fixed
- Language selection not working

## 1.1.4 - 2024-08-09
### Added
- Localized editor UI
- Support for iOS build worlds (treats iOS as Quest/Android for playlist URLs)

### Fixed
- Quest clients unable to synchronize when clicking playlist entries ([#46](https://github.com/JLChnToZ/VVMW/issues/46))
- Minor bug fixes on speed adjustments with special speakers and AudioLink setup

### Changed
- Internal code refactoring

## 1.1.3 - 2024-08-01
### Fixed
- Build script with faulty logic breaking other U# scripts

## 1.1.2 - 2024-07-31
### Fixed
- AudioLink integration failure due to U# compiler changes

## 1.1.1 - 2024-07-30
### Fixed
- Luminance icon not affected by color config options

## 1.1.0 - 2024-07-30
### Added
- Playback speed adjustment
- Luminance control for screens using bundled material/shaders
- Button to reverse playlist order in editor ([#42](https://github.com/JLChnToZ/VVMW/issues/42))
- Upright button for pickup screen ([#44](https://github.com/JLChnToZ/VVMW/issues/44))
- Smart request handling to reduce rate limit errors

### Fixed
- Minor bugs in installer/migrator/config modifiers

### Changed
- Redesigned pickup screen buttons
- Moved YTTL definition file to a trusted, stable URL

### Notes
- UI updates may require manual action for color configs and TextMeshPro migration

## 1.0.41 - 2024-06-17
### Added
- Button to reverse playlist order ([#42](https://github.com/JLChnToZ/VVMW/issues/42))

### Fixed
- Japanese README translation ([#43](https://github.com/JLChnToZ/VVMW/issues/43))

## 1.0.40 - 2024-06-05
### Changed
- Enhanced time synchronization accuracy
- Reduced AudioLink time reporting frequency

## 1.0.39 - 2024-05-27
### Added
- Option to seed RNG before shuffle ([#40](https://github.com/JLChnToZ/VVMW/issues/40))

### Fixed
- Time line label bug on video error

## 1.0.37 - 2024-05-12
### Added
- Image Viewer Module for viewing PNG/JPG images from the internet
- Support for toggling visibility in VR/PC modes and mirrors

### Fixed
- Z-fighting in video player select UI ([#39](https://github.com/JLChnToZ/VVMW/issues/39))
- Several typos in README

## 1.0.36 - 2024-05-05
### Fixed
- Import errors for Unity 2019 / no VCC (#36, #37)
- Font resolution bug in TMPro migration

### Changed
- Enhanced TMPro migration flow for use outside VizVid

## 1.0.34 - 2024-04-28
### Changed
- Tweaked TMPro font asset for wider character range (European Glyphs)
- Adjusted playback position timing window for low-end machines/weak networks (#31, #29)
- Updated Japanese wordings (#32, #33, #34)

### Fixed
- Internal TMPro migration logic

## 1.0.33 - 2024-04-24
### Changed
- Enhanced URL display for non-ASCII domain names (Punycode)
- Internal code tidy up and refactoring

### Fixed
- Title display/history sync on playing input URL

## 1.0.32 - 2024-04-23
### Added
- Option to migrate legacy UI text to TextMeshPro
- Playback history
- Double-click resync button to trigger owner state synchronization

### Fixed
- Title display issue after playing custom URL without YTTL
- Workaround for VRChat open beta build (1444)

### Changed
- Enhanced late joiner flow to reduce glitches
- Minor UI and visual tweaks

## 1.0.31 - 2024-04-10
### Fixed
- Empty unremovable entry in queue list
- Clarified URL input field

## 1.0.30 - 2024-03-17
### Added
- Display of user adding URL in queue list ([#27](https://github.com/JLChnToZ/VVMW/issues/27))

### Fixed
- Bugged behavior when queue list is disabled

## 1.0.28 - 2024-02-23
### Fixed
- Language detection failure due to VRChat API update ([#24](https://github.com/JLChnToZ/VVMW/issues/24))
- Missing using statement for non-VCC projects ([#26](https://github.com/JLChnToZ/VVMW/issues/26))

## 1.0.27 - 2024-02-18
### Added
- Support for importing playlists from VideoTXL

### Fixed
- Blurry text in separated controls

### Changed
- Pickupable screen keeps distance when picked up
- Updated Simplified Chinese wordings ([#25](https://github.com/JLChnToZ/VVMW/issues/25))
- Updated terminology (Play list -> playlist, video player wrapper -> frontend)

## 1.0.26 - 2024-01-31
### Fixed
- Upside-down default texture with AVPro
- Screen retention after other users stop playback post-pause

### Changed
- Optimized workaround and initialization flow

## 1.0.25 - 2024-01-30
### Fixed
- Auto play crash

### Changed
- Optimized migration flow to VCC version

## 1.0.24 - 2024-01-28
### Added
- Workaround for AVPro screen flickering using extra render texture
- Auto play delay time option to avoid rate limits

### Changed
- Enabled workaround by default with option to disable

## 1.0.23 - 2023-12-27
### Added
- Documentation links in inspector via help button

### Fixed
- Default material emission intensity

### Changed
- Minor refactoring

## 1.0.22 - 2023-12-10
### Added
- Realtime GI support for video surface shader and core
- BGM control component
- Auto play on near component

### Changed
- Updated to Unity 2022 (backward compatible with 2019)
- Minor tweaks in inspector UI for AVPro flags
- Fixed playlist import error ([#22](https://github.com/JLChnToZ/VVMW/issues/22))

## 1.0.21 - 2023-11-25
### Fixed
- Minor bugs in playlist editor

### Changed
- Hides "default URL" when playlist is active

## 1.0.18 - 2023-11-20
### Fixed
- Language manager assignment to non-empty UdonBehaviour fields on build
- Minor UI and build enhancements

## 1.0.17 - 2023-11-19
### Added
- URL field validation for untrusted/invalid input

### Fixed
- Minor bugs and refactoring

## 1.0.16 - 2023-11-16
### Fixed
- Random video player stoppage per build

## 1.0.15 - 2023-11-15
### Fixed
- Overlay control functionality

### Changed
- Implemented self-update flow for VCC installs

## 1.0.13 - 2023-11-14
### Fixed
- UI functionality issue ([#19](https://github.com/JLChnToZ/VVMW/issues/19))

## 1.0.12 - 2023-11-11
### Added
- Right-click menu for adding player/control prefabs
- Support for importing USharpVideo playlists

### Fixed
- Video texture flipping on Android/Quest
- Pooled list view event spamming

### Changed
- Improved install/config procedures
- Wired "inter-udon" callbacks at build time
- Enhanced inspector editors

## 1.0.11 - 2023-11-04
### Added
- PC mode keyboard shortcuts for overlay control
- Language editor and mechanism for locale management
- Object picker for "+" buttons in inspectors
- Support for importing USharpVideo playlists

### Fixed
- Minor glitches in AudioLink playback state

### Changed
- Enhanced "Find" button to instantiate prefabs if components missing

## 1.0.10 - 2023-10-17
### Added
- Scroll bar for playlists ([#11](https://github.com/JLChnToZ/VVMW/issues/11))
- Indicator text for URL confirmation ([#13](https://github.com/JLChnToZ/VVMW/issues/13))

### Fixed
- Title display for author-only without YTTL ([#9](https://github.com/JLChnToZ/VVMW/issues/9))
- Private/deleted video fetching in YouTube playlists ([#15](https://github.com/JLChnToZ/VVMW/issues/15))

## 1.0.9 - 2023-10-07
### Fixed
- Title information update from remote

## 1.0.8 - 2023-10-07
### Added
- Custom shader texture property selection in setup UI
- AudioLink V1 playback state reporting
- Simplified Chinese language pack
- YTTL integration for video title display
- Stop button visibility during video loading

### Fixed
- Queue list synchronization ([#7](https://github.com/JLChnToZ/VVMW/issues/7))
- Behavior after loading failed videos

### Changed
- Updated language detection to use VRChat UI language
- Enhanced playlist import for third-party players

## 1.0.6 - 2023-09-16
### Fixed
- Unlock/lock flow issues

### Changed
- Improved playlist/queue list performance for large entries

### Notes
- Layout errors may occur; revert Rect Transform or re-add player to scene

## 1.0.5 - 2023-09-10
### Changed
- Playlists auto-switch on playback change

## 1.0.4 - 2023-09-09
### Fixed
- Playlist update when hidden in overlay UI

## 1.0.3 - 2023-09-08
### Fixed
- Glitches in playlist reordering and merging

### Changed
- Added fetch title button to playlist editor

## 1.0.2 - 2023-09-07
### Changed
- Enhanced playlist editor with YouTube and third-party player import/export

## 1.0.1 - 2023-09-04
### Fixed
- Misspell in documentation

### Changed
- Removed "Repeat All" function for queue list

## 1.0.0 - 2023-09-03
Initial public release
