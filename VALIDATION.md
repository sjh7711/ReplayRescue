# Validation

Replay Rescue has been checked on Windows with NVIDIA App 11.0.9.251. NVIDIA state and log formats are internal implementation details and may change in future versions.

## Automated checks

- 70 update, policy, browser-state, settings, localization, runtime-log, and native-message framing checks passed.
- 11 UI checks passed: hidden startup, restore, language switching, preservation of unsaved domains and interval, translated tray menus, minimize, and close-to-tray.
- 10 Chrome domain-matching checks passed.
- Extension language delivery, protected-tab reporting, disconnection, and popup language switching checks passed.
- Native Messaging protocol and language-field checks passed.

## Desktop verification

- English and Korean screens were rendered and inspected. The language selector uses **English / Korean** in both interface languages.
- Instant Replay enabled state and confirmed recording state are displayed separately. The tray indicator follows the enabled state: green, yellow, or gray when unknown.
- Automatic recovery was observed from a known-off state, with the NVIDIA enabled setting and a new `IR Enabled` event confirming restoration.
- Current-user Windows startup registration and tray-only execution were checked. A full Windows restart was not tested.
- Without extension reports, a read-only diagnostic confirmed that open Chrome windows do not prevent recovery and that website checks are marked as skipped.
- The latest language-label and packaging checks did not send NVIDIA toggle input.

## Update verification

- Live GitHub metadata parsing and SHA256 digest retrieval passed using the built app.
- Update checks reject prereleases, unexpected download addresses, missing digests, oversized or corrupted ZIPs, unsafe paths, preserved local data paths, and executable-version mismatches.
- Isolated executable fixtures verified the complete installer handoff: wait for the old app, stop only this folder's native hosts, replace files, restart in tray mode, and preserve existing preferences and unrelated files.
- A deliberately locked extension file verified restoration and restart of the previous version. Unit checks also verified removal of newly added files during rollback and release of the installer lock.
- Individual file replacements are atomic; a transaction failure rolls back files already replaced. Power loss and damaged storage were not simulated.
- English and Korean update dialogs and the recording-dot tray logo were rendered and inspected. GitHub update checks do not run in UI previews.
## Limits

Recorded clip playback, continuous frame/audio integrity, and all driver versions have not been verified. Test clip saving in your own game. With desktop capture disabled, waiting for a game is a normal enabled state.

The Chrome extension only reports tabs visible to its profile and permissions. Incognito access must be allowed separately. Chrome app windows such as YouTube Music can differ from Windows window counts; this count mismatch does not block recovery.

Settings, runtime logs, diagnostics, and the generated native-host manifest are local files and are excluded from the repository and release ZIP.
