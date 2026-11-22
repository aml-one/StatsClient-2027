# Graph Report - OrderCreation  (2026-06-12)

## Corpus Check
- 10 files · ~2,675 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 86 nodes · 131 edges · 13 communities (6 shown, 7 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f348cc01`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 12|Community 12]]

## God Nodes (most connected - your core abstractions)
1. `OrderCreationOrderXmlBuilder` - 17 edges
2. `OrderCreationGenerationService` - 11 edges
3. `XElement` - 11 edges
4. `OrderCreationConfigResolver` - 10 edges
5. `OrderCreationInput` - 6 edges
6. `OrderCreationPaths` - 6 edges
7. `OrderCreationEmbeddedAssets` - 5 edges
8. `OrderCreationOrderIdService` - 5 edges
9. `OrderCreationInput` - 4 edges
10. `OrderCreationIdFactory` - 3 edges

## Surprising Connections (you probably didn't know these)
- None detected - all connections are within the same source files.

## Import Cycles
- None detected.

## Communities (13 total, 7 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.23
Nodes (7): IEnumerable, OrderCreationGenerationResult, OrderCreationInput, OrderCreationMaterialsXmlBuilder, OrderCreationOrderXmlBuilder, string, OrderCreationGenerationService

### Community 1 - "Community 1"
Cohesion: 0.17
Nodes (4): Dictionary, XElement, OrderCreationConfigResolver, OrderCreationMaterialConfig

### Community 2 - "Community 2"
Cohesion: 0.33
Nodes (4): OrderCreationConfigResolver, OrderCreationInput, XDocument, OrderCreationRestorationInput

### Community 3 - "Community 3"
Cohesion: 0.32
Nodes (4): string, XDocument, OrderCreationEmbeddedAssets, Stream

### Community 7 - "Community 7"
Cohesion: 0.40
Nodes (3): OrderCreationInput, XDocument, OrderCreationMaterialsXmlBuilder

## Knowledge Gaps
- **18 isolated node(s):** `Dictionary`, `XElement`, `string`, `XDocument`, `Stream` (+13 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `OrderCreationOrderXmlBuilder` connect `Community 9` to `Community 2`, `Community 11`, `Community 4`?**
  _High betweenness centrality (0.022) - this node is a cross-community bridge._
- **What connects `Dictionary`, `XElement`, `string` to the rest of the system?**
  _18 weakly-connected nodes found - possible documentation gaps or missing edges._