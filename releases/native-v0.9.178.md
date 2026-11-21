# Arcane Vault Native Windows v0.9.178 (Preview)

## Added
- Executed regression coverage for the explicit AI card reference regex, including ordinary words such as Brawl and a marked Fell the Profane reference.

## Changed
- Xeneon Edge snapshot export prioritizes initial deck and match data before optional commander artwork and set progress work.
- Initial widget refresh runs before the potentially expensive Arena log scan; subsequent refreshes continue tracking matches.
- AI review link parsing uses the shared tested domain regex instead of initializing a separate WPF-only compiled expression.

## Fixed
- AI Review crash showing `The type initializer for ArcaneVault.Next.Desktop.AiReviewCardLinks threw an exception` caused by over-escaped regex syntax.
- Delayed Xeneon Edge data refresh associated with synchronous log processing and auxiliary work ahead of the populated snapshot.

## Removed
- Redundant WPF-only card-link pattern initialization. Collection data, saved decks, and matches are unchanged.

## Verification
- The CI regression suite now executes the card-reference pattern to catch runtime regex initialization failures. Verify live Windows AI review and Xeneon Edge startup refresh timing after this release completes.
