# Thesis Guideline: Hybrid IR in Elasticsearch vs MongoDB

Oct 4, 2026 · @Someone

## The thesis in one paragraph

**Working title:** *Lexical, Semantic and Hybrid Retrieval in Elasticsearch and MongoDB Atlas Search: A Controlled Comparison*

The thesis asks whether hybrid retrieval (BM25 + dense vectors) finds more relevant articles than either method alone, and whether the answer depends on the engine. It answers with a controlled experiment: the same corpus, chunks, embeddings and queries run through 3 search modes on 2 engines, judged against a hand-built relevance set.

**What you deliver:**

1. A working .NET search platform that runs any query across both engines and all three modes.
2. A reusable evaluation harness: query set, graded relevance judgments, and scripts that compute Precision@K, Recall@K, MRR and nDCG@K.
3. Empirical results on relevance, latency, indexing time and storage, with statistical tests.
4. Practical guidance: when to choose which engine and which mode.

**The contribution** is not the system itself. It is the controlled design: it separates *engine effects* from *fusion effects* by running each engine's native hybrid next to an identical application-level fusion. Most published comparisons mix the two.

## Research questions and hypotheses

Four questions structure the whole thesis; each results chapter section answers exactly one.

| RQ | Question | Hypothesis | How it is answered |
| --- | --- | --- | --- |
| RQ1 | Does hybrid retrieval improve relevance over BM25 alone and vector alone? | H1: hybrid has higher nDCG@10 than both single modes on average | Mean nDCG@10, MRR, P@K, R@K per mode, paired significance tests |
| RQ1b | Does the benefit depend on query type? | H1b: BM25 wins on keyword and entity queries, vector on paraphrased natural-language queries, hybrid is most robust overall | Same metrics, broken down by the query-type labels |
| RQ2 | With identical data, embeddings and fusion, do Elasticsearch and MongoDB produce equivalent rankings? | H2: under app-level fusion, differences are small and not significant; under native hybrid they may differ | Metric deltas, rank overlap (Jaccard@10, Rank-Biased Overlap) |
| RQ3 | What does each mode cost on each engine? | H3: vector and hybrid add latency and storage; quantization reduces storage at a small recall cost | p50/p95 latency, indexing time, index size |
| RQ4 (optional) | How do chunk size and overlap affect vector and hybrid quality? | H4: an intermediate chunk size performs best | nDCG@10 across 2–3 chunking settings |

RQ1 and RQ2 are the core. RQ3 is cheap to collect alongside them. Do RQ4 only if time allows; it is the first thing to cut.

## Scope

The line is simple: anything that helps answer RQ1–RQ3 is in; anything that only makes the demo nicer is optional.

**In scope**

- BBC News corpus, English, chunked with overlap.
- One real embedding model (384 dimensions keeps your current indexes valid).
- BM25, vector and hybrid on both engines; hybrid in two variants (native and app-level).
- Article-level results (chunks collapsed with one shared rule).
- Hand-built query set with graded judgments.
- Relevance, latency, indexing time and storage measurements.
- A minimal UI: query, engine and mode selectors, TopK, side-by-side results.

**Optional (only if ahead of schedule)**

- RQ4 chunking experiment.
- A second embedding model for comparison.
- A public benchmark with existing judgments (e.g. a small BEIR dataset) to validate the pipeline.
- LLM-assisted relevance judgments, validated against your manual labels.

**Out of scope (name these as future work)**

- Book PDFs and PDF parsing.
- Rerankers and learning-to-rank.
- Production-scale load testing or cloud cost comparison.
- Multilingual retrieval.

**Assumptions to state in the thesis:** both engines run locally in Docker on the same machine with fixed resources; results describe relative behaviour, not production performance; the corpus is small enough that exact (brute-force) kNN is available as ground truth for ANN recall.

## System design

Your existing architecture is the right one; the change that matters is that every engine receives exactly the same chunks and vectors, and the API applies one shared fusion and collapsing logic.

