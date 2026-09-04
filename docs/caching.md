# Caching

Caching is cross-cutting because the decision to cache lives at a read boundary, but
correctness depends on every write anywhere in the program. That dependency is what a
compiler can see and a class can't. Weft makes invalidation a checked property.

## Anatomy

```csharp
cache OrderById
{
    scope   project;
    target  boundary service OrderService.Find;      // what gets memoized
    filter  args[0] is Id<Order>;                     // optional
    key     args[0];                                  // cache key from arguments
    ttl     5m;                                       // optional; absent = until invalidated
    effect  invalidate on Db.Write(Order), Db.Delete(Order) by .Id;
              invalidate on Stream(orders)             by .OrderId;
}
```

Targets: `boundary service X.Y` (memoize a method), `Http.Send` (response caching —
key defaults to route + query + `Principal` if required).

## Checks the compiler performs

- **Stale-cache proof.** Every write site for the cached type — `Db.Write`, `Db.Update`,
  `Db.Delete`, receivers on typed topics/streams carrying it — must appear in an
  `invalidate on` clause. Otherwise a warning names the write site:
  *"Order written at OrderService.MarkPaid; cache OrderById not invalidated."*
- **Key adequacy.** If the target `requires Tenant` (or any ambient marked
  `partitioning`), that ambient is part of the key automatically, and omitting it
  explicitly is an error. Cross-tenant cache leaks don't ship.
- **Key derivability.** `by .Id` on the invalidation side must map to the key expression
  on the read side. Mismatched types are an error.
- **Provenance survives.** A cached `Order` is still `Order from OrderService`; downstream
  rules fire exactly as on a miss.

## Honest gap

Writes the compiler cannot see — other processes, other languages, DBA scripts — are not
covered. Typed topics and streams cover the visible cross-process case. `ttl` is the
backstop for the rest, and a cache with no `ttl` and no external invalidation source
emits an informational diagnostic.

## Opt-out

To deliberately not invalidate on a known write:

```csharp
    effect  invalidate on Db.Write(Order) by .Id;
            ignore Db.Update(Order.Status);          // status changes don't affect what Find returns
```

`ignore` is per-site and per-field so the check stays live for everything else.
