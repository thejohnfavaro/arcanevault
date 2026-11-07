# Arcane Vault for Windows v5.21.4

## Added
- A conservative 700 ms minimum interval between Scryfall live `/cards/collection` requests, including retries.
- Respect for Scryfall `Retry-After` response headers with a shared 60–1,800 second cooldown.
- Clear paused/cooldown status on Physical Collection valuation refresh.

## Changed
- `429 Too Many Requests` stops live lookups immediately instead of retrying repeatedly.
- Scryfall `400` and `403` access warnings now stop lookups and trigger a five-minute cooldown instead of being converted to retryable `502` errors.
- Only temporary service or network failures are retried; requests are paced, and partial successful price lookups remain cached.
- Full card catalog and printing updates continue using Scryfall bulk data instead of scanning individual cards.

## Fixed
- Repeated pricing retries could aggravate Scryfall rate limiting, making a physical collection valuation stop midway.
- Failed batches and cooldowns now report actionable status while preserving previously saved prices.

## Removed
- Rapid retries on Scryfall rate-limit and access-denial responses.
