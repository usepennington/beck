---
title: AI agent loop
description: An agent plans, calls tools in parallel, then synthesises an answer.
order: 8
sectionLabel: AI & agents
uid: examples.agent-loop
---

An agentic assistant: the agent takes a question, fans out to its tools **in parallel** —
web search, a code runner, and a vector store — gathers the results, then synthesises an
answer with the model. This one leans on the animation vocabulary: `working`/`idle` to keep a
node visibly busy, a `parallel` block so the three tool calls fire at once, hollow `ring`
packets that `decelerate` into an `impact` burst on arrival, and results travelling back green
along the same edges.

```yaml:symbol
wwwroot/examples/agent-loop.beck.yaml
```

<beck-diagram src="/examples/agent-loop.beck.yaml" mode="auto"></beck-diagram>

Open it in the [Playground](/playground) to retime the loop or swap the tools.
