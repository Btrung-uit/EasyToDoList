# M12 Windows installer and release verification

**Goal:** Build a reproducible win-x64 self-contained release candidate and Inno Setup installer without deleting user data.
**Architecture:** Reuse the WPF application, SQLite migrations and existing release workflow. Install per-user under LocalAppData/Programs; database stays under LocalAppData/EasyToDoList. One packaging script owns publish/archive/installer/hash steps; a clean Windows smoke script owns install/upgrade/uninstall checks.
**Tech:** .NET 10, PowerShell, Inno Setup 6, Windows Sandbox when available.
**Spec:** TASKS.md M12; docs/superpowers/plans/2026-11-11-easy-to-do-list-mvp.md Task 10.

## Constraints and review focus

- No schema changes or new application dependency. Do not delete user database or settings on uninstall.
- Keep AppId stable across versions; upgrade uses the same install path and shortcuts.
- Require app Exit before replacing files. Never force kill a running timer.
- Cleanup of the startup registry value must only remove a command owned by this installation.
- Version/path inputs must be validated; build failure stops packaging and stale output cannot be published as new.
- Self-contained payload must run without a system .NET runtime and include SQLite native binaries and notices.
- Physical DPI, Narrator, true host sleep/restart/midnight remain explicit manual release gates unless actually observed.

## Execution (inline; one milestone commit)

- [x] Add a regression proving migration from the pre-Pomodoro To-do schema retains existing task/profile data. Add packaging assertions covering runtime payload and destructive installer rules.
- [x] Add installer, release script and a stable running-app mutex; set release-candidate version and ship README/asset notices.
- [x] Extend release CI to build/upload installer, portable archive and SHA256 manifest; do not publish a GitHub Release automatically.
- [x] Publish and compile locally; exercise install/upgrade/uninstall, shortcuts, retained database and offline app launch in an isolated Windows environment where available.
- [x] Run full tests and warnings-as-errors build; freeze code for one reviewer, fix confirmed issues, record remaining manual gates and commit M12.

Evidence: full Debug/Release suites 235/235 pass, Release build zero warnings/errors; independent read-only review found no actionable defect. Windows Sandbox build 26100 passed 8 smoke groups with networking disabled and no system dotnet. UI Automation exposed a real completion Checkbox bug; IToggleProvider regression failed at 0% before the Checked-event fix and passed after it. Remaining physical/interactive gates are recorded in docs/testing/manual-release-checklist.md. No host shutdown, restart or personal-data writes were performed.
