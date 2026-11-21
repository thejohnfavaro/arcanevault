# Arcane Vault Native Windows v0.9.171 (Preview)

## Added
- Post-response rendering pass: the final ChatGPT analysis is parsed into a new native WPF document containing formatted headings, readable paragraphs, prioritized swap tables and locally verified card-detail hyperlinks.
- Card names verified in the read-only Scryfall catalog become clickable directly during construction of the review, including in bold spans and table cells.

## Changed
- The original ChatGPT output remains intact for Copy review and is rendered progressively while streaming. Once the complete response is received, the final result is constructed from the full response with formatted card links in one pass.
- The existing AI Review workspace and separate full card details screen are retained; card navigation never writes to the deck or collection.

## Fixed
- AI Review showed `InvalidOperationException: Collection was modified; enumeration operation may not execute` and no card previews after an otherwise successful response.
- Removed mutation of WPF InlineCollection members while enumerating them to inject hyperlinks.
- Bold and italic card references in AI analysis can now include verified links without modifying a live rendered document.

## Removed
- The old post-render WPF link-injection code path. No user data, saved ChatGPT session, collection records, decks, proxies, sync data or backups are removed.

## Verification
- PR #240 passed the native Windows compilation and regression suite. The release pipeline verifies the portable bundle and installer.
- Visual card click/return behavior still needs hands-on verification on Windows. No new automatic AI requests are made by opening card details.
