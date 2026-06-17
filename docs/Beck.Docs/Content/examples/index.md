---
title: Microservices platform
description: An API gateway fronting three independent services.
order: 1
sectionLabel: Microservices
uid: examples.microservices
---

A request-driven backend: an API gateway in front of grouped services, with an asynchronous
event bus for fan-out. A good starting point for most service architectures.

The YAML below is the real file in this repo, embedded with a `:symbol` source fence — and the
diagram under it is that exact file, rendered live.

```yaml:symbol
wwwroot/examples/microservices.beck.yaml
```

<beck-diagram src="/examples/microservices.beck.yaml" mode="auto"></beck-diagram>

Open it in the [Playground](/playground) to tweak it, or browse the other examples in the
sidebar.
