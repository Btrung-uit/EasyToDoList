# Assets shipped with EasyToDoList

## Guild Quest Board pixel art

Source: `assets/pixel-art/mvp/`. Created for this project using OpenAI image
generation on 2026-11-11, then sliced and pivot-normalized locally.
Prompts, provenance and production limitations: [asset contract](../assets/pixel-art/mvp/README.md).

Runtime includes the generated valley background, normalized knight frames
(Idle/Focus/Celebrate/Rest), cloud sprites, normalized flag/grass and leaves.
No downloaded stock image, source atlas or GIF preview is shipped. The stock
reference itself is excluded. These are AI-generated assets, not a third-party
stock license; no third-party attribution requirement is claimed. Review edge
pixels and similarity to reference material before a public art-quality release.

## Original synthesized audio — CC0 1.0

Files: `assets/audio/*.wav` (completion, combo 02–13, tick, bell).
Source: [generate-audio.cjs](../assets/audio/generate-audio.cjs), written for this
project. Pure additive synthesis; no sampled recordings, sound fonts or external
music. Reproduce with `node assets/audio/generate-audio.cjs`.

To the extent copyright applies, the project contributors dedicate this script
and its generated WAV files to the public domain under **CC0 1.0 Universal**.
Attribution is not required. Full legal text:
<https://creativecommons.org/publicdomain/zero/1.0/legalcode>.

Audio is embedded as mono signed 16-bit PCM, 22050 Hz. Master volume scales PCM
samples; volume zero and per-cue switches suppress playback. Sound failure never
blocks a committed task or a Pomodoro transition.

## Font and platform resources

Segoe UI is requested from the installed Windows font collection; no font file
is redistributed.

## EasyToDoList branding

The transparent logo was supplied by the project owner on 2026-11-18 as
`Logo_EasyToDoList.png`. Source and converted Windows icon are in
`assets/branding/`; the PNG pixels are unchanged. The ICO contains resized
16/24/32/48/64/128/256 px versions and is used for the executable, window and
tray. No third-party license or authorship beyond the owner's supplied asset
is asserted here.

## Bundled software notices

SQLitePCLRaw 2.1.12 (bundle, core, provider and native package) is declared
Apache-2.0 in its NuGet metadata. Unmodified upstream LICENSE.TXT and NOTICE.TXT
from https://github.com/ericsink/SQLitePCL.raw/tree/v2.1.12 are shipped under
`third-party-notices/`. The packaging script also includes the exact .NET runtime
pack license and third-party notices under `runtime-notices/`.
