---
title: Deploy pipeline
description: A build/test/scan/deploy pipeline with a failing stage.
order: 6
sectionLabel: Data pipelines
uid: examples.cicd
---

A left-to-right CI/CD pipeline with a scripted flow that tells a story: the build passes, then
the security scan fails with a red shake. Status pills and the `fail` step come from the
`flow` block.

```yaml:symbol
wwwroot/examples/cicd.beck.yaml
```

<beck-diagram src="/examples/cicd.beck.yaml" mode="auto"></beck-diagram>
