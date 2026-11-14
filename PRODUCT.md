# EasyToDoList Product Context

## Platform

Windows desktop application built with .NET 10, WPF, and local SQLite storage.

## Users

People who manage study or work tasks on Windows and want a compact focus companion that can remain visible above other applications.

## Purpose

EasyToDoList combines daily task planning, Pomodoro focus sessions, history, and light game-like progression in one local-first utility.

## Core experience

- A full Dashboard is the management surface for tasks, history, profile, rewards, and settings.
- A compact Overlay shows either current tasks or the active Pomodoro and may stay above other windows.
- Only one visual mode is shown at a time; changing modes must not recreate the active Pomodoro service or lose state.
- Closing the window hides the app to its tray state; an explicit Exit action ends the process.

## Product commitments

- Local-first and usable offline; user data remains in SQLite under the Windows application-data directory.
- Medieval pixel-art identity named **Guild Quest Board**.
- Strong legibility: near-black brown text on light parchment and cream text on dark wood.
- Game feedback supports productivity but never changes business results after the database commit.
- Keyboard access, Reduced Motion, and non-audio feedback remain first-class release requirements.

## MVP boundary

The MVP excludes cloud sync, accounts, shop inventory, leaderboards, recurring tasks, subtasks, tags, and projects.
