# Arcane Vault Native Windows v0.9.169 (Preview)

## Added
- Live stages in the embedded EDH Lab AI Review showing authorization check, model selection, request dispatch, receiving, and completion.
- Streaming, formatted review text appears incrementally as ChatGPT sends it rather than waiting for the entire response and the local card-link scan.
- Cancel request control with a three-minute request deadline, explicit empty-answer and incomplete-stream errors, and useful HTTP response status messages.
- Server-sent-event parsing supports multi-line data frames and a final completed-response output fallback.

## Changed
- Full AI review text is presented immediately on completion. Verifying locally indexed cards and decorating their names as links occurs afterward and cannot block the readable answer.
- Link enrichment is bounded to 75 candidate names and a 12-second timeout to prevent slow catalog reads from leaving AI Review blank.
- Interrupted or failed requests show the current stage and reason, retaining any partial text already received instead of silently clearing it.

## Fixed
- Analyze current deck could appear to do nothing while waiting for a response or before slow post-processing had completed, despite an existing ChatGPT connection.
- Empty successful or prematurely terminated response streams now produce a clear error rather than a blank review area.
- A stalled local catalog cannot hide an otherwise completed AI response.

## Removed
- Nothing. Existing ChatGPT account authorization, saved decks, owned cards, printings, proxies, sync data, and backups remain unchanged.

## Verification
- Native Windows WPF compilation and the native regression suite passed in pull request #238.
- The main-branch release pipeline validates bundle and installer packaging. A live connected ChatGPT request still requires on-device verification after installing the update.
- No automatic deck edits or extra AI uploads were introduced.
