# Arcane Vault Native Windows v0.9.176 (Preview)

## Added
- Regression checks for the overlapping card names Fell and Fell the Profane, including modal double-faced card identities.

## Changed
- AI Review now recognizes the complete front-face name of a double-faced card as a catalog-verified alias, while linking to its canonical complete card details.

## Fixed
- Prevents the shorter card Fell from capturing the beginning of Fell the Profane when the review references the latter's front face.
- Corrects the native preview publication failure caused by attempting to reuse the already published v0.9.175 release tag.

## Removed
- Nothing. Saved decks, ownership data, synchronization data, and existing releases remain untouched.

## Verification
- The previous v0.9.175 workflow compiled Windows successfully and passed catalog checks, but publication failed because that tag belonged to an earlier commit.
- Verify the v0.9.176 workflow completes successfully and test the card link in a live review before treating the fix as verified.
