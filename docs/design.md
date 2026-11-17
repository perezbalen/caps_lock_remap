# Caps Lock Remap — product and technical design

Last updated: 2026-11-16 (America/Bogota)

Status: First tray/input prototype implemented and transition-tested; Windows integration validation and reliability work remain incomplete.

## Purpose and continuity

This document is the working specification for implementation across separate chats. Read it before changing the application. Keep it updated when decisions change, record validation results, and leave a concrete next step in the implementation status section.

The user currently uses AutoHotkey to turn Caps Lock into Ctrl + Alt + Shift for system-wide shortcuts configured in PowerToys. The existing script sometimes stops working around sleep, lid changes, and power changes. A failed shortcut can insert a letter and replace selected text. The actual failure mechanism has not been diagnosed; do not assume every failure is a process crash.

The replacement must be a small, fast Windows tray application whose input handling and recovery are designed explicitly for these failures.

## Confirmed user requirements

- Target Windows 11.
- Holding Caps Lock holds a configurable set of modifier keys. Default: Ctrl + Alt + Shift.
- Releasing Caps Lock releases the app's simulated modifiers while preserving modifiers still physically held by the user.
- A tap can perform a configurable key action; doing nothing must be supported.
- Caps Lock does not toggle capitalization while remapping is enabled.
- Existing PowerToys shortcuts continue to work. The app supplies modifiers; PowerToys remains responsible for shortcut actions.
- Support ordinary desktop applications and applications running as administrator.
- Remote Desktop, virtual machines, and games are outside the initial scope.
- Live in the notification area with clear enabled/disabled status, an enable/disable toggle, Settings, a checked Start with Windows option, and Exit.
- Start automatically at sign-in by default, subject to successful setup and required administrator approval.
- A one-time administrator setup is acceptable.
- Deliver a portable executable. A larger file is acceptable if deployment and maintenance are simpler.
- Follow Windows UI and application behavior conventions.

## Proposed implementation decisions

These are the initial engineering choices, not additional requirements from the user. Change them if implementation evidence warrants it and record the reason below.

- C# and a supported .NET LTS release; choose and pin the actual SDK when implementation starts.
- Windows Forms for native controls, notification-area integration, accessibility, and straightforward deployment.
- Direct Win32 interop for keyboard interception, input injection, session and power notifications, and task scheduling.
- Framework-dependent single-file Windows executable, per the user's packaging decision after reviewing the 111 MiB self-contained build. Requires the .NET 10 Desktop Runtime (x64); it is already installed on the user's laptop. Publish with the Portable profile; debug symbols are embedded.
- Initial target Windows x64. Confirm whether ARM64 output is needed before claiming ARM64 support.
- Run one application instance per interactive user session.
- Use a dedicated message-loop thread for the keyboard hook, separate from settings UI, persistence, logging, and recovery orchestration.
- Use a per-user interactive scheduled task with highest available privileges for elevated sign-in startup. Do not run the tray UI as a Windows service or as SYSTEM.

## Input behavior

### Hold and tap

Default settings: hold Ctrl + Alt + Shift, tap does nothing, tap threshold 200 ms. The threshold is a proposed starting value and must be configurable.

Inject hold modifiers immediately on Caps Lock down so shortcuts have no tap-decision delay. Repeated Caps Lock down events must not inject duplicate presses.

A tap action is eligible only when Caps Lock is released within the threshold and no other physical keyboard key was pressed during the hold. Release the app-owned hold modifiers before injecting the tap key. A longer hold or any intervening key press cancels the tap action. Mouse clicks do not cancel taps in the initial design; revisit if testing shows accidental actions.

Version one supports modifier checkboxes for Ctrl, Alt, Shift, and Windows, and either no tap action or one supported non-modifier key, such as Escape. Arbitrary macros, text expansion, and configurable source keys are outside the initial scope. The settings UI must clearly describe the supported choices.

### Key ownership and transitions

Track left and right physical modifiers separately from the app's requested modifiers and injected events. Tag injected input so it is not treated as a new physical press or recursively remapped.

The desired logical modifier state is the union of physical state and the active Caps Lock modifier request. Account for physical modifiers pressed or released before, during, and after a Caps Lock hold. Merely skipping an injected key-down when a physical key is already held is insufficient: releasing that physical key during the hold must still preserve the requested modifier.

Use a documented, deterministic side for synthesized modifiers, initially left-side keys. Validate modifier reconciliation against actual Windows event behavior, including Alt menu activation and Windows-key side effects.

On disable, settings application, session transition, recovery, and normal exit, cancel any pending tap and reconcile app-owned modifiers. Do not blindly send key-up events for all modifiers. Defer re-arming until Caps Lock is physically released after a reset, so a stale hold cannot fire a tap or leave the next shortcut in a partial state.

When disabled, Caps Lock returns to ordinary Windows behavior. While enabled, ensure capitalization is off, including when enabling after Caps Lock was on. Do not restore a previous capitalization state on exit in the initial design.

## Architecture

| Component | Responsibility |
| --- | --- |
| Application host | Single-instance handling, lifetime, startup arguments, coordinated shutdown |
| Keyboard engine | Hook installation, minimal event processing, physical state tracking, state machine |
| Input injector | Tagged SendInput batches, result checking, modifier reconciliation |
| Recovery coordinator | Power/session notifications, health checks, bounded hook reinstallation |
| Tray and settings | Status, commands, accessible configuration and validation |
| Startup integration | Inspect/register/remove the current user's scheduled task |
| Settings store | Versioned configuration, atomic writes, migration and defaults |
| Diagnostics | Bounded operational logs and exportable troubleshooting information |

Keep input-state logic separate from Win32 calls so meaningful transition tests can run without generating real keystrokes.

### Engine states

- Disabled: no Caps Lock interception.
- Ready: hook active and waiting for a fresh press.
- Held: modifiers requested; tap eligibility tracked.
- Recovering: release/reconcile state and rebuild input handling as needed.
- Error: reliable operation could not be restored; show the failure and allow retry.

The tray's enabled indication means remapping is requested and initialization succeeded. It must not claim proof that every application is receiving input correctly.

## Reliability and boundaries

Windows can silently remove a low-level keyboard hook after a callback timeout. Keep the callback bounded: no disk access, UI calls, network calls, blocking waits, or expensive logging. Allocate and synchronize conservatively in this path.

Handle suspend/resume, session lock/unlock, Explorer restart, startup while running on battery, and transitions between AC and battery. Refresh the hook and reconcile state after relevant transitions; avoid unnecessary full-process restarts.

Configure the scheduled task to allow startup and continued operation on battery, with no arbitrary execution time limit. Verify task restart behavior rather than assuming it covers every crash or hang.

Recovery must detect more than a living process. Prototype a hook health check, such as a tagged harmless sentinel observed by the hook, and validate that it cannot activate user shortcuts or alter application input. A timer or thread heartbeat alone cannot prove hook health. If safe hook verification proves impractical, document the actual detection limit before shipping.

Recovery should use bounded retries and avoid reinstallation while Caps Lock is held when possible. A failed SendInput batch or hook installation is an operational error, not a successful mapping. Provide a visible error state and retain diagnostic details without recording typed content.

No user-mode tray app can guarantee remapping while its process is stopped, on a secure desktop, or during all system failures. Scheduled task recovery and hook checks reduce the failure window; they cannot guarantee zero accidental typing. Normal application cleanup also cannot run after forced termination.

If testing cannot meet the reliability goal, evaluate a separate, explicitly opt-in system-level mapping of Caps Lock to an unused key, such as F24, as a failure-containment option. It requires additional design and testing, may require restart, changes disabled behavior, and still does not prevent a following letter from being typed if the app is absent. It is not part of the agreed baseline. Kernel keyboard drivers are outside version-one scope.

## Elevation and portable startup

SendInput is restricted by Windows integrity levels. The initial plan runs the input host elevated so it can interact with elevated desktop apps. UAC prompts and other secure-desktop screens are outside scope.

First launch must explain why elevation is needed and request it through Windows. Creating or updating elevated startup registration may require approval. Do not imply that accepting one-time setup eliminates all future UAC prompts, particularly for direct manual launches or moving the executable.

