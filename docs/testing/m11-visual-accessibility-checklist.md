# M11 — Visual / accessibility verification

## Automated evidence (2026-11-16)

Final snapshot: **211/211 tests pass** (App 64, Core 99, Infrastructure 48).
Debug and Release solution builds pass with warnings as errors: **0 warnings,
0 errors**. The independent review finding below was fixed and the full suite
and both builds rerun. No app process remains after the WPF test shutdown.

`CompletionWpfTests` creates one real STA WPF window using an in-memory SQLite
database, with master volume zero. It closes the window and shuts down the
application; it does not open or edit the user's database.

- Dashboard and Overlay render; Undo keeps its SQLite transaction semantics.
- Every Dashboard page loads with accessible names for authored inputs, lists,
  tables and action controls; no WPF binding errors are captured.
- Keyboard focus reaches Undo; Tab moves from Hôm nay to Lịch sử. Global control
  styles retain a system-colored focus outline and minimum input heights.
- All 24 shipped knight frames decode at 320 × 320. Pixel art uses nearest
  neighbor scaling. Frame scheduling: Idle/Focus 6 fps, Rest 5 fps, Celebrate
  10 fps once, flag 6 fps, grass 4 fps with an initial offset.
- A running Focus phase selects Focus; pause selects Idle; a running break
  selects Rest. Completion triggers a single celebration without changing rewards.
- Reduced Motion and disabled animation pin frame 00, stop cloud/leaf motion,
  and stop scene timers. Overlay and tray hide stop decorative timers as well.
- Active text palette combinations meet contrast ratio 4.5:1. Disabled controls
  and OS-native calendar/selection colors require interactive review.
- Completion/combo/tick/bell WAV resources are valid mono 16-bit PCM. Volume zero
  is silent. Tick cues occur in the last five seconds; completion bells deduplicate
  on the committed phase end. Settings suppress individual cues.
- `m11-dashboard-100.png`, `m11-dashboard-150.png`, `m11-dashboard-200.png` in the
  App test output are RenderTargetBitmap captures at 96/144/192 DPI. These check
  raster rendering, **not physical per-monitor DPI behavior**. The Dashboard can
  scroll when logical work area is smaller than its content.
- Review regression: a long Vietnamese title plus 35 tasks stays within the
  bounded Dashboard width, wraps onto multiple lines, and retains a finite
  list viewport with its own scrollbar. Outer scrolling does not expand the
  task list or history table indefinitely.
- `m11-runtime.txt` records a short process-CPU observation with a Reduced Motion
  baseline and rendering tier. This is diagnostic only: the test host, screenshot
  readback, and short sampling window prevent a reliable production CPU claim.

Asset provenance and audio licensing: [ASSET-LICENSES](../../Resources/ASSET-LICENSES.md).

The final branch review found the outer ScrollViewer could measure Dashboard
content with infinite space. A focused WPF regression failed before the fix;
the Dashboard now binds its finite dimensions to the scroll viewport (with a
minimum content size), and Today disables horizontal list scrolling. No
domain or persistence behavior changed in this fix.

## Remaining interactive Windows checks

Do not mark M11 complete until these pass on the final Release build.

- [ ] At actual Windows scale 100%, 150%, 200%: Dashboard and both Overlay modes
  remain readable; scroll, resize and move between monitors without inaccessible
  buttons or overlapping text. Try long Vietnamese task titles and validation errors.
- [ ] Observe complete Idle/Focus/Celebrate/Rest, flag and grass loops in the app:
  feet/pole/baseline remain anchored; inspect source-frame silhouette/edge artifacts.
- [ ] Observe clouds (8–16 DIP/s), grass offset and two sparse leaves. Profile
  steady-state animation after warm-up for at least 30 seconds, compare Reduced
  Motion and tray baselines, and check typing/scrolling/timer responsiveness.
- [ ] Keyboard-only: create/edit/complete/Undo/cancel tasks; operate DatePicker,
  history, Pomodoro and Settings; Shift+Tab and focus indicators remain clear.
- [ ] Narrator: verify task context, input names, validation, reward/Undo feedback,
  timer status and table columns. Text, not animation or sound alone, conveys status.
- [ ] Test muted mode, each individual cue switch, speaker quality and abrupt
  device removal. Confirm tick/bell still work while the window is hidden in tray.
- [ ] Check system High Contrast and selected/disabled controls interactively;
  the custom medieval palette is not a claim of complete High Contrast support.
- [ ] Exit from tray closes the process; the existing M10 sleep/resume checklist
  remains a separate release prerequisite.

## Reproducible commands

```powershell
dotnet restore EasyToDoList.sln --verbosity minimal
dotnet test EasyToDoList.sln --no-restore -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet build EasyToDoList.sln --no-restore -warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
dotnet build EasyToDoList.sln -c Release --no-restore -warnaserror -m:1 -p:UseSharedCompilation=false --verbosity minimal
node assets/audio/generate-audio.cjs
```
