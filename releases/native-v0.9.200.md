# Arcane Vault Windows v0.9.200 (Preview)

## Added
- No new features. This is a compatibility follow-up for the v0.9.199 artwork retry change.

## Changed
- Image retry catches non-cancellation failures without referencing framework-specific exception types, preserving cancellation and diagnostic behavior.

## Fixed
- Removes exception-filter type dependencies introduced with v0.9.199 that could prevent certain Windows build configurations from compiling. This is a compile-compatibility mitigation; the exact CI failure still requires the failed job log to confirm.

## Removed
- Nothing. Existing saved deck art, collection records, arena match tracking, and widget statistics are unchanged.

## Verification
- Run the Windows native build and regression test workflow.
- Confirm custom art loads in Deck Library and XENEON EDGE after choosing a saved deck printing.
- Confirm that missing/damaged image requests show a diagnostic and can retry while an off-screen image request cancels safely.
