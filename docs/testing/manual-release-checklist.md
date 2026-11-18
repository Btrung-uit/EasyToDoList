# M12 — Release candidate checklist

Candidate: **0.1.0-rc.1**, Windows 11 x64. Implementation and local packaging do not by themselves authorize a public release. Unchecked gates below remain open.

## Build and artifacts

- [x] Full solution tests pass on the final source snapshot.
- [x] Release warnings-as-errors build passes.
- [x] `scripts/package-release.ps1` produces installer, portable ZIP and `SHA256SUMS.txt` in one successful invocation.
- [x] Self-contained payload contains `coreclr.dll`, `hostfxr.dll`, WPF assemblies, native `e_sqlite3.dll`, README, asset provenance and runtime license/notices; no user database or secrets.
- [x] Recompute hashes after the final build and compare with the manifest. No files may be replaced after hashing.
- [x] Release workflow defines build/test before packaging and artifact upload; its command sequence passed locally. No automatic GitHub Release is created.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-release.ps1
```

Version defaults to the application project. `-Version` accepts a three-part version with an optional prerelease suffix. Tag workflow strips a leading `v`. Use `-NoRestore` only when assets have already been restored for `win-x64` with `SelfContained=true`. Outputs are unsigned release candidates; signing requires the maintainer's signing identity and is not fabricated.

## Clean Windows installer smoke

Run on Windows Sandbox with networking and clipboard sharing disabled. The launcher maps only its generated input folder read-only and its evidence folder writable. It never maps the personal application-data directory. The guest smoke refuses to run outside `WDAGUtilityAccount`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-installer-sandbox.ps1 `
  -Installer 'artifacts/release/0.1.0-rc.1/EasyToDoList-0.1.0-rc.1-win-x64-setup.exe' `
  -PublishDirectory '<self-contained publish directory printed by package-release.ps1>' `
  -CompilerPath '<path to Inno Setup 6 ISCC.exe>'
```

Inspect the printed evidence directory. `result.json` must say `passed`; a VM starting is not proof. Keep its steps, logs, screenshot and candidate hashes. The guest shuts down after the test; close any remaining Sandbox client window. Do not replace the account guard to run this on personal data.

- [x] Windows 11 x64 clean profile, no system `dotnet`; self-contained app runs with Sandbox networking disabled.
- [x] Fresh per-user installation succeeds without requesting elevation; Start Menu/Desktop shortcuts target the installed EXE.
- [x] Task create/complete and reward balance work; a second launch restores the primary; X hides to tray.
- [x] Upgrade and uninstall refuse a running application without killing the timer.
- [x] Simulated `WM_QUERYENDSESSION` only to the owned test app saves the running Pomodoro and exits; reopening restores Paused.
- [x] Upgrade keeps the same AppId/installation and updates version registration. Database SHA256 is unchanged by the installer itself; task, balance and timer survive startup.
- [x] Uninstall removes executable, shortcuts and its own startup command; database/settings/history/logs remain.
- [x] Reinstall retains tasks, rewards, Reduced Motion and muted settings; recovered Pomodoro resumes.
- [x] Forced termination of the owned guest process restores Paused from its checkpoint without extra reward.
- [x] Uninstall preserves a startup value pointing to a different installation.

The older-version installer fixture intentionally uses the candidate payload with an older **installer registration**, because no previous public release exists. This proves installer identity/upgrade behavior; it does not pretend to test a previous released binary. Schema compatibility is tested separately by migrating a real SQLite file from `20261112081912_AddTodoRewards`, retaining task/profile data and applying initialization twice.

- [ ] Execute the updated GitHub workflow and verify uploaded artifacts before public distribution (not pushed/dispatched in this task).

## Physical and interactive release gates

These are carried forward from [M10](m10-windows-checklist.md) and [M11](m11-visual-accessibility-checklist.md); do not mark them pass from synthetic clocks, raster DPI images, VM message simulation or a successful installer.

- [ ] Let a full Focus finish in tray and confirm the actual Windows end notification and exactly one reward; test each notification switch.
- [ ] Cross actual local midnight and confirm reconciliation/penalty/overdue notification once.
- [ ] Sleep/Resume in both Dashboard and tray; confirm Paused, no elapsed sleeping time or reward. Owner previously reported that Sleep pauses; the entire matrix remains unverified.
- [ ] Actual Windows restart/shutdown/logoff, then sign in: check final-save log, Paused remaining time and automatic startup behavior. No host restart is performed by the smoke scripts.
- [ ] Actual Windows display scaling 100%, 150%, 200%, resize and multiple displays: readable Vietnamese text and accessible controls.
- [ ] Full keyboard-only and Narrator journeys, including the completion checkbox, validation and Undo. UI Automation regression now exercises the real Toggle provider; it does not replace listening to Narrator.
- [ ] System High Contrast, selected/disabled controls and window focus.
- [ ] Complete Idle/Focus/Celebrate/Rest and ambient loops; pivot stability and at least 30 seconds sustained performance versus Reduced Motion/tray.
- [ ] Actual speaker/audio quality, each cue setting, mute and device removal.
- [ ] No unresolved blocker/critical defects; final maintainer decision before tagging or publishing a public release.

## Verification record — 18/11/2026

- Source: M12 snapshot on `codex/m12-installer-release`; one milestone commit. Local Debug and Release suites: **235/235 pass** (App 82, Core 99, Infrastructure 54). Release build: **0 warnings, 0 errors**. The RID restore/build/test/package sequence used by the workflow also passed locally.
- Inno Setup **6.7.4**, downloaded from the official signed release; payload includes .NET 10.0.12 self-contained runtimes and SQLite native. Portable ZIP verified to contain **500 files**, including runtime and SQLitePCLRaw license/notices. SHA256 manifest independently recomputed.
- Exact candidate smoke: **passed**, **8/8 groups**, Windows Sandbox **10.0.26100.0**, no system dotnet and networking disabled. Local evidence: `artifacts/sandbox/254f48c1f3d44dc5951cdfe6db697c43/evidence/result.json`, screenshot and native setup/uninstall logs. UTC completion: `2026-11-18T16:38:46.2372920Z`. Evidence is generated locally, excluded from Git and release payloads.
- Installer SHA256: `3f8b76f89827b05b014881d15e69c5c03acd792ed5a8ad3afffc422a32e7f757`.
- Portable ZIP SHA256: `e486a8ba5a3125d95a0d13ddbe91404ab86b7cc3149d9a864d4a4a0515314666`.
- Runtime smoke discovered that UI Automation Toggle changed the checkbox without executing its Command. The WPF IToggleProvider regression failed at 0% progress before the Checked-event fix and passed afterward; final installed-app smoke confirms persisted completion/rewards.
- Independent read-only review found no actionable defect in the implementation snapshot. No personal application database was used for installation tests. The final installer is unsigned; no stable/public release, push or merge was performed. Physical/interactive and GitHub-execution gates above remain open; M12 release completion is not claimed.
