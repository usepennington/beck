---
title: RAG answer pipeline
description: Embed a question, retrieve context, and answer — with a scripted flow.
order: 3
sectionLabel: AI & agents
uid: examples.rag
---

A retrieval-augmented generation pipeline, left-to-right. This one ships an explicit `flow` —
watch a packet travel from the user through embedding and the vector store to the model, then
back with the answer.

```yaml:symbol
wwwroot/examples/rag-pipeline.beck.yaml
```

<beck-diagram src="/examples/rag-pipeline.beck.yaml" mode="auto"></beck-diagram>
