using Beck.Authoring;

namespace Beck.Docs.Samples;

/// <summary>
/// Real, compilable <c>Beck.Authoring</c> samples. The docs embed these methods
/// verbatim through Pennington's <c>:symbol</c> source fences, so the C# shown on
/// the page is guaranteed to be exactly what compiles here — it can never drift.
/// </summary>
public static class AuthoringSamples
{
    /// <summary>An API gateway fronting grouped services, with an async event bus.</summary>
    public static string Microservices() =>
        new DiagramBuilder("Web Platform")
            .Direction(Direction.Tb)
            .Node("web", n => n.Title("Web App").Kind(NodeKind.User))
            .Node("gw", n => n.Title("API Gateway").Kind(NodeKind.Gateway))
            .Node("auth", "Auth Service")
            .Node("orders", "Orders Service")
            .Node("authdb", n => n.Title("Auth DB").Kind(NodeKind.Db))
            .Node("events", n => n.Title("Events").Kind(NodeKind.Queue).Subtitle("Message bus"))
            .Group("services", g => g.Label("Services").Members("auth", "orders").Accent(AccentToken.Primary))
            .Edge("web", "gw")
            .Edge("gw", "auth")
            .Edge("gw", "orders")
            .Edge("auth", "authdb")
            .Edge("orders", "events", e => e.Kind(EdgeKind.Async))
            .ToYaml();

    /// <summary>The simplest request path — emitted as a fenced <c>```beck</c> block.</summary>
    public static string RequestPath() =>
        new DiagramBuilder("Request Path")
            .Direction(Direction.Lr)
            .Node("client", n => n.Title("Browser").Kind(NodeKind.User))
            .Node("api", "API Server")
            .Node("db", n => n.Title("Postgres").Kind(NodeKind.Db))
            .Edge("client", "api")
            .Edge("api", "db", e => e.Label("query"))
            .ToFence();

    /// <summary>A request/reply interaction — the sequence-diagram builder.</summary>
    public static string CheckoutSequence() =>
        new SequenceDiagramBuilder("Checkout")
            .Participant("web", "Web App", NodeKind.User)
            .Participant("api", "Orders API")
            .Participant("db", p => p.Title("Postgres").Kind(NodeKind.Db))
            .Message("web", "api", "POST /orders")
            .Message("api", "api", "validate cart")
            .Message("api", "db", "INSERT order")
            .Reply("db", "api", "ok")
            .Reply("api", "web", "201 Created")
            .ToFence();

    /// <summary>A publish lifecycle — the state-diagram builder.</summary>
    public static string PublishLifecycle() =>
        new StateDiagramBuilder("Publish Lifecycle")
            .Direction(Direction.Lr)
            .State("review", "In Review", AccentToken.Warn)
            .State("published", "Published", AccentToken.Success)
            .Initial("draft")
            .Transition("draft", "review", "submit")
            .Transition("review", "draft", "reject")
            .Transition("review", "published", "approve")
            .Final("published")
            .ToFence();

    /// <summary>A hand-built domain model — the class-diagram builder.</summary>
    public static string OrderModel() =>
        new ClassDiagramBuilder("Order Model")
            .Class("entity", c => c.Name("Entity").Stereotype("abstract").Accent(AccentToken.Neutral)
                .Fields("Id: Guid", "CreatedAt: DateTimeOffset"))
            .Class("order", c => c.Name("Order").Stereotype("aggregate")
                .Fields("Status: OrderStatus", "Total: Money")
                .Methods("AddLine(sku, qty)", "Submit()"))
            .Class("line", c => c.Name("OrderLine").Fields("Sku: string", "Qty: int"))
            .Inherits("order", "entity")
            .Composition("order", "line", fromCard: "1", toCard: "*")
            .ToFence();

    /// <summary>An always-current domain model, reflected from real CLR types.</summary>
    public static string ReflectedModel() =>
        ClassDiagramBuilder
            .FromTypes(typeof(DiagramBuilder), typeof(SequenceDiagramBuilder), typeof(StateDiagramBuilder), typeof(ClassDiagramBuilder))
            .Title("Beck.Authoring builders")
            .ToFence();

    /// <summary>A cache-aside read path with a scripted animation flow.</summary>
    public static string ReadPath() =>
        new DiagramBuilder("Read Path")
            .Direction(Direction.Lr)
            .Node("client", n => n.Title("Client").Kind(NodeKind.User))
            .Node("api", "API")
            .Node("cache", n => n.Title("Redis").Kind(NodeKind.Cache))
            .Node("db", n => n.Title("Postgres").Kind(NodeKind.Db))
            .Edge("client", "api")
            .Edge("api", "cache")
            .Edge("api", "db")
            .Flow(f => f
                .Repeat(-1)
                .RepeatDelay(1.5)
                .Packet("client", "api", label: "GET /item")
                .Parallel(p => p
                    .Packet("api", "cache", color: "warn")
                    .Working("db"))
                .Status("cache", "miss", color: "warn")
                .Packet("api", "db", label: "SELECT")
                .Idle("db")
                .Packet("db", "api", color: "success")
                .Wait(1))
            .ToFence();
}