VREC -- initial Windows test version (source project)

STATUS: NOT a polished or tested installer. This code has not been compiled or tested on your PC.
It records one VALORANT LAUNCH SESSION, not a separate file per match/map.

REQUIREMENTS:
- Windows 10/11
- OBS Studio open, configured with your 1440p/120 fps/NVENC recording settings
- OBS Tools > WebSocket Server Settings > Enabled, port 4455, authentication enabled
- Microsoft .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0)

USAGE:
1. Extract the ZIP to a folder on your Windows gaming PC.
2. Double-click BUILD-AND-RUN.cmd. It builds the app locally and launches it.
3. When asked, enter the OBS WebSocket password (not your Riot password).
   Find it under OBS > Tools > WebSocket Server Settings > Show Connect Info.
   Password is held in memory only, not saved to disk.
4. Keep OBS and the VREC window open while you play VALORANT.
5. VREC starts recording when VALORANT's game process appears, and stops when it exits.
6. Ctrl+C in VREC stops its owned recording and exits.

KNOWN LIMITATIONS:
- No per-match detection, kill/death markers, clipping, GUI or startup-at-login yet.
- OBS must already be open, with Game Capture working and audio configured.
- If OBS is already recording when VALORANT starts, VREC does not interfere.
- If OBS restarts during gameplay, this prototype does not automatically resume capture.
- The console password entry accepts characters silently; paste is not supported.
- The project is provided as source; actual GPU/performance and function must be tested locally.
- Never disable Riot Vanguard or attempt to read VALORANT protected process memory.
