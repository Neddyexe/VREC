# Detection choice and research status

The prototype only observes the VALORANT process. Process/window presence is useful for GAME_CLOSED and for restricting capture to the foreground game; it does not establish that a match began.

The implementation uses visible desktop pixels captured through Windows screen-copy APIs. It does not use protected memory, DLL injection, game logs, local client credentials, undocumented endpoints, gameplay input or Vanguard interaction. It provides no overlay or live competitive information.

Official Riot documentation sources to review before distribution:

- https://developer.riotgames.com/docs/valorant
- https://developer.riotgames.com/policies/general
- https://developer.riotgames.com/apis#val-match-v1

Attempts to fetch the first two sources from this cloud environment on 2026-10-20 returned HTTP 403. Current policy text was therefore not verified. No official live match-boundary API has been established in this work. Match history must not be presumed to provide timely live start/stop signals. No local client endpoint is assumed permitted merely because it is technically accessible. This implementation makes no claim of Riot approval or independently verified policy compliance; current applicable policies need review before distribution.

All eight requested game states are represented plus Unknown. Their visual recognition depends on supplied templates; there are no invented universal signatures. Unknown is a first-class observation that preserves recording. Supported modes cannot be listed as verified until real captures are tested. UI template matching is experimental and does not establish exact match boundaries.

Priority for the next Windows session: collect representative labeled screenshots and sequences across the user's modes, calibrate templates, measure start/stop error and false-positive rate, then tune regions/thresholds using held-out samples. Store only intentionally reviewed fixtures without personal identifiers. Add regression fixtures for menus, loading, round transitions, results, surrender and disconnect/reconnect before calling detection reliable.