&#91;embedded content: system architecture · ingestion, two engines, one API\]

The highlighted API is where the controlled comparison lives: app-level fusion and chunk collapsing run there, identically for both engines.

**Key design decisions to make and justify in the thesis**

- **Embedding model:** switch from the placeholder now. A 384-dimension English model such as `bge-small-en-v1.5`, `e5-small-v2` or `all-MiniLM-L6-v2` keeps your indexes unchanged. Justify the choice by retrieval benchmark scores, size and speed. Note any required prefixes (e5 expects `query:` and `passage:`).
- **Chunking:** choose a size in tokens (e.g. 200–300 with 15–20% overlap) and keep the title as a separate field on every chunk so BM25 can boost it.
- **Generate embeddings once:** store vectors with the chunk records and index the same values into both engines. Never re-embed per engine.
- **Index definitions:** keep mappings for both engines in version control; they go in the appendix.
- **Result contract:** one shared result type (article ID, score, rank, best chunk, engine, mode, latency) makes the comparison layer and the evaluation export trivial.
- **Run export:** every search can write a TREC-format run line; this feeds the evaluation scripts directly.

## Experimental design

The core experiment is 8 configurations, every one run on the same query set. Keep everything else fixed so any difference can be traced to one cause.

**Configuration matrix**

| ID | Engine | Mode | Fusion |
| --- | --- | --- | --- |
| ES-BM25 | Elasticsearch | BM25 | — |
| ES-VEC | Elasticsearch | Vector (kNN) | — |
| ES-HYB-N | Elasticsearch | Hybrid | Native (RRF retriever or linear retriever) |
| ES-HYB-A | Elasticsearch | Hybrid | App-level (same .NET code as MDB-HYB-A) |
| MDB-BM25 | MongoDB | BM25 (Atlas Search) | — |
| MDB-VEC | MongoDB | Vector ($vectorSearch) | — |
| MDB-HYB-N | MongoDB | Hybrid | Native ($rankFusion / $scoreFusion, if your version supports it) |
| MDB-HYB-A | MongoDB | Hybrid | App-level |

**Controlled variables (identical across engines)**

- Corpus, normalization, chunk size and overlap, chunk IDs.
- Embedding model and the exact vectors (generate once, store, index into both).
- Similarity metric (cosine, or dot product on normalized vectors).
- Quantization: off in both for the main run, or matched explicitly.
- Candidate pool: same `num_candidates` / `numCandidates` and same k.
- Searched fields and boosts (e.g. title × 2, body × 1) and phrase boosting.
- Chunk-to-article rule: article score = its best chunk's score; best chunk becomes the snippet.
- Retrieval depth before collapsing: fetch e.g. top 100 chunks, collapse, then cut to TopK articles.

**App-level fusion: implement two methods**

- **RRF:** score = Σ 1 / (k + rank), with k = 60 as the standard default. Rank-based, so no score normalization needed.
- **Weighted sum:** min-max normalize each list's scores to \[0, 1\], then α · vector + (1 − α) · BM25. Tune α on a small *development* query set (e.g. 10 queries) that is not used in the final test.

**Fairness rules to write into the methodology chapter**

- Native hybrid is reported as "what a developer gets out of the box"; app-level hybrid is the controlled comparison.
- Never tune parameters on the test queries.
- Report the exact engine versions, index settings and hardware.
- Check ANN recall: compare each engine's kNN top-k against exact brute-force top-k for the same query vectors. This tells you whether a relevance difference comes from the ANN index or from the method.

## Evaluation protocol

The evaluation is the heart of the thesis: plan for it to take as long as the engineering. Its output is a frozen query set, a judgment file (qrels) and one script that turns runs into tables.

**1. Build the query set (target 50 test + 10 development queries)**

Write queries across BBC's five categories (business, entertainment, politics, sport, tech) and label each with a type:

| Type | Example | Expected winner |
| --- | --- | --- |
| Keyword / entity | "Apple iPod sales" | BM25 |
| Natural-language question | "why are music companies worried about online downloads" | Vector |
| Paraphrase (no shared words with relevant articles) | "government plans to curb smoking in pubs" | Vector |
| Broad topic | "economic outlook in Europe" | Hybrid |

Aim for roughly equal numbers per type. Write queries *before* looking at search results, so they aren't biased toward one engine. Freeze the set and version it in git.

**2. Judge relevance with pooling**

1. Run all 8 configurations, take the top 20 articles from each.
2. Merge into one pool per query (duplicates removed, typically 40–80 articles).
3. Judge each pooled article in random order, without seeing which configuration found it: 0 = not relevant, 1 = partially relevant, 2 = highly relevant.
4. Write a one-page judging guideline first (what makes something a 2 versus a 1) and include it as an appendix.
5. Have a second person judge a random 20% subset; report agreement with Cohen's kappa.

Unjudged articles count as not relevant; say so explicitly.

**3. Optional: LLM-assisted judgments**

If you want more queries, use an LLM judge with the same guideline. Validate it against your manual labels on the overlapping subset and report the kappa. Use it only if agreement is reasonable (kappa ≥ 0.6 is a common bar).

**4. Metrics (report @5 and @10)**

| Metric | What it tells you | Uses grades? |
| --- | --- | --- |
| nDCG@10 | Overall ranking quality; the primary metric | Yes |
| MRR | How high the first relevant result appears | No (relevant = grade ≥ 1) |
| Precision@K | Share of top K that is relevant | No |
| Recall@K | Share of all known relevant articles found in top K | No |
| Jaccard@10, RBO | How similar two configurations' rankings are (for RQ2) | No |

Don't hand-write metric code if you can avoid it: `ranx` or `pytrec_eval` (Python) take a qrels file and run files and compute all of these plus significance tests. Export runs from .NET in TREC format (`qid Q0 docid rank score run_id`).

**5. Efficiency measurements**

- Latency: 5 warm-up runs, then each query 20–30 times; report p50 and p95 per configuration. Measure at the engine and end-to-end through your API separately.
- Indexing: total time to ingest and index the corpus, with embedding time reported separately.
- Storage: index size on disk per engine, for text-only and with vectors.
- Fix Docker CPU and memory limits and report them.

**6. Statistical testing**

Metrics are per query, so compare configurations with a paired test across queries: a paired t-test or Wilcoxon signed-rank test, or a randomization test. With several comparisons, apply a correction (Holm–Bonferroni). Report effect sizes, not just p-values; with 50 queries, small differences will often not be significant, and that is a valid finding.

## Work plan

Engineering finishes by week 6; from then on the thesis is evaluation and writing. The plan assumes about 4–5 months to submission, so stretch or compress the bars but keep the order and the gates.

&#91;embedded content: work plan · 7 workstreams, 4 freeze gates\]

The gates are freezes that protect the evaluation: after *pipeline frozen*, no engine, chunking or fusion changes; after *judgments frozen*, no query or relevance label changes; after *results frozen*, every number in the thesis comes from those runs.

**If you fall behind, cut in this order:** RQ4 chunking experiment, second embedding model, LLM-assisted judging, UI polish. Never cut the query set, the judging or the significance tests.

**Start writing early:** background (chapter 2) from week 3, system design (chapter 3) as you build, methodology (chapter 4) while judging. Only results and discussion wait until week 12.

## Thesis chapter structure

A typical length is 60–90 pages; check your faculty's rules. Start writing chapters 3 and 4 while building, not after.

| # | Chapter | What goes in it | Approx. share |
| --- | --- | --- | --- |
| 1 | Introduction | Motivation, problem, RQs, contributions, thesis outline | 5% |
| 2 | Background and related work | IR basics, BM25, dense embeddings, ANN and HNSW, hybrid fusion (RRF, weighted), chunking, IR evaluation metrics; how Elasticsearch and Atlas Search implement these (both on Lucene); prior benchmark studies | 20% |
| 3 | System design and implementation | Architecture, ingestion and chunking, embedding model choice, index mappings per engine, query construction per mode, fusion and collapsing logic, comparison layer, UI | 20% |
| 4 | Methodology | Experiment matrix, controlled variables, query set, judging process and agreement, metrics, latency protocol, statistical tests, threats to validity | 15% |
| 5 | Results | One section per RQ: tables and charts, significance, per-query-type breakdown, efficiency results | 20% |
| 6 | Discussion | Why results came out this way, failure analysis with concrete example queries, practical recommendations, limitations | 15% |
| 7 | Conclusion and future work | Answers to each RQ in one paragraph, PDFs, rerankers, other models | 5% |
| — | Appendices | Judging guideline, full query list, index mappings, configuration files, repo link | — |

**Strong material for the discussion chapter:** pick 5–8 queries where the modes or engines disagree most and walk through why (vocabulary mismatch, entity names, chunk boundaries, short vs long articles). Committees remember concrete examples.

## Risks and mitigations

The biggest risk is not technical: it is running out of time for evaluation and writing because engineering expands.

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Engineering and UI expand and eat evaluation time | High | Hard timebox per phase; UI stays minimal; PDFs are future work |
| Relevance judging takes longer than expected | High | Start judging early; cap pool depth at 20; 50 queries is enough; LLM judge only as a validated supplement |
| MongoDB native hybrid stage unavailable in Atlas Local version | Medium | App-level fusion is the main comparison anyway; report the native gap as a finding |
| Results show no significant differences | Medium | Still valid; frame it as "engines are equivalent under controlled conditions" and focus on per-query-type and cost differences |
| Hidden differences between engines (analyzers, quantization defaults) skew results | Medium | Document every setting; ANN recall check against exact kNN; small sanity tests per engine |
| Embedding model change late forces re-indexing and re-running everything | Medium | Switch to the real model now; keep ingestion scripted and repeatable |
| Single judge introduces bias | Medium | Written guideline, blind and randomized judging, second judge on 20% subset |
| Docker latency is noisy | Low | Fixed resource limits, warm-ups, repetitions, percentiles; claim relative not absolute performance |

## Checklists

Tick these off as you go; a phase is done only when its list is complete.

**Phase 1: Real embeddings**

- [ ] Embedding model chosen and justified (dimensions, language, licence, speed)
- [ ] Vectors generated once, stored, and indexed identically into both engines
- [ ] Exact kNN baseline available for ANN recall checks

**Phase 2: Hybrid on both engines**

- [ ] MongoDB native hybrid implemented (or its absence documented)
- [ ] App-level RRF and weighted fusion shared by both engines
- [ ] Same chunk-to-article collapsing rule in all 8 configurations
- [ ] α tuned on development queries only

**Phase 3: Comparison layer**

- [ ] `SearchEngine.Both` runs all configurations for one query
- [ ] Runs exported in TREC format with a run ID per configuration
- [ ] Overlap metrics (Jaccard@10, RBO) computed

**Phase 4: Evaluation set**

- [ ] Judging guideline written
- [ ] 50 test + 10 development queries frozen in git, each with a type label
- [ ] Pool judged blind; qrels file saved
- [ ] Second judge on 20% subset; kappa computed

**Phase 5: Experiments**

- [ ] All metrics per configuration, overall and per query type
- [ ] Significance tests with correction
- [ ] Latency, indexing time, storage recorded
- [ ] Failure-analysis queries selected

**Record for reproducibility (put in the appendix)**

- [ ] Engine versions (Elasticsearch, MongoDB / Atlas Local, mongot) and .NET version
- [ ] Index mappings and search index definitions
- [ ] Embedding model name and version, normalization, similarity metric
- [ ] Chunk size, overlap, and number of chunks produced
- [ ] HNSW and candidate parameters, quantization settings
- [ ] Hardware and Docker resource limits
- [ ] Git commit hash for every reported run
- [ ] Dataset source and preprocessing steps
