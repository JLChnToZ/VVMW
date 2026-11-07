---
name: workspace-udonsharp-conventions
description: >-
  Detailed usage guidance for the idv.jlchntoz.vrcw-foundation package's
  UdonSharp event dispatch, build-time binding and reference resolution,
  metadata, localization, URL generation and validation, inspector attributes,
  and Unity utilities. Use alongside unity-vrc-udon-sharp for Foundation API
  work; the UdonSharp skill governs SDK constraints and networking, and
  .editorconfig governs formatting.
license: MIT
metadata:
    author: workspace
    version: "1.0.0"
    tags: vrchat, udonsharp, foundation, resolver, event-binding, localization
---

# VRC Foundation UdonSharp Toolkit

## Scope and Compatibility

This guide covers only the `idv.jlchntoz.vrcw-foundation` package. Its manifest
declares a dependency on VRChat Worlds SDK 3.9.0 or newer within the 3.x line.
Check the installed package and SDK versions before using an API.

- Follow `unity-vrc-udon-sharp` for UdonSharp language/API constraints,
  networking, sync, ownership, and persistence.
- Follow `.editorconfig` for formatting and naming. In this workspace, C# uses
  four spaces, same-line opening braces, and the configured public Udon-event
  naming convention.
- Do not assume a Foundation attribute is a runtime API. Many attributes are
  interpreted by editor inspectors or scene-build preprocessors; those tools
  must be installed and active for the attribute to have an effect.
- Use Foundation APIs only where their documented contract fits. Avoid
  copying internal reflection or editor implementation details into
  UdonSharp runtime code.

## Event Wiring and Dispatch

### Bind Unity events during scene preprocessing

`[BindEvent]` connects a Unity `UnityEvent` to a Udon custom event through the
scene-build preprocessor. It creates a persistent string listener that invokes
`UdonBehaviour.SendCustomEvent`; it does not add a C# delegate listener at
runtime.

For a component referenced by a serialized field, put the attribute on that
field. The field's component type is used as the source type:

```csharp
[SerializeField, BindEvent(nameof(Button.onClick), nameof(_OnButtonClick))]
Button button;

public void _OnButtonClick() {
}
```

For a source component attached to the same GameObject, put the attribute on
the UdonSharp behaviour class and specify the component type:

```csharp
[RequireComponent(typeof(Button))]
[BindEvent(typeof(Button), nameof(Button.onClick), nameof(_OnButtonClick))]
public class ButtonHandler : UdonSharpBehaviour {
    public void _OnButtonClick() { }
}
```

Use `nameof` for component event and method names. For arrays of event-source
components, the destination string supports composite placeholders: `{0}` is
the array index and `{1}` is the source object's name. The preprocessor also
maps known Udon event names to their exported Udon symbol form. Prefer a public,
parameterless destination method with a leading underscore. The editor
validation warns about missing methods, parameters, non-public entrypoints, and
destinations without the underscore. Do not apply the underscore convention
to SDK lifecycle overrides.

### Use `UdonSharpEventSender` for named or broadcast callbacks

Derive the sender behaviour from `UdonSharpEventSender` when it needs to notify
registered UdonSharp behaviours. Call its protected `SendEvent(string)` from
the sender; receivers implement public custom-event methods. This is a
parameterless event mechanism, not a typed delegate/event bus.

On a receiver, serialize a reference to the sender and annotate it with
`[BindUdonSharpEvent]`. An empty argument list registers the receiver for all
events; pass event names to register only for those names:

```csharp
[SerializeField, BindUdonSharpEvent(nameof(_OnChanged))]
SomeEventSender sender;
```

The field is the sender reference; the preprocessor registers the behaviour
containing that field as the listener. `SendEvent` dispatches a named event to
the filtered set and to any broadcast targets. For runtime registration, call
`_AddListener(receiver)` or `_AddListener(receiver, eventName)` on the sender;
the implementation avoids duplicate registrations. Do not build a parallel
listener list when this sender abstraction already fits.

### Use `UdonEventUtils` only from editor-side Unity code

`UdonEventUtils.GetSendEventAction()` returns a cached `UnityAction<string>`
for a `UdonBehaviour`, `UdonSharpBehaviour`, or compatible `MonoBehaviour`.
`AddUdonEventListener()` adds a persistent UnityEvent listener in Edit mode.
At runtime in the editor it only logs a warning; runtime binding is not
supported. Keep this reflection/delegate utility out of UdonSharp code and do
not rely on it to mutate event wiring during play.

Source contracts: `Runtime/Attributes Markers/UnityEventBinder.cs`,
`Runtime/UdonSharpEventSender.cs`, and `Runtime/UdonEventUtils.cs`.

## Reference Resolution and Build Metadata

### Resolve scene references with `[Resolve]`

`[Resolve]` is processed while the scene is being preprocessed for build. It
fills a serialized field before the world is built; it is not a runtime search
and does not continuously update when the hierarchy changes. The field type
supplies the desired result type in the common case:

```csharp
[SerializeField, Resolve(".")]
Animator animator;

[SerializeField, Resolve("/")]
Transform sceneRoot;
```

Path syntax accepted by `ResolveAttribute` and `Resolver.WithPath`:

- `/` separates hierarchy names; a leading `/` starts at the hierarchy root.
- `..` selects the parent; `..*` traverses successive parents.
- `*` selects a child; `**` traverses child descendants.
- `.` or `this` keeps the current object. Dot-separated names resolve object
  properties/fields.
- `#` changes between hierarchy and property resolution. For example,
  `/Root/Panel#transform.parent` resolves a hierarchy object, then follows its
  `transform.parent` members. Escape special path characters with `\`.
- `WithType<T>()` or the attribute's `SourceType` can insert an explicit type
  lookup. `SourceType` is useful when composing multiple `[Resolve]`
  attributes; each attribute continues from the previous result.
- A scalar field receives the first matching result. An array field receives
  all matching results. `NullOnly` defaults to true, preserving an already
  assigned scalar or non-empty array; set `NullOnly = false` to replace it.
  `HideInInspectorIfResolvable` controls the corresponding Inspector behavior.

Use `nameof` when a path contains a C# field/property name. Validate the
resolved type and hierarchy assumptions in the scene. For custom resolution,
`Resolver.CustomResolveProvider` can register a `TryResolveMemberDelegate`; a
provider returns true and an object when it handles a member, otherwise the
default property/field resolution continues. Register and unregister custom
providers with a controlled lifetime.

`Resolver.Create()` and `DataResolver.ToVRCToken()` are general C# tooling
helpers, not a promise of Udon compatibility. The resolver implementation uses
reflection and collections; use it in editor/build tooling unless the exact
API has been verified against the active UdonSharp compiler. `DataResolver`
converts supported CLR values, dictionaries, enumerables, Unity objects,
`VRCUrl`, and serializable instance fields into VRC `DataToken` values; it
recursively converts collections and skips non-serialized fields.

### Inject scene metadata with `[UdonMeta]`

`[UdonMeta]` marks a field for build-time injection from the current scene or
Udon behaviour configuration. Match the field type to the selected metadata:

| Metadata | Field type | Value |
|---|---|---|
| `NetworkID` | `int` | Scene network ID of the attached GameObject |
| `NetworkSyncModeNone`, `NetworkSyncModeContinuous`, `NetworkSyncModeManual` | `bool` | Whether the behaviour uses that sync mode |
| `NetworkSyncMode` | `int` | Numeric `Networking.SyncType` value |
| `BuiltTimeStamp` | `DateTime` or `string` | Build timestamp |
| `WorldBlueprintID` | `string` | Current world's blueprint ID |

Example: `[SerializeField, UdonMeta(UdonMetaAttributeType.NetworkSyncModeManual)]
bool isManualSync;`. Network IDs are scene/build data and are not reliable
identifiers for runtime-cloned objects. These fields are populated during
preprocessing, not at runtime.

Sources: `Runtime/Resolver/ResolveAttribute.cs`,
`Runtime/Resolver/Resolver.cs`, `Runtime/Resolver/Resolver.ResolveResults.cs`,
`Runtime/Resolver/UdonDataResolver.cs`,
`Runtime/Attributes Markers/UdonMetaAttribute.cs`,
`Editor/Preprocessors/ResolvePreprocessor.cs`, and
`Editor/Preprocessors/UdonMetaInjector.cs`.

## Localization

`LanguageManager` loads language JSON supplied as `TextAsset[]` and/or inline
JSON, exposes `LanguageKeys`, `LanguageNames`, `LanguageKey`, and
`GetLocale(key)`, and stores the selected language through PlayerData when a
save key is configured. If a key is absent, `GetLocale` returns the key itself.
The editor preprocessing merge understands a top-level language-key object
with translation keys and these optional metadata keys:

```json
{
  "EN": {
    "_name": "English",
    "_vrclang": "English",
    "_timezone": ["Etc/UTC"],
    "_fallback": "",
    "menu.play": "Play"
  }
}
```

`_name` is the display name, `_vrclang` matches VRChat's UI language,
`_timezone` can be a string or array of strings, and `_fallback` names another
language to consult for missing entries. Keep translation keys consistent
across locales; the first value found for a key is used as the default fallback
when the build merges language sources.

Use `[LocalizedLabel]`, `[LocalizedEnum]`, and `[LocalizedHeader]` on Inspector
fields/enums/sections. `LocalizedLabel.Key` overrides the default key, which is
derived from namespace, type, and field; `TooltipKey` overrides the default
`{Key}:tooltip` key.

Attach `LanguageReceiver` beside a legacy `Text` or TMP text component. Set
`key` explicitly, or leave it blank to use the existing text as the key during
preprocessing. The receiver replaces that text with the localized value. Set
`Args` to format placeholders in the localized string. It can use the
build-bound manager listener, or `dynamicBinding` to register at runtime.

Use `LanguageSetter` for a language-selection UI. It expects a Toggle entry
template containing `Text` or `TextMeshProUGUI`; it clones one entry per
language and updates the manager's `LanguageKey`. Its optional ScrollRect and
dropdown references are for the packaged scrolling/dropdown behavior.

Sources: `Runtime/I18N/LanguageManager.cs`,
`Runtime/I18N/LanguageReceiver.cs`, `Runtime/I18N/LanguageSetter.cs`, and
`Runtime/I18N/LocalizedLabelAttribute.cs`.

## URL and Inspector Tooling

### Generate finite URL sets

`[GeneratedUrls]` is evaluated by the scene-build preprocessor. The target
field must be `string[]`, `VRCUrl[]`, or `DataDictionary`. Provide `Pattern`
directly, or name a string field through `PatternSourceProperty`; use `Limit`
to cap generation. For a `DataDictionary`, generated strings are converted to
`VRCUrl` values and the first regex capture group is used as each key when it
matches.

The pattern uses Foundation's finite Regex2Pattern subset, not full .NET
regular expressions: literals, character classes/ranges, `\d`, `\w`, groups,
alternation, optional `?`, and bounded repetition such as `{3}` or `{2,5}`.
`*` and `+` are not repetition quantifiers in this parser; they are not
expanded as regular-expression wildcards. Keep the generated set small and
use `Limit` for a hard cap.

`[GeneratedUrlMapper]` builds a `DataDictionary` index from a source
`string[]` or `VRCUrl[]` field named by `TargetUrlArray`. Set `RegexPattern` or
`RegexPatternSourceProperty`; capture group 1 becomes the key and the matching
array index becomes the value. `IndexOnly` affects the empty-URL entry, and
`ClearSourceArray` clears the source array after mapping. Ensure the regex has
a capture group and verify every source URL matches it.

### Check URLs in the Inspector

`[TrustUrlCheck(TrustedUrlTypes.X)]` belongs on a `VRCUrl` field. The custom
Inspector drawer checks URL syntax, supported scheme, and VRChat's configured
trusted host list for the selected use (`UnityVideo`, AVPro platform, image,
or string). This is editor validation/authoring feedback, not a runtime
security boundary or a replacement for runtime URL policy.

### Use authoring-only attributes deliberately

- `[Locatable]` adds an Inspector resolve control and can instantiate a
  specified prefab when no match is found. `ResolveOnBuild` is documented as
  not yet implemented; do not depend on it.
- `[ToggleAndNumberField]` is for an `int` where a non-negative value means
  enabled and `-1` means disabled; `DisabledText` labels the off state.
- `[TMProMigratable]` marks a `Text` field/class for the Foundation editor's
  migration workflow. It has no runtime localization effect.
- `[EditorOnly]` marks a MonoBehaviour type whose instances are removed from
  the scene during build. Do not put it on runtime behavior.
- `[DeclareDefine]` / `[DeclareUndefine]` on an assembly request an editor
  scripting define to be added/removed for the active build target group.
  Use these only for package-level compatibility symbols; do not use them as
  runtime feature flags.

## Runtime and Editor Utilities

- `TransformUtils.GetGlobalAspect`, `NormalizeDimensions`, and
  `SynchronizeColliderWithRect` help preserve RectTransform proportions and
  synchronize a BoxCollider. `IterateAllComponents`,
  `FindClosestComponentInHierarchy`, and `IsAvailableOnRuntime` are excluded
  from Udon compilation; use them in editor/build code only.
- `CastHelper.TryCast` uses reflection, conversion operators, and
  `Convert.ChangeType`; use it for general/editor tooling, not Udon runtime
  casts.
- `PooledObjectExtensions.Get` wraps Unity collection pools and returns the
  collection to the pool when its `PooledObject` is disposed. It is excluded
  from Udon compilation. Keep the pooled collection inside a `using` scope and
  do not return a reference to it after disposal.
- `PrefabEnzyme` is an editor helper that unpacks prefab instances and removes
  itself; it is not a runtime component.
- `VRCCanvasConfigurator` is an editor-only authoring component for normalizing
  a RectTransform and optionally matching its BoxCollider. Its `EditorOnly`
  marker removes it for builds.
- `IUdonAdaptor` lets an editor-side MonoBehaviour expose its target
  `UdonBehaviour` or `UdonSharpBehaviour` to Foundation preprocessors. Use it
  only when editor-side authoring data must be transferred to a Udon program.
- `ISingleton<T>` is a scene-build merge contract for duplicate
  UdonSharpBehaviour components of one type. The preprocessor calls `Merge`
  on the retained instance, reroutes component references, then removes
  duplicates. Implement its interface members outside the
  `COMPILER_UDONSHARP` compilation path. Use it only when duplicate instances
  are truly mergeable and implement deterministic merge behavior.
- `IPrioritizedPreProcessor`, `ISelfPreProcess`, and `IPreprocessor` support
  custom editor/build preprocessing. Lower `Priority` runs first. The manager
  discovers preprocessors and catches/logs exceptions; validate your own
  preprocessor output rather than assuming an exception aborts the build.

## Adoption and Validation Checklist

Before using a Foundation feature:

1. Check whether it runs in Udon at runtime, in the Inspector, or during scene
   build. Keep editor-only reflection, collections, and UnityEditor APIs out
   of UdonSharp runtime paths.
2. Confirm the package version, SDK version, attribute target, field type, and
   required preprocessor. Read the attribute contract and matching processor
   before adding custom behavior around it.
3. Keep event destinations public, parameterless, and underscore-prefixed
   unless a specific SDK callback signature requires otherwise.
4. Check hierarchy/path assumptions and generated output in the actual scene;
   resolve and metadata attributes do not continuously react to runtime edits.
5. Run ordinary C# compilation and UdonSharp compilation after changing code
   shared across editor and Udon compilation paths. For scene preprocessors,
   build a representative scene and inspect warnings/errors and serialized
   output.

## Source Map

- Events: `Runtime/Attributes Markers/UnityEventBinder.cs`,
  `Runtime/UdonSharpEventSender.cs`, `Runtime/UdonEventUtils.cs`,
  `Editor/Preprocessors/BindEventPreprocessor.cs`,
  `Editor/Preprocessors/BindUdonSharpEventPreprocessor.cs`.
- Resolution and metadata: `Runtime/Resolver/`,
  `Runtime/Attributes Markers/ResolveAttribute.cs`,
  `Runtime/Attributes Markers/UdonMetaAttribute.cs`,
  `Editor/Preprocessors/ResolvePreprocessor.cs`,
  `Editor/Preprocessors/UdonMetaInjector.cs`.
- Localization: `Runtime/I18N/` and `Editor/I18N/`.
- URL authoring: `Runtime/Regex2Pattern/`,
  `Runtime/Attributes Markers/GeneratedUrlsAttribute.cs`,
  `Runtime/Attributes Markers/TrustUrlCheckAttribute.cs`,
  `Editor/Preprocessors/GeneratedUrlsPreprocessor.cs`,
  `Editor/TrustedUrlUtils.cs`.
- Scene/build utilities: `Runtime/TransformUtils.cs`,
  `Runtime/CastHelper.cs`, `Runtime/PooledObjectExtensions.cs`,
  `Runtime/PrefabEnzyme.cs`, `Runtime/IUdonAdaptor.cs`,
  `Editor/Preprocessors/BuildPreprocessorManager.cs`,
  `Editor/Preprocessors/SingletonBehaviourCombiner.cs`.