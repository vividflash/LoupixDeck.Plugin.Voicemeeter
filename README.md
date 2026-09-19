# LoupixDeck Voicemeeter plugin

A [LoupixDeck](https://github.com/RadiatorTwo/LoupixDeck) plugin that controls Voicemeeter (Standard, Banana and Potato) from a Loupedeck device. It is a port of XeroxDev's [Loupedeck VoiceMeeter plugin](https://github.com/XeroxDev/Loupedeck-plugin-VoiceMeeter).

Requirements: Windows and Voicemeeter installed (the plugin loads `VoicemeeterRemote64.dll` from the Voicemeeter folder). Voicemeeter may start before or after LoupixDeck.

## Commands

Strips (`<strip>`) are numbered from 1 across the whole edition (hardware inputs first, then virtual inputs) — the same number for every strip command, including the hardware/virtual-only ones. Buses (`<bus>`) take a number or a name (`A1`, `B2`).

"Hardware input" / "virtual input" below is an applicability restriction, not a separate numbering: e.g. on Potato (5 hardware + 3 virtual), `Voicemeeter.StripEQGain1(6)` addresses the first virtual strip. Point the command at a strip of the wrong kind and it draws "n/a" and does nothing.

### Toggles

All of these show the live on/off state on a touch button.

| Command | Channel | Potato only |
|---|---|---|
| `Voicemeeter.StripMute(<strip>)` | Strip | |
| `Voicemeeter.BusMute(<bus>)` | Bus | |
| `Voicemeeter.StripSolo(<strip>)` | Strip | |
| `Voicemeeter.StripA(<strip>,<n>)` | Strip, assigns to physical bus A1..A5 (count per edition) | |
| `Voicemeeter.StripB(<strip>,<n>)` | Strip, assigns to virtual bus B1..B3 (count per edition) | |
| `Voicemeeter.StripMono(<strip>)` | Hardware input | |
| `Voicemeeter.StripPostReverb(<strip>)` | Hardware input | yes |
| `Voicemeeter.StripPostDelay(<strip>)` | Hardware input | yes |
| `Voicemeeter.StripPostFx1(<strip>)` | Hardware input | yes |
| `Voicemeeter.StripPostFx2(<strip>)` | Hardware input | yes |
| `Voicemeeter.BusMono(<bus>)` | Bus | |
| `Voicemeeter.BusEQ(<bus>)` | Bus | |
| `Voicemeeter.BusSel(<bus>)` | Bus | |

### Knobs

`...Down(<channel>,<step>)` / `...Up(<channel>,<step>)` change the value by step (default below); `...Reset(<channel>)` sets it to the reset value and, on a touch button, shows the live value.

| Command family | Channel | Range (step, reset) | Potato only |
|---|---|---|---|
| `Voicemeeter.StripGainDown/Up/Reset` | Strip | -60 to +12 dB (step 1, reset 0) | |
| `Voicemeeter.BusGainDown/Up/Reset` | Bus | -60 to +12 dB (step 1, reset 0) | |
| `Voicemeeter.StripCompDown/Up/Reset` | Hardware input | 0 to 10 (step 1, reset 0) | |
| `Voicemeeter.StripGateDown/Up/Reset` | Hardware input | 0 to 10 (step 1, reset 0) | |
| `Voicemeeter.StripReverbDown/Up/Reset` | Hardware input | 0 to 10 (step 1, reset 0) | yes |
| `Voicemeeter.StripDelayDown/Up/Reset` | Hardware input | 0 to 10 (step 1, reset 0) | yes |
| `Voicemeeter.StripFx1Down/Up/Reset` | Hardware input | 0 to 10 (step 1, reset 0) | yes |
| `Voicemeeter.StripFx2Down/Up/Reset` | Hardware input | 0 to 10 (step 1, reset 0) | yes |
| `Voicemeeter.StripPanXDown/Up/Reset` | Strip | -0.5 to +0.5 (step 0.1, reset 0) | |
| `Voicemeeter.StripPanYDown/Up/Reset` | Strip; range depends on the strip | hardware 0 to 1, virtual -0.5 to +0.5 (step 0.1, reset 0) | |
| `Voicemeeter.StripEQGain1Down/Up/Reset` | Virtual input | -12 to +12 dB (step 1, reset 0) | |
| `Voicemeeter.StripEQGain2Down/Up/Reset` | Virtual input | -12 to +12 dB (step 1, reset 0) | |
| `Voicemeeter.StripEQGain3Down/Up/Reset` | Virtual input | -12 to +12 dB (step 1, reset 0) | yes |

For a dial, pick Voicemeeter > Strips (or Buses) > channel > "... Gain (dial)" in the command menu. That fills turn left, turn right and press in one step.

### Globals

| Command | What it does |
|---|---|
| `Voicemeeter.Show` | Shows the Voicemeeter window if minimized |
| `Voicemeeter.Eject` | Ejects the recorder cassette |
| `Voicemeeter.Load(Path)` | Loads a Voicemeeter settings file from `Path` |
| `Voicemeeter.Reset` | Resets ALL Voicemeeter configuration to default — affects the whole Voicemeeter |
| `Voicemeeter.Restart` | Restarts the Voicemeeter audio engine, brief audio dropout — affects the whole Voicemeeter |
| `Voicemeeter.Shutdown` | Shuts down Voicemeeter — affects the whole Voicemeeter |

### Raw commands

| Command | What it does |
|---|---|
| `Voicemeeter.Raw(Name,OnColor,OffColor,Api)` | If `Api` is a single parameter (no `=`, no `;`), toggles it 0/1. Otherwise runs `Api` as a `;`-separated script; any instruction may use `%toggle%` on the right of its `=`, which is replaced with the inverted current value of that instruction's own parameter. `Name` is the button label (falls back to `Api`); `OnColor`/`OffColor` are `#rrggbb`/`#rgb` colors for the toggle state, ignored for scripts |
| `Voicemeeter.RawDown(Step,Min,Max,Api)` / `Voicemeeter.RawUp(Step,Min,Max,Api)` | Raises/lowers any `Api` parameter by `Step`, clamped to `[Min, Max]` (defaults: Step 1, Min 0, Max 10) |
| `Voicemeeter.RawReset(Step,Min,Max,Api)` | Sets `Api` to 0, clamped to `[Min, Max]`; on a touch button shows the live value |

`Api` is always the last parameter and may itself contain commas (it is rejoined from that position onward), since Voicemeeter scripts and file paths legitimately contain them.

### Level meter

`Voicemeeter.Level(Channel,Type)`: live meter on a touch button, in dB. `Channel` is a strip number for the input types, a bus number or name (`A1`, `B2`) for `Output`. `Type` is `PreFader`, `PostFader` (default), `PostMute` or `Output`.

### Voicemeeter Levels (side strip)

One band per adjacent dial: channel name, a level bar (green/yellow/red zones, white marker at the dial's gain on the same -60/+12 dB scale) and the gain value or "muted". Meters whatever Voicemeeter command (toggle, knob or Level) the dial next to it is bound to. Where a dial has no such command, it falls back to a fixed channel list from the `levels.stripChannels` plugin setting (default `1,A1,B1`; numbers are strips, `A1`..`B3` are buses).

## Building

`LoupixDeck.PluginSdk` 1.23.0 is not on nuget.org. Put its nupkg into `local-feed\`, then run `dotnet test` and `.\build.ps1` (this writes `dist\voicemeeter-<version>-windows.zip`).

## License

MIT, see [LICENSE](LICENSE). Based on XeroxDev's plugin (MIT).
