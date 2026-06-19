---
title: Incident response
description: A metric breaches, an alert fires, and the on-call gets paged.
order: 9
sectionLabel: Operations
uid: examples.incident-response
---

The on-call story, top to bottom: metrics scrape quietly until a service spikes 5xx, a
threshold trips, and the alert fans out to every notification channel. This is the example for
*colour and drama* — a red `fail` shake on the failing service, `activate` lighting the
escalation path and keeping it lit (the page leg danger-red, the Slack and email legs amber), a
`burst` broadcasting to all three channels at once, and `status` pills carrying the incident
from `FIRING` to `ack ✓`.

```yaml:symbol
wwwroot/examples/incident-response.beck.yaml
```

<beck-diagram src="/examples/incident-response.beck.yaml" mode="auto"></beck-diagram>
