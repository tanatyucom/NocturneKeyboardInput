# Steam Text Input Bridge for SMT3HD

This directory contains a verified proof of concept and a reusable bridge for Steam's Big Picture gamepad text input in *Shin Megami Tensei III Nocturne HD Remaster*.

It uses the IL2CPP Steamworks wrappers already shipped with the game. It does not instantiate the game's incomplete `ImeKeyboard` path, call `NameSet()`, or modify any game data.

## Verified behavior

- Normal desktop Steam launch: unavailable state is rejected safely.
- Steam Big Picture launch: the text input dialog opens.
- Japanese kanji and full-width spaces are returned correctly.
- Repeated requests work.
- Cancellation is reported correctly.
- No callback exceptions or game-data side effects were observed.

## Integration

Add `Smt3SteamTextInputBridge.cs` to an SMT3HD MelonLoader mod and reference `Assembly-CSharp.dll`, `Assembly-CSharp-firstpass.dll`, `Il2Cppmscorlib.dll`, and `Il2CppInterop.Runtime.dll`.

```csharp
private readonly Smt3SteamTextInputBridge _bridge = new();

bool opened = _bridge.TryOpen(
    new SteamTextInputOptions
    {
        Description = "Enter a name",
        ExistingText = "",
        MaximumCharacters = 32,
        MultipleLines = false,
        Password = false
    },
    result => MelonLogger.Msg(result.Text),
    () => MelonLogger.Msg("Canceled"),
    error => MelonLogger.Error(error),
    out string failureReason);
```

Dispose the bridge when the owning mod is unloaded:

```csharp
public override void OnDeinitializeMelon()
{
    _bridge.Dispose();
}
```

Only one request per bridge instance may be active. Steam broadcasts the dismissed callback to every registered listener; the bridge ignores callbacks unless it currently owns an active request, so dialogs opened by the game or another mod are not consumed.

## Platform limitation

The bundled API is `ISteamUtils.ShowGamepadTextInput`. Steam documents this as a Big Picture gamepad text input feature. A normal desktop launch generally returns `false`; callers must treat that as an unavailable feature rather than an error.

## Portability

The bridge references the `Il2CppSteamworks` types bundled with SMT3HD. Other games may ship different wrapper versions or no managed wrapper. Porting requires a game-specific wrapper adapter or a native Steamworks implementation.

## License

MIT License. Keep the repository copyright notice and license text when copying or redistributing substantial portions.
