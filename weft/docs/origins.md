# Origins

An origin is where values and messages come from. Origins are **language features**, not
library: the compiler needs each origin's envelope shape, address grammar, and outcome
semantics to refine `msg`, validate addresses, and check outcome exhaustiveness.

## What an origin contributes

| Piece | Purpose |
|---|---|
| **Address grammar** | How receivers bind (`Http.Post("/x/{id}")`, `Queue(topic)`, `Timer(every: 5m)`). Parsed and validated at compile time. |
| **Envelope** | The shape of `msg` when refined by `from X`. |
| **Outcome map** | What `respond` and each `reject Outcome` do on this transport. |
| **Ambients** | Context the origin makes available (`Connection`, `Partition`, `Attempt`). |
| **Adapter** | The runtime driver (Kestrel, Kafka, …). Configured, not coded. |

## Built-in inbound origins

Origins a receiver can bind to.

### Http

```csharp
on Http.Get("/orders/{id}")            // {id} must bind to a parameter — checked
on Http.Post("/orders")
on Http.Any("/health")
```

Envelope: `Method`, `Path`, `Query`, `Headers`, `Cookies`, `RawBody`, `Body<T>`.
Outcomes: `Unauthorized→401`, `Forbidden→403`, `NotFound→404`, `Invalid→400`,
`TooMany→429`, `Conflict→409`, `Internal→500`; `respond→reply`.
Compile-time: route conflicts, parameter binding, verb/route pairs unique per pipeline.

### Ws (WebSocket)

```csharp
on Ws("/live").Open
on Ws("/live").Frame                   // msg.Body : Frame<T> under ParseJson<T>
on Ws("/live").Close
```

Envelope: `Connection`, `Frame`. Provides ambient `Connection`. Stateful: the three
bindings on one route share a connection lifetime. `respond` sends a frame;
`reject Unauthorized` closes with a code.

### Grpc

```csharp
on Grpc.Orders.Create                  // address from the schema; envelope typed by it
```

Envelope from the `.proto` (or Weft-native contract). Outcomes map to gRPC status codes.

### Queue

```csharp
on Queue(orders.create)                // typed address, see below
on Queue(orders.create, group: "billing")
```

Competing consumers, at-least-once. Envelope: `Key`, `Attempt`, `Headers`, `Body`,
`EnqueuedAt`. Outcomes: `respond→Ack`; `reject Invalid→DeadLetter`,
`reject TooMany→Retry(after)`, `reject *→Nack`. Provides `Attempt`.

### Stream

```csharp
on Stream(orders, group: "billing")
on Stream(orders, from: Offset.Earliest)
```

Partitioned, ordered, offset-based, replayable. Envelope: `Partition`, `Offset`, `Key`,
`Body`, `Timestamp`. Outcomes: `respond→Commit`; `reject→Halt(partition)` by default —
streams don't skip. Provides `Partition`.

### Channel

```csharp
on Channel(orderEvents)
```

In-process, typed, bounded. Covers thread-to-thread handoff — a thread handing work to
another thread *is* a channel, and naming it that makes backpressure explicit.
Envelope: `Body`, `Sender`. Fire-and-forget by default; `respond` with a body is an
error unless the channel is declared `request-reply`.

### Timer

```csharp
on Timer(every: 5m)
on Timer(cron: "0 3 * * *")            // validated at compile time
on Timer(at: 03:00, tz: "America/Chicago")
on Timer(after: 30s, once: true)
```

Envelope: `ScheduledAt`, `FiredAt`, `Overrun`. Outcomes: `reject→Log+Skip`;
`reject TooMany→Backoff`. Overlapping runs are prevented unless `concurrent: true`.

### Tcp / Udp

```csharp
on Tcp(9000, framing: LengthPrefix<u32>).Frame
on Udp(9001).Datagram
```

Framing is required for TCP and is a compile-time contract. Envelope: `Connection`,
`Bytes`, `Remote`. Provides `Connection`.

### Signal

```csharp
on Signal.Start
on Signal.Stop                         // graceful shutdown; deadline ambient provided
on Signal.Reload
on Signal.Sigterm
```

Process lifecycle and OS signals as messages, so shutdown logic gets the same middleware
and rules as everything else.

### Watch

```csharp
on Watch("config/*.toml")
```

Filesystem change events. Envelope: `Path`, `Change` (`Created | Modified | Deleted`).

## Built-in source-only origins

Stamp provenance; never receive.

```
Db, Cache, Config, Env, Clock, Random
```

Used by rules (`forbid Log` on `from Env`) and by test rewiring (`Clock`, `Random` can be
substituted per scope in `weft.toml`).

## Typed addresses

Addresses are declared, so producers and receivers agree on the payload type.

```csharp
topic   orders.create : CreateOrder    on Queue;
topic   orders        : OrderEvent     on Stream(partitions: 12, key: OrderId);
channel orderEvents   : OrderEvent     (capacity: 1024, full: Backpressure);
channel priceQuotes   : Quote          (capacity: 1, full: DropOldest);

Queue.Send(orders.create, cmd);        // cmd : CreateOrder — checked
Queue.Send(orders.create, "oops");     // compile error
Channel.Send(orderEvents, evt);        // evt : OrderEvent — checked
```

Checks: topic with no receiver (warning), receiver on undeclared topic (error), payload
mismatch (error). The consuming side's provenance is exact: `CreateOrder from Queue`.

HTTP routes are not "typed addresses" in this sense — the route *is* the declaration, and
the receiver parameter type is the payload type.

## Outcomes

Symbolic, origin-independent results that each origin maps:

```
Ok, Created, Accepted, NoContent,
Invalid, Unauthorized, Forbidden, NotFound, Conflict, TooMany, Gone,
Internal, Unavailable, Timeout
```

Outcomes carry an optional `Problem` (RFC 7807-shaped, stdlib). Custom outcomes can be
declared and must then be mapped by every origin that can raise them.

## Custom origins

Declare the same pieces built-ins have.

```csharp
origin Mqtt : adapter MqttNet
{
    address  topic: string, qos: 0..2;
    envelope { string Topic; byte[] Payload; bool Retained; }
    outcomes match { respond => Ack; Invalid => Drop; * => Nack; }
    provides Qos;
}
```

Custom origins get the same compiler treatment: address validation (against the declared
grammar), envelope refinement, outcome exhaustiveness, and ambient availability.
