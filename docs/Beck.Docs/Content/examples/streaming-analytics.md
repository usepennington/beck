---
title: Streaming analytics
description: Clickstream ingest, windowed processing, and a live dashboard.
order: 7
sectionLabel: Data pipelines
uid: examples.streaming-analytics
---

A real-time analytics pipeline: a clickstream firehose lands in Kafka, a stream processor
windows it, and results fan out to a warehouse and a live dashboard — with bad events shunted
to a dead-letter queue. It shows off the *traffic* animations: `burst` for the firehose of
small dots, a single `packet` traced end-to-end through the collector and bus with `via`, and
`stream` for the continuous flowing dashes on the hot paths. A planned dead-letter **replay**
consumer rides along as a `ghost` node — there in the design, not yet built.

```yaml:symbol
wwwroot/examples/streaming-analytics.beck.yaml
```

<beck-diagram src="/examples/streaming-analytics.beck.yaml" mode="auto"></beck-diagram>
