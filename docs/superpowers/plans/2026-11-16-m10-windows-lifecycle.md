# M10 Windows lifecycle

Scope: TASKS.md M10 and the approved MVP plan, Task 9. Execute inline; one milestone commit.

- [x] Test startup recovery before reconciliation, midnight/resume, suspend, notification settings/deduplication, and stopped lifecycle.
- [x] Add a process-owned tray, per-user instance activation, startup registration and bounded local logs.
- [x] Wire recovery → reconciliation → UI after migration; keep ticking in every window mode.
- [x] Verify focused tests, full tests, warnings-as-errors build and WPF runtime; record Windows checks still requiring a person.

Implementation decisions: Windows adapters live in App, the existing Windows-targeted composition layer, preserving platform-neutral Infrastructure/Core. Use the Windows Forms NotifyIcon provided by the desktop runtime; no package dependency. Due/overdue notifications group unseen task/deadline/type keys while the process lives, without exposing task titles. Keep a separate lifecycle query/reconciliation DbContext and Pomodoro DbContext; serialize Pomodoro operations so UI commands cannot overlap background ticks. Register auto-start only when settings are saved; synchronize saved configuration at startup. Do not merge M09; branch M10 from its existing commit.

Risks to verify: background callbacks racing UI commands; offline time counted on resume; repeated notifications; startup registration failure; tray/IPC resources surviving Exit. Physical sleep and the Windows notification policy require manual verification.

Review: one read-only reviewer found UI-context power callback deadlock, unconditional cancellation of Windows session ending, and missing startup registration on Exit persistence. Fixed with async dispatcher-aware power adapter, checkpoint recovery on forced shutdown, and registration on both settings persistence paths; regression tests cover dispatcher execution and Exit persistence. Full executable smoke was skipped because a pre-existing user instance is running; isolated WPF/tray and SQLite runtime tests run in the test host instead.

Final verification (2026-11-16): full solution tests 192/192 passed (App 45, Core 99, Infrastructure 48); required warnings-as-errors build passed with zero warnings/errors. Pending native Windows checks are tracked in `docs/testing/m10-windows-checklist.md` and M10 remains unaccepted until those pass.
