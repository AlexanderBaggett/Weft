# Effect scopes (tentative)

Blocks that change what is *allowed* or *automatic* inside them. This is effect typing
in service of the web tier: the compiler knows which calls do I/O, which are idempotent,
and which are external, and enforces the combinations that discipline usually fails to.

**Status:** less settled than the rest of the design. Included because the checks fall
out of machinery the language already has.

## Call classifications

Functions and service methods may declare:

```csharp
idempotent  Order Find(Id<Order> id);         // safe to retry
external    void Send(Email e);               // leaves the process; not transactional
pure        int Total(Order o);               // no I/O, no ambient reads
```

Built-in origin sends and stdlib I/O come pre-classified. Unclassified calls are treated
as non-idempotent, non-external, impure — the conservative case.

## `transaction`

```csharp
transaction
{
    Db.Write(order);
    Db.Write(ledgerEntry);
    Email.Send(receipt);                  // error: external call inside transaction
}
```

Inside the block, `Db` (and any service declared `transactional`) enlists automatically.
`external` calls are errors — send after commit, or use an outbox:

```csharp
transaction
{
    Db.Write(order);
    Outbox.Send(orders.created, evt);     // Outbox is transactional; delivered after commit
}
```

## `deadline`

```csharp
deadline 200ms
{
    var quote = Pricing.Quote(item);
}
```

Narrows the `Deadline` ambient for the block. All I/O inside honors it. On expiry the
block raises `Timeout`. A `deadline` longer than the ambient one is a warning, not an
extension.

## `retry`

```csharp
retry(3, backoff: Exponential(50ms))
{
    Inventory.Reserve(item);              // error unless Reserve is `idempotent`
}
```

`retry` is only legal over calls the compiler can prove idempotent. That single rule
removes the double-charge class of bug.

## `compensate` (sketch)

```csharp
saga
{
    step  Inventory.Reserve(item)  compensate Inventory.Release(item);
    step  Payment.Charge(card)     compensate Payment.Refund(card);
    step  Shipping.Book(order);                                    // error: no compensation
}
```

Every step needs a compensation unless marked `final`. Failure runs compensations in
reverse. The compiler checks completeness; the runtime handles execution.

## Open questions

- Should classifications be inferred (from the body) or declared? Declared is simpler
  and honest about the boundary; inferred is less noise.
- Is `transaction` tied to `Db` only, or is `transactional` a service modifier?
- Interaction with `Stream` reject semantics (halt partition) — retry inside a stream
  handler probably needs to be the origin's retry, not the block's.
