= Ngan.Ime Software Guide

== Overview
Ngan.Ime is an Avalonia-based Rime IME frontend.
It targets desktop and Android, with Android using `InputMethodService` as the system IME host.

== Architecture
The code is split into four layers.

=== `Ngan.Ime.Core`
Keyboard state and event contracts.

=== `Ngan.Ime.Rime`
Rime native loading, session setup, and key processing.

=== `Ngan.Ime.UI`
Cross-platform Avalonia UI for the keyboard, candidates, and input preview.

=== Platform Hosts
Windows starts the desktop app.
Android hosts Avalonia UI inside a real IME service.

== Runtime Flow
1. Platform host starts.
2. DI container creates `ImeState`.
3. UI sends key actions to `ImeState`.
4. `ImeState` forwards to `IImeKeyProcessor`.
5. Rime updates preedit and candidates.
6. UI refreshes from `AfterInput`.

== Android IME
Android uses a standard IME service registration.
The IME panel is rendered by Avalonia and constrained to half screen.

== Note
`AvlnImeDemo` was used as a proof-of-concept for the Android IME hosting path.
