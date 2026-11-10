# Arcane Vault Next — Planning Pack

**Draft date:** 2026-11-10. **Status:** RFC/proposal only. **No implementation changes included.**

Arcane Vault Next is a **Windows-first native migration with an offline Android companion**. Reliability and data integrity are the first priority, measurable performance is the second, and camera-based physical MTG card scanning is the Android companion's flagship new feature.

## Documents

1. [TECHNICAL_SPEC.md](TECHNICAL_SPEC.md) — existing repository architecture, target modules, authoritative data schema, safe legacy migration, conflict-aware sync v2, performance budgets, Android scanner specification, and release gates.
2. [MIGRATION_ROADMAP.md](MIGRATION_ROADMAP.md) — ordered phases, dependencies, deliverables, acceptance gates, estimated capacity and the initial AVN backlog.
3. [FEATURE_PARITY_AND_QA.md](FEATURE_PARITY_AND_QA.md) — desktop/companion scope matrix, golden-data tests, scanner QA, benchmark fixtures and migration reconciliation worksheet.

## Proposed first milestone

**Phase 0: data-integrity and performance audit.** Baseline current releases, enumerate real persisted data sources, produce a verified backup/restore test, and investigate the .NET 10 LTS upgrade for the existing Windows host. **No data migration or new authoritative DB is installed during this milestone.**

This planning PR should be reviewed before enabling any native cutover, two-way sync or scanner writes. The current Windows and Android apps remain the production versions until separate release checks pass.

## Repository and external references

- Windows: https://github.com/thejohnfavaro/arcanevault
- Android: https://github.com/thejohnfavaro/arcanevaultmobile
- Microsoft .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- WPF control performance/virtualization: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls
- ML Kit text recognition: https://developers.google.com/ml-kit/vision/text-recognition/v2/android
- CameraX architecture: https://developer.android.com/media/camera/camerax/architecture
- Scryfall rate guidance: https://scryfall.com/docs/faqs/i-m-having-trouble-accessing-the-scryfall-api-or-i-m-blocked-17
