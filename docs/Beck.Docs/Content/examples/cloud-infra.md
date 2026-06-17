---
title: Edge to cluster
description: Browser to CDN to load balancer to a Kubernetes ingress.
order: 4
sectionLabel: Cloud & infra
uid: examples.cloud-infra
---

A typical edge-to-cluster path with named infrastructure icons — CDN, load balancer, ingress,
pods, and object storage — plus a dashed origin-pull edge from the CDN to the bucket.

```yaml:symbol
wwwroot/examples/cloud-infra.beck.yaml
```

<beck-diagram src="/examples/cloud-infra.beck.yaml" mode="auto"></beck-diagram>
