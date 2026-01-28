# UIA Recorder (WPF .NET 8) — Plan

## Overview
Build a Windows desktop WPF .NET 8 application that globally records UI Automation events and keystrokes across all processes, producing AI-friendly JSON and a human-readable timeline on Stop. Focus is on reliable capture, privacy by default, and operator-friendly status visibility.

## Project Type
**Desktop** (WPF .NET 8 on Windows 10/11)

## Scope
### In Scope
- Global UIA event capture: focus changed, invoke, value/text/selection changes, window opened/closed
- Foreground window change tracking
- Global keystrokes with assembled text bursts + special keys
- UI: Record/Stop, output directory picker, password capture toggle (default off)
- Redaction by default using UIA `IsPassword` (passwords masked unless user enables capture)
- Debounce/noise suppression for high-frequency events
- Status counters for captured event types
- Output on Stop: 
  - AI-friendly JSON (session + events)
  - Human-readable chronological timeline summary
- Ignore the app’s own events

### Out of Scope
- Playback or automation execution
- Cross-platform support (Windows only)
- Cloud sync or remote upload

## Success Criteria
- Records required UIA events globally across processes without capturing its own UI
- Password fields redacted by default; capture only when toggle enabled
- Keystrokes captured as text bursts plus special keys
- Outputs saved on Stop to user-selected directory: JSON + timeline summary
- Debounce reduces noisy duplicates without dropping meaningful events
- Works on Windows 10/11 with stable performance

## Tech Stack
- **WPF .NET 8** for desktop UI
- **Windows UI Automation (UIA)** for event capture and element metadata
- **Low-level keyboard hook** for global keystrokes
- **System.Text.Json** for structured output
- **File I/O** with safe buffering and graceful shutdown on Stop

## File Structure (Planned)
- `/App.xaml` and `/App.xaml.cs`
- `/MainWindow.xaml` and `/MainWindow.xaml.cs`
- `/Services/` (UIA event subscription, keyboard hook, debounce, filtering)
- `/Models/` (event models, session metadata)
- `/Output/` (JSON serialization + timeline formatter)
- `/Utilities/` (privacy helpers, time/sequence helpers)

## Task Breakdown
Each task includes **INPUT → OUTPUT → VERIFY** and a rollback strategy.

### T1 — Requirements & Event Model Definition
- **Agent**: backend-specialist
- **Priority**: P1
- **Dependencies**: none
- **Input**: requirements list (events, outputs, privacy rules)
- **Output**: event schema definitions (session + event fields), privacy rules documented
- **Verify**: schema covers all required events; redaction rules clear
- **Rollback**: revert schema doc to last agreed version

### T2 — UI Layout & Controls Plan (WPF)
- **Agent**: frontend-specialist
- **Priority**: P2
- **Dependencies**: T1
- **Input**: feature list and UI requirements
- **Output**: UI layout plan (Record/Stop, directory picker, password toggle, status counters)
- **Verify**: all required controls included; default states defined
- **Rollback**: revert UI plan to minimal layout

### T3 — UIA Event Capture Strategy
- **Agent**: backend-specialist
- **Priority**: P1
- **Dependencies**: T1
- **Input**: event list + global capture scope
- **Output**: subscription plan for UIA events; process/self-ignore rules
- **Verify**: coverage for focus/invoke/value/selection/window events + foreground changes
- **Rollback**: fall back to minimal event set

### T4 — Keystroke Capture Strategy
- **Agent**: backend-specialist
- **Priority**: P1
- **Dependencies**: T1
- **Input**: keystroke requirements
- **Output**: approach for global hook + text burst assembly + special keys mapping
- **Verify**: plan includes privacy handling and reliable buffering
- **Rollback**: reduce to special keys only if needed

### T5 — Debounce & Noise Filtering Rules
- **Agent**: backend-specialist
- **Priority**: P1
- **Dependencies**: T1
- **Input**: event list + noise concerns
- **Output**: debounce strategy (time window, duplicate suppression criteria)
- **Verify**: rules documented for noisy events
- **Rollback**: disable debounce for critical event types

### T6 — Output Formats (JSON + Timeline)
- **Agent**: backend-specialist
- **Priority**: P1
- **Dependencies**: T1
- **Input**: schema and session metadata
- **Output**: JSON structure and timeline formatting rules
- **Verify**: JSON is AI-friendly; timeline is chronological and readable
- **Rollback**: simplify timeline fields to core set

### T7 — Status Counters & Session State
- **Agent**: frontend-specialist
- **Priority**: P2
- **Dependencies**: T2, T3, T4
- **Input**: event schema and UI plan
- **Output**: counter definitions and update rules during recording
- **Verify**: counters match required event categories
- **Rollback**: aggregate counters into fewer categories

### T8 — Windows 10/11 Compatibility Checks
- **Agent**: test-engineer
- **Priority**: P3
- **Dependencies**: T3, T4
- **Input**: capture strategies
- **Output**: compatibility checklist and runtime considerations
- **Verify**: requirements confirm Win10/11 support
- **Rollback**: constrain to minimum supported APIs

## Dependencies Graph (Summary)
- T1 → {T2, T3, T4, T5, T6}
- T2 → T7
- T3 & T4 → T7, T8

## Risks & Mitigations
- **Global hooks stability** → ensure clean start/stop and safe teardown
- **Privacy leakage** → default redaction + explicit opt-in for passwords
- **Noisy UIA events** → debounce and duplicate suppression
- **Performance** → buffering and throttling strategies

## Phase X: Verification (After Implementation)
> Do not mark complete until all checks pass and are executed.

1. **Lint & Type Check**
   - `npm run lint && npx tsc --noEmit` (if JS tooling exists)
2. **Security Scan**
   - `python .agent/skills/vulnerability-scanner/scripts/security_scan.py .`
3. **UX Audit**
   - `python .agent/skills/frontend-design/scripts/ux_audit.py .`
4. **Build**
   - Build WPF solution (MSBuild/Visual Studio) and confirm success
5. **Runtime Smoke Test**
   - Record across multiple apps; verify redaction; verify outputs on Stop

## ✅ PHASE X COMPLETE
- Lint: ⬜
- Security: ⬜
- Build: ⬜
- Date: ⬜
