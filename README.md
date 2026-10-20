# VREC

Windows 11 tray application for experimental per-match VALORANT recording through OBS WebSocket v5. The original console prototype is retained in `prototype/Program.cs.txt`; `VREC-QuickStart.zip` is the **old session recorder**, not this version.

**Status:** Windows executable cross-built on Linux; state-machine and mock OBS tests run here. Screen capture, Credential Manager, startup registration, real OBS, live VALORANT detection, all game modes and gaming performance require Windows testing. This is an implementation for calibration and testing, not a claim of production-ready match recognition.

## Download from GitHub

Open [Windows build and simulations](https://github.com/Neddyexe/VREC/actions/workflows/windows.yml), select the latest successful run for your commit, and open its **Summary**. Under **Artifacts**, click **VREC-win-x64** to download the ZIP. Sign in to GitHub with access to this repository. Extract the whole ZIP before launching `VREC.exe` or the optional `Install.ps1`.

The ZIP contains the self-contained Windows x64 application, installer script, these calibration instructions, and the Windows testing/detection guides. No separate .NET runtime is required. Artifacts expire after 30 days. To rebuild, select **Run workflow** on that workflow page and choose `main`. Pushes also trigger builds automatically. A successful CI build does not establish live VALORANT detection accuracy.

## Build and install

Install .NET 8 SDK (8.0.419 or later 8.0 patch). On Windows run:

```powershell
./scripts/Publish.ps1
```

This runs simulations and produces `artifacts/win-x64/VREC.exe`, a self-contained x64 executable. Copy that package to the gaming PC. Run `VREC.exe` directly, or right-click the packaged `Install.ps1` and run with PowerShell for a per-user Start menu installation. No administrator rights required. Windows CI builds and uploads this package; see the selected run for its actual test and build result.

For source development, `BUILD-AND-RUN.cmd` builds and launches the tray application without a console. Exit through its tray menu before replacing files. To uninstall, exit VREC, disable Start with Windows, delete `%LOCALAPPDATA%\Programs\VREC` and the VREC Start menu shortcut. Optional: delete `%LOCALAPPDATA%\VREC` and the `VREC/OBS` entry in Windows Credential Manager.

## First run

1. Keep OBS running. Enable its authenticated WebSocket server on port 4455. Use a working Game Capture scene and verify game/audio capture in OBS. VREC does not launch or configure OBS.
2. Open VREC's tray Settings. Enter the OBS WebSocket password. It is stored in Windows Credential Manager, never in JSON or diagnostic logs. Connections are restricted to `127.0.0.1`.
3. Calibrate visible UI templates as described below. Save and restart VREC after the current match. Configuration changes intentionally do not interrupt an active recording.
4. Optionally enable Start with Windows **after installation in a permanent location**. This starts VREC at sign-in, not OBS.

VREC never changes OBS recording quality, encoder, scene, resolution, frame rate, output directory, or container. Keep the tested OBS profile: **2560×1440, 120 FPS, NVENC HEVC, MKV, CQP 20, keyframe 2 seconds, P5, single pass, look-ahead off, adaptive quantization off, B-frames 2**. Hardware and capture validation are done in OBS; VREC does not certify these settings are currently selected.

## Visual calibration and detection

No match templates are shipped because no real gameplay screenshots were supplied. With no templates the app waits; game launch does not start recording. There is no automatic full-session recording fallback.

Use borderless window mode. Choose a state in Settings, click **Capture template in 4 seconds**, and switch to foreground VALORANT during the countdown. On the captured image drag a small, distinctive UI region, then press Enter. Escape cancels. Only the downsampled selected region and its normalized coordinates are retained, not the full screenshot. Settings remain local in `%LOCALAPPDATA%\VREC\settings.json`.

At minimum capture **MatchActive** and **PostMatch**. Choose a match-only HUD element that remains recognizable through gameplay, and a clear post-match result element. Avoid flat backgrounds, animated scenes, common menu decorations, overlays and text that changes every second. Capture multiple examples when necessary. Add menu, queue, agent select, loading and ending templates to distinguish those states. Calibration across modes, UI scale, aspect ratio and localization is required. After changing display configuration, recalibrate.

Default polling is every 1.5 seconds. Each template compares 32×18 RGB samples with a similarity threshold of 0.96. Competing states within 0.03 are treated as Unknown. Two consecutive match observations start recording; three consecutive end/menu/closed observations plus an 8-second post buffer stop it. Settings expose polling, threshold, post buffer and an optional early start on confirmed loading. Advanced confirmation counts/margin can be edited in JSON while VREC is closed.

Unknown/ambiguous evidence, alt-tab and missed screenshots preserve an ongoing recording. Confirmed game closure stops it after the safety delay. There is no maximum-match timer, so overtime does not end capture. Ending evidence must remain continuous through the safety delay; a return to active or unknown cancels it. A new confirmed active match after saving creates another OBS file.

**Timing limitation:** this uses StartRecord, not a replay buffer. It cannot recover footage before detection. Default recognition can miss several seconds at the start; optional loading start includes loading footage and reduces that risk but is not true pre-roll. A configurable number of seconds of genuine pre-match footage is not implemented. False negatives and false positives remain possible; do not rely on this for irreplaceable footage before live validation.

## Recovery and ownership

Commands are serialized; OBS requests have five-second timeouts and reconnection uses bounded backoff. If OBS is idle during an active match, VREC retries recording. It never deliberately stops a recording it found already active.

After a failed or unconfirmed OBS request, ownership is relinquished because OBS v5 does not supply a durable owner token. An existing active output is then left alone. This preserves footage but can merge subsequent matches into that recording; check OBS manually after such an error. Fully automatic recovery of an ambiguously acknowledged recording is not solved. Avoid manual OBS record controls while VREC owns a recording. A stop/start performed externally between polls cannot be distinguished reliably.

Normal tray exit and Windows sign-out attempt to stop confirmed VREC-owned recordings with a bounded timeout. Forced termination, power loss or OBS failure cannot guarantee finalization. MKV reduces container damage risk. Error status appears in the tray menu; local rotating diagnostics are in `%LOCALAPPDATA%\VREC`. Logs contain states and generic errors, never passwords or raw OBS payloads.

## Tests and development

```sh
dotnet run --project tests/VREC.Tests.csproj -c Release
dotnet build VREC.csproj -c Release
```

Tests use a dependency-free executable runner that exits nonzero on assertion failure. They cover transitions, debounce, uncertain evidence, external recording ownership, retries, game crashes, separate matches, safety delays, explicit loading start, similarity ambiguity, a **synthetic** two-match event fixture, and a local mock OBS server exercising authentication, event interleaving, fragmented messages and start/status/stop. Fixtures are not recordings of real VALORANT behavior.

The Windows UI references the cross-platform `Core` project. `ObsClient.cs` retains the prototype's WebSocket v5 authentication and command protocol; `Windows/ObsRecorder.cs` adds timeouts and transport reset. Future timestamps/highlights can consume confirmed transitions without coupling game detection to recording transport.

See [Windows validation](docs/WINDOWS-TESTING.md) and [detection research and limits](docs/DETECTION.md). On Linux use `bash scripts/cloud-setup.sh`; this prepares tests and cross-compilation, not Windows runtime services.
