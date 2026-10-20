# Windows acceptance procedure

Record Windows/OBS/VREC versions, game language, mode, resolution, UI scale, template set and settings for every run. Keep independent timestamps of actual gameplay boundaries and OBS recording boundaries. None of these live checks has been executed in Linux.

1. Build with scripts/Publish.ps1 and run its package on Windows 11. Verify the tray icon, single-instance behavior, Settings, saved credential, recording-folder action and log rotation. Inspect JSON/logs to confirm the password is absent. Verify startup enabled/disabled after sign-in. OBS must be started separately.
2. With no templates, launch and close VALORANT: no recording should start. Configure authenticated OBS and calibrate active/post-match samples. Save and restart VREC.
3. Visit menu, queue, agent select and loading. Default settings should remain idle. Start a match: verify recording after two active observations. Inspect saved video/audio. Log start delay, missing opening footage, end delay and menu footage. Repeat for at least three consecutive matches and verify separate playable MKVs.
4. Repeat in Deathmatch, Swiftplay, Competitive, Unrated, Spike Rush and any other desired modes. Include overtime and surrender. Do not mark a mode supported solely from a synthetic test.
5. Alt-tab, obscure the game and simulate a temporary connection interruption: recording should continue on Unknown. Return to the game. Test a brief false ending observation: safety delay must cancel. Close/crash the game and verify saving after debounce/buffer.
6. Start a recording manually before a match: VREC must leave it alone, including on Exit. In a separate run close/restart OBS during a match; idle OBS should reconnect and start. For an interrupted or ambiguously acknowledged command, verify the app leaves an already active output alone and reports the condition. Such footage may need manual splitting/stopping.
7. Exit normally during a VREC-owned recording and inspect the MKV. Check sign-out behavior. Do not expect cleanup after forced termination/power loss.
8. Compare identical gameplay runs with VREC off/on: record gaming FPS/frame-time percentiles, VREC CPU/private working set, GPU usage and disk throughput for at least ten minutes each. Include calibration template count and polling interval. Screen-copy costs and gaming overhead have not been measured here.

Fail acceptance for any menu-triggered recording, unexplained missed match, unusable file, external recording stopped, exposed credential or excessive measured gaming overhead. Keep failing screenshots/events for regression work. Until acceptance passes, this is a test build and automatic match detection remains unverified.
