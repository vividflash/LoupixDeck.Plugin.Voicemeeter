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
| `Voicemeeter.BusEQ(<bus>)` | Bus | Banana, Potato |
| `Voicemeeter.BusSel(<bus>)` | Bus | yes |

### Knobs

`Voicemeeter.<Name>(<channel>,<step>)`: on a dial, a turn changes the value by step per tick (default below) and a press sets the reset value. On a touch button it shows the live value and a press resets.

| Command | Channel | Range (step, reset) | Potato only |
|---|---|---|---|
| `Voicemeeter.StripGain` | Strip | -60 to +12 dB (step 1, reset 0) | |
| `Voicemeeter.BusGain` | Bus | -60 to +12 dB (step 1, reset 0) | |
| `Voicemeeter.StripComp` | Hardware input | 0 to 10 (step 1, reset 0) | |
| `Voicemeeter.StripGate` | Hardware input | 0 to 10 (step 1, reset 0) | |
| `Voicemeeter.StripReverb` | Hardware input | 0 to 10 (step 1, reset 0) | yes |
| `Voicemeeter.StripDelay` | Hardware input | 0 to 10 (step 1, reset 0) | yes |
| `Voicemeeter.StripFx1` | Hardware input | 0 to 10 (step 1, reset 0) | yes |
| `Voicemeeter.StripFx2` | Hardware input | 0 to 10 (step 1, reset 0) | yes |
| `Voicemeeter.StripPanX` | Strip | -0.5 to +0.5 (step 0.1, reset 0) | |
| `Voicemeeter.StripPanY` | Strip; range depends on the strip | hardware 0 to 1, virtual -0.5 to +0.5 (step 0.1, reset 0) | |
| `Voicemeeter.StripEQGain1` | Virtual input | -12 to +12 dB (step 1, reset 0) | |
| `Voicemeeter.StripEQGain2` | Virtual input | -12 to +12 dB (step 1, reset 0) | |
| `Voicemeeter.StripEQGain3` | Virtual input | -12 to +12 dB (step 1, reset 0) | yes |

In the command menu: Voicemeeter > Strips (or Buses) > channel > e.g. "Strip Gain". A dial can keep a different command on press, e.g. `Voicemeeter.StripMute`.

The 0.x commands `...Down`, `...Up` and `...Reset` still work but are no longer listed. Dials that carry them (with `...Reset` or nothing on press) are moved to the new command on first start.

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
| `Voicemeeter.RawAdjustment(Step,Min,Max,Api)` | Knob for any `Api` parameter: turning changes it by `Step`, clamped to `[Min, Max]` (defaults: Step 1, Min 0, Max 10), pressing sets 0 (clamped); the dial shows the value, a touch button shows it as a bar |

`Api` is always the last parameter and may itself contain commas (it is rejoined from that position onward), since Voicemeeter scripts and file paths legitimately contain them.

The old `Voicemeeter.RawDown`, `RawUp` and `RawReset` still work but are no longer listed. Dials that carry them (with `RawReset` or nothing on press) are moved to `Voicemeeter.RawAdjustment` on first start.

### Channel folder

`Voicemeeter.ChannelFolder(Channel)`: touch button that opens a folder for one channel. `Channel` is a strip number or a bus name (`A1`, `B2`).

On a device with 3 rows of keys the outer columns belong to the knobs next to them, here on a 4x3 grid:

| | | | |
|---|---|---|---|
| value of left knob 1 | toggle | toggle | value of right knob 1 |
| value of left knob 2 | toggle | toggle | value of right knob 2 |
| back | toggle | toggle | value of right knob 3 |

- Knobs: turning changes the value by its default step, pressing resets it. The key beside the knob shows the value; touching it does nothing. Left knob 3 sits next to the back key and has no function.
- Knob order (left 1, left 2, right 1, right 2, right 3): Gain, then the first four the channel has of Comp, Gate, Reverb, Delay, Bass, Mid, Treble (EQ Gain 1-3), Pan X, Pan Y, Fx1, Fx2. A bus only has Gain.
- Toggles: Mute, Solo, A1, A2, B1, B2, then the rest in the order of the toggle table. If they do not all fit, the last toggle key is "More" and shows the next ones; the knobs stay.

On other devices the folder is the plain toggle list in table order; toggles that do not fit are left out.

### Level meter

`Voicemeeter.Level(Channel,Type)`: live meter on a touch button, in dB. `Channel` is a strip number for the input types, a bus number or name (`A1`, `B2`) for `Output`. `Type` is `PreFader`, `PostFader` (default), `PostMute` or `Output`.

### Voicemeeter Levels (side strip)

One band per adjacent dial: channel name, a level bar (green/yellow/red zones, white marker at the dial's gain on the same -60/+12 dB scale) and the gain value or "muted". Meters whatever Voicemeeter command (toggle, knob or Level) the dial next to it is bound to. Where a dial has no such command, it falls back to a fixed channel list from the `levels.stripChannels` plugin setting (default `1,A1,B1`; numbers are strips, `A1`..`B3` are buses).

## Building

Run `.\build.ps1` (this writes `dist\voicemeeter-<version>-windows.zip`). The plugin needs LoupixDeck 1.37.0 or newer.

## License

MIT, see [LICENSE](LICENSE). Based on XeroxDev's plugin (MIT).