The Start with Windows checkbox reflects the actual task registration state. Commit its checked state only after the operation succeeds. Use a stable, application-specific task identity and modify only that task. Remove the registration when the checkbox is cleared.

A portable executable must remain at its registered path. Detect an outdated registration when launched from a new location and offer repair. Show the configured startup path in Settings. Do not silently copy the program into a protected directory.

## Windows UI conventions

- Start quietly in the tray; show onboarding/settings on first launch when setup is needed.
- Right-click menu: Enabled checkbox, Settings, Start with Windows checkbox, separator, Exit.
- Double-click the tray icon to open Settings. A second launch activates the existing instance's Settings.
- Use distinguishable status icons plus tooltip text; do not rely on color alone. Expose recovery/error status without constant notifications.
- Settings has Hold modifiers, Tap action, Tap threshold, startup status/path, and a small diagnostics section.
- Save validates and applies settings atomically after safely resetting input state. Cancel discards edits.
- Closing Settings leaves the tray application running. Exit releases app-owned input and terminates the instance.
- Support keyboard navigation, accessible labels, system font and scaling, multiple monitors, and high contrast. Follow system theme where supported by the chosen toolkit; validate before claiming full dark-mode support.
- Recreate the tray icon after Explorer restarts.

## Settings and diagnostics

Store versioned JSON in `%LOCALAPPDATA%\CapsLockRemap\settings.json`. Portable describes executable deployment; settings persist in the user's profile. Use an atomic replacement strategy and recover gracefully from malformed files.

Store bounded diagnostic logs in the same application directory under the user profile. Record lifecycle events, recovery attempts, API errors, version, and startup registration status. Never record ordinary keys, text, window contents, or shortcut histories. Configuration export must be explicit.

No network connection, telemetry, or automatic updater is required for version one. Unsigned portable builds may trigger Windows reputation warnings; do not disable Windows protection to bypass them.

## Validation and acceptance

Automate meaningful state-machine cases: repeat events, tap cancellation and timing, physical modifier overlap in both directions, mixed left/right modifiers, injected-event filtering, disable/reset while held, and failures during partial injection. Use an injectable clock and input abstraction where useful.

Manual Windows integration validation is required before declaring the input behavior reliable:

| Scenario | Expected result |
| --- | --- |
| Caps Lock + S with a configured PowerToys shortcut | Shortcut fires; no stray S; modifiers release |
| Short tap, long hold, and interrupted tap | Exactly the configured tap behavior; no unwanted tap |
| Physical Shift/Ctrl/Alt overlap before and during a hold | Physical holds survive; requested modifiers remain active until Caps Lock release |
| Ordinary and elevated applications | Equivalent shortcut behavior |
| Repeated sleep/resume, lid and AC/battery changes | Mapping resumes; no stuck modifiers; failures are reported |
| Lock/unlock, including a held key | Safe reset and a fresh subsequent hold |
| Disable, save settings, and exit while held | No unintended tap or stuck app-owned modifiers |
| Forced termination and simulated hook failure | Recovery behavior and remaining failure window measured and documented |
| Sign-in on battery; startup toggle; moved executable | Correct task behavior, truthful checkbox and actionable repair |
| Second launch and Explorer restart | One instance and restored tray icon |
| Scaling, keyboard-only navigation, high contrast | Usable settings and readable status |

Measure idle CPU, working set, startup time, executable size, and shortcut latency on the target laptop. Record results; no numerical resource or latency budgets have yet been agreed. Never describe implementation tests alone as proof of end-to-end reliability.

## Implementation milestones

1. Establish project structure, pin SDK, add a minimal tray host and build instructions.
2. Prototype keyboard interception/injection and validate with PowerToys and elevated apps before investing in UI polish.
3. Implement the tested hold/tap state machine and physical modifier reconciliation.
4. Add recovery, diagnostics, and fault-injection validation; resolve hook health detection.
5. Add settings UI, persistence, single-instance activation, and Explorer recovery.
6. Implement elevated startup registration and portable-path repair.
7. Publish the portable build, run the Windows acceptance matrix, document measurements and limitations.

## Implementation status and handoff

