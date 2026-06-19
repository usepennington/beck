---
title: Zero-trust access
description: Every request is authorised before it reaches a protected service.
order: 10
sectionLabel: Cloud & infra
uid: examples.zero-trust
---

A request runs the gauntlet: a WAF, then an API gateway that checks with a policy engine
before the **allowed path lights green** into the protected zone — and a second, malicious
request that the WAF blocks outright. It shows per-node colour overrides (`surface` and
`textColor` give the secrets vault its hardened dark card), a bidirectional, S-curved
`authorize` edge (`arrow: both` + `curve: s`), `activate` to keep the approved route
highlighted, and a `fail` beat for the blocked attempt. Watch the flow, too: a hollow `ring`
packet carries the policy decision, `working` breathes on the service, and a `parallel` step
fans out to the vault and database at once.

```yaml:symbol
wwwroot/examples/zero-trust.beck.yaml
```

<beck-diagram src="/examples/zero-trust.beck.yaml" mode="auto"></beck-diagram>
