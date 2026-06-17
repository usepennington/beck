---
title: Nested cloud topology
description: Account, VPC, and tiers as boundaries nested to any depth.
order: 5
sectionLabel: Cloud & infra
uid: examples.nested-groups
---

Groups nest: an AWS Account contains a VPC, which contains a Web Tier and a Data Subnet, each
with its own nodes. Beck lays each boundary out on its own and feeds it to its parent as a
single sized super-node, so the boxes nest cleanly and span ranks.

```yaml:symbol
wwwroot/examples/nested-groups.beck.yaml
```

<beck-diagram src="/examples/nested-groups.beck.yaml" mode="auto"></beck-diagram>
