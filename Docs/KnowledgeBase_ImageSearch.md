# Knowledge Base — image search (StatsClient)

Vision-based scan matching runs **inside StatsClient** against StatsDB. There is no separate API service required.

## Requirements

- `Docs/SQL/KnowledgeBase_Schema.sql` deployed on StatsDB
- `Nvidia_API_KEY` in `dbo.Settings` (same key used elsewhere in StatsClient)

## How to use

1. Open **Knowledge Base** tab.
2. Under **Search by image**, set **Min match confidence** (50–99%). Default is **85%** to reduce false positives.
3. Drop a scan/prep body photo onto the drop zone.
4. Click **Find matching entries**.
5. Click a result to open that card.

Category and tag filters narrow which cards are compared. Text search does not affect image matching.

## Confidence slider

- **85% (default)** — strict; fewer false positives
- **70–80%** — more candidates when nothing matches at 85%
- **50%** — widest net; use when you expect a loose visual match

The setting is saved locally per PC (`KnowledgeBaseVisionMinConfidence`).

## Technical notes

- Uses `KnowledgeBaseVisionSearchService` + `NvidiaVisionService` (read-only vision call).
- Compares the query photo to up to 30 knowledge-base thumbnails (respecting active category/tag filters).
- NVIDIA returns confidence 0.0–1.0; results below your threshold are dropped client-side and in the prompt.

The optional `StatsClient.KnowledgeBaseApi` project (if present in the repo) is not part of the solution and is not required for normal use.
