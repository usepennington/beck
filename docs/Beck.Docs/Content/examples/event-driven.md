---
title: Event-driven orders
description: Synchronous intake, asynchronous fulfilment over a message bus.
order: 2
sectionLabel: Microservices
uid: examples.event-driven
---

An order is taken synchronously, then published to a bus that fans out to independent
consumers — payments, shipping, and notifications — each processing on its own. Note the dashed
`async` edges.

```yaml:symbol
wwwroot/examples/event-driven.beck.yaml
```

<beck-diagram src="/examples/event-driven.beck.yaml" mode="auto"></beck-diagram>