- Tap settings fix: replaced the dropdown's data binding with explicit items because form binding-context initialization reset the saved selection to "Do nothing". A regression check reproduced the original failure and now verifies all six saved tap actions survive window creation. All 14 checks pass; Release solution build passes with zero warnings/errors. Portable publishing succeeded with NU1900 warnings because NuGet vulnerability metadata was unreachable. Updated executable: `artifacts/win-x64/CapsLockRemap.exe`. Replace the installed copy and verify saving/reopening Settings interactively.

- Startup verification fix: inspected the user's saved task read-only; Task Scheduler canonicalizes the logon trigger SID to an account name. Verification now resolves account names to SIDs rather than comparing raw strings. The existing task points to `C:\Apps\CapsLock\CapsLockRemap.exe`; replace that copy with the updated build. No task was modified during this diagnosis.

- Startup bug fix: missing-task HRESULT `0x80070002` can be translated by .NET to `FileNotFoundException`; lookup/deletion now handle that code regardless of exception class, while retaining access-denied errors. Regression checks added; all 12 checks and portable publishing pass. User still needs to retry the updated executable and complete UAC/sign-in validation.

- Completed: requirements discussion, design baseline, .NET 10.0.404 SDK pin, Windows Forms project and solution, ignore rules, and README build instructions. Existing system tools were sufficient; no installation was needed.
- Validation: `dotnet build CapsLockRemap.slnx -c Release` passed with zero warnings/errors; self-contained win-x64 single-file publishing passed using the README command with NuGet network access. No GUI or keyboard integration validation performed.
- Implemented prototype: tray host, hold/tap configuration, atomic settings save, dedicated hook thread, modifier ownership, manual retry, power/session reset, administrator relaunch, and per-session instance guard. Ten pure transition tests pass; Release build has zero warnings/errors.
- Startup implemented: tray checkbox reflects task existence, elevation helper runs before the instance guard, interactive per-user highest-privilege task supports battery operation and no execution limit, and stale executable paths offer repair. Startup remains opt-in; automatic onboarding/default registration is deferred. Eleven automated checks pass, including task XML; framework-dependent publish succeeded. Actual COM registration/removal, UAC, and sign-in still need interactive validation. No task was created during development.
- Not started: silent hook-loss detection, diagnostics, and interactive integration validation. Second launch currently reports the existing instance rather than activating Settings. First launch opens Settings; automatic elevated onboarding is still pending. Explorer recovery relies on WinForms NotifyIcon behavior and needs validation.
- No system keyboard mappings or scheduled tasks have been changed during development. The user reports the input prototype is working so far; this is not completion of the acceptance matrix.
- Next step: manually validate input ordering with PowerToys and elevated apps, validate startup registration/UAC/sign-in, then implement hook health/recovery. Do not claim reliability until the acceptance matrix passes. The prototype has not been launched against the user's keyboard.
- Engineering questions to resolve through prototyping: safe hook health verification, modifier reconciliation with other injectors, exact elevated-launch behavior, and supported single-key tap choices.

For each future work session, update this section with completed work, exact build/test commands and results, known problems, and the next concrete action. Distinguish implemented behavior from planned behavior.

## Decision log

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-11-16 | Framework-dependent single-file publishing replaces self-contained delivery | User requested a smaller executable; desktop runtime is already installed |
| 2026-11-16 | Windows 11 tray app with configurable hold/tap behavior | Confirmed user requirements |
| 2026-11-16 | Proposed C#/.NET with Windows Forms and Win32 interop | Maintainability, native controls, portable self-contained distribution |
| 2026-11-16 | Proposed elevated interactive scheduled task | Elevated-app support and sign-in startup |
| 2026-11-16 | No driver or global registry remap in baseline | Keep initial deployment portable and scoped; evaluate stronger containment only if needed |

## Technical references

- [LowLevelKeyboardProc: callback constraints and silent hook removal](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)
- [SendInput: integrity restrictions and existing keyboard state](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput)
- [Task Scheduler interactive logon type](https://learn.microsoft.com/en-us/windows/win32/taskschd/taskschedulerschema-logontype-principaltype-element)

Check current primary documentation when implementing platform-specific details. These references inform the design; they do not establish that the proposed recovery mechanisms have been validated.

