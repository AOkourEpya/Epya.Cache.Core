# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

An out-of-process HTTP response cache for EPYA services. An ASP.NET Core app decorates controllers
with `[Cache]`, and GET responses are stored in a separate cache server process over raw TCP. What
makes it more than a key/value store is **entity-aware invalidation**: the server parses cached JSON
bodies, records which entities each response depends on, and drops exactly the affected entries when
the host app reports a data change.

The codebase was recently migrated from .NET Framework (System.Web.Http, `ConfigurationManager`,
`.nuspec`, `app.config`) to .NET 10. Comments referencing "the .NET 10 upgrade" mark the seams.
`README.md` is still an unfilled Azure DevOps template — ignore it. Not a git repository.

## Repository layout

```
Epya.Cache.sln              net10.0 (net10.0-windows for the WinForms host)
Directory.Build.props       LangVersion latest; Nullable + ImplicitUsings DISABLED; version pinning
Epya.Cache.Common/          shared contracts — referenced by all three other projects
  CacheBase.cs              base class host entities inherit; emits the `t` type token
  CacheRequest.cs           CacheRequest, CheckImpactRequest, EntityActionMetadata, Action enum
  CacheResponse.cs          ResponseCode ("200"/"404"/"500") + Body + Headers + error fields
  CacheMeta.cs              Timestamp + CacheKey, stored per cached entry
  CacheConfigDto.cs         binds "CacheSettings"; IsEnabled/IsConfigured gates
  EntityMetadataDto.cs      EF model snapshot: entity + ForeignKeyMetadataDto list
  CommonOperations.cs       IsValidJson / IsValidJsonAndHasT gatekeepers
  Extensions.cs             AddEpyaCache / UseEpyaCache / GetCacheConfigs + static Current
  Tcp/TcpManager.cs         the entire wire protocol, client and listener in one class
Epya.Cache.Client/          NuGet package consumed by the host web app
  CacheAttribute.cs         [Cache] — action filter (read) + result filter (write)
  CacheManager.cs           the four outbound calls + EF model walker
  TcpClientPool.cs          written, never wired up
Epya.Cache.Server/          OutputType=Library despite having Program.Main
  Engine.cs                 listeners, cacheReplica store, dependency scan, invalidation
  Program.cs                vestigial console entry point (unreachable — library output)
  ILogger.cs / FileLogger.cs / ConsoleLogger.cs / ClientContext.cs   dead subsystem, see below
WindowsFormsApp1/           the actual cache server host + live stats dashboard
  Program.cs                builds config + DI, runs Form1
  Form1.cs                  constructs Engine with replica count from a textbox; 100ms stats poll
```

Reference direction: `Common ← Client`, `Common ← Server ← WindowsFormsApp1`. Client and Server never
reference each other — they communicate only over TCP, and `Common` is the shared contract that keeps
their JSON compatible. **Changing anything in `Common` is a protocol change** affecting both sides plus
the two published packages.

## Build, run, package

```powershell
dotnet build Epya.Cache.sln -v minimal --nologo         # whole solution
dotnet build Epya.Cache.Client\Epya.Cache.Client.csproj # one project
```

**The solution build fails with `MSB3027` file-lock errors while WindowsFormsApp1 is running** — it
holds `Epya.Cache.Common.dll` / `Epya.Cache.Server.dll`. Close the app, or build the three library
projects individually, which is enough to verify almost every change. (`dotnet build` accepts only one
project per invocation — pass the `.sln` or run it once per project.)

There are **no test projects** in this solution. Verifying behaviour means running the WinForms host and
driving it from a client app; there is no `dotnet test` to fall back on.

Running the cache server: **`Epya.Cache.Server` is `OutputType=Library`** even though it contains a
`Program.Main`, so `dotnet run` on it does nothing. The real host is `WindowsFormsApp1` — it constructs
the `Engine`, starts it from the button handler, and polls replica counters on a 100 ms timer. Replica
count comes from the form's textbox, not from config.

Packaging (`Epya.Cache.Common` and `Epya.Cache.Client` are the shipped NuGet packages; `Server` is not):

```powershell
cd .\Epya.Cache.Client
dotnet pack -c Release
```

`<PackageId>`/`<Version>` live in each csproj — the `.nuspec` files are gone. Both packages are
versioned `1.2.3-1-beta` and should be bumped together. `NugetCmd.txt` in each project holds the exact
`dotnet nuget push` command and the EPYA DevOps feed URL. `AssemblyVersion`/`FileVersion` are
deliberately pinned to `1.0.0.0` in `Directory.Build.props` so existing binding redirects keep working —
do not bump those with the package version.

## Request lifecycle

Tracing one request end to end is the fastest way to understand the system; the logic is spread across
`CacheAttribute` → `CacheManager` → `TcpManager` → `Engine`.

**Cache miss (first GET):**
1. `CacheAttribute.OnActionExecutionAsync` — bails unless GET and `IsEnabledAndConfigured`.
2. Builds `CacheRequest { FullUrl, UserName, Action.Read }`.
3. `CacheManager.SendReadRequestAsync` → `TcpManager.SendAsync` → **read port 5001**.
4. `Engine._RecieveReadMessageAsync` → `_ProcessReadRequest` → `cacheMap` miss → `ResponseCode "404"`.
5. Filter falls through to `await next()`; the action runs normally.
6. `OnResultExecutionAsync` swaps `Response.Body` for a `MemoryStream`, reads the rendered body, copies
   it to the real stream, then tests `IsValidJsonAndHasT`.
7. Fire-and-forget `SendWriteRequestAsync` → **write port 5000**.
8. `Engine._HandleWriteAsync` → `_ScanJsonForDepandancy` → one guid written into six dictionaries **on
   every replica**.

**Cache hit:** steps 1–3 identical; `Engine` returns `"200"` with body + headers; the action filter
short-circuits with a `FileContentResult`, replays headers, adds `X-Cache-Header: true`, and sets the
`__Epya.Cache.ServedFromCache` HttpContext item so the result filter skips re-caching. **The action
never executes.**

**Invalidation:** the host app calls `SendCheckImpactRequestAsync` with a list of
`EntityActionMetadata { Type, Id, Operation, Fks }` → port 5000 → `Engine._HandleCheckImpactAsync` splits
it: `Delete`/`Update` drive `_HandleDirectImpact`, `Add`/`Update` drive `_HandleInDirectImpact`. Both
purge across all replicas, then `_CleanCachDepandancy` sweeps the dependency maps in the background.

## What the host application must provide

This repo is only half the system. A consuming app has to supply four things, none of which live here —
if caching "does nothing", check these first:

1. **Entities inherit `CacheBase`**, so serialized JSON carries `t`. Without it nothing is ever cached.
2. **`httpContext.Items["Username"]`** set by the app's own middleware — it is half the cache key, and
   falls back to the literal `"Unknown"`.
3. **`GenerateContextMetadata(dbContext)`** called at startup, or indirect invalidation silently no-ops.
4. **`SendCheckImpactRequestAsync` on save** — there is no EF `SaveChanges` interceptor in this repo;
   the host is responsible for detecting changes and reporting them.

## Architecture

### Transport

`Tcp/TcpManager.cs` is the entire protocol — one class serving as both client and listener. Both
directions send UTF-8 JSON terminated by a literal `<END>` sentinel; there is no length prefix, so a
body containing `<END>` would corrupt framing. Reads use a shared 64 KB `ArrayPool<byte>`, sockets set
`NoDelay` with 256 KB buffers, and `ThreadPool.SetMinThreads(200, 400)` is raised at listener start.
Every request opens a **fresh** `TcpClient`; connections are not reused.

**Two ports, split by intent** (`ReadPort` 5001, `OthersPort` 5000). The read port serves only
`Action.Read`; the other multiplexes `Write`, `CheckImpact` and `ExportMetadata`. `Engine` runs a
`TcpManager` listener on each and dispatches on `CacheRequest.Action`. The split exists so heavy write
and invalidation traffic cannot stall reads.

Only the **async** server path is wired up (`StartServerAsync` / `OnMessageReceivedAsync`). The sync
`_HandleClient` path writes an empty string back instead of the handler result, so don't switch to
`StartServer()` expecting it to work.

### The `t` property — the core convention

`CacheBase` computes an obfuscated encoding of its own `GetType().FullName` in its constructor and
exposes it as `T`. Host domain entities inherit it, so their JSON carries `"t"` alongside `"id"`.
Everything downstream keys off this:

- **Only responses whose JSON contains `t` are cached** (`IsValidJsonAndHasT`, checked in the result
  filter). Valid JSON without `t` is silently not cached; empty objects/arrays are rejected too. Note
  the check only inspects root-level object properties or array elements, not arbitrary depth.
- The server decodes `t` back to a type name to build dependency keys.

Encoding: FullName is lowercased, each letter becomes its 1–26 ordinal, `.` becomes `0`, and every
character emits a trailing comma (non-letters leave empty parts the decoder discards). Because the name
is lowercased first, **all type comparisons downstream are lowercase** — the metadata lookups in
`Engine` and `CacheManager` depend on it.

The decoder is duplicated: `Engine._DecodeTypeName` and `CacheManager.DecodeEntityTypeName` are the same
algorithm in two projects. Change one, change both.

### Cache keys and storage

Cache key is `FullUrl + ":" + UserName` (`CacheRequest.CacheKey`, `[JsonIgnore]` so it is recomputed
server-side rather than trusted off the wire).

Each `cacheReplica` holds six `ConcurrentDictionary` instances, indirected through a GUID: `cacheMap`
(cache key → guid), then `cacheData` / `cacheHeaders` / `cacheMeta` keyed by guid, plus
`directCacheDependances` / `indirectCacheDependances` (dependency key → comma-terminated guid list). The
guid indirection is what lets invalidation drop an entry knowing only its dependencies, and lets one
guid appear under many dependency keys without duplicating the body.

**Replicas are read fan-out, not sharding.** Every replica holds a full copy: writes and invalidations
apply to all, reads round-robin via `Engine._GetAndIncrement`.

There is **no TTL and no eviction**. Entries leave only through `CheckImpact` invalidation or
`StopServer`. Writes use `cacheMap.TryAdd`, so a repeat write for an existing key returns
`500 "The cache key already exists"` and `break`s out of the replica loop — there is no update path, and
that early break can leave replicas divergent.

### Entity-aware invalidation

The subtle part. The two dependency kinds must stay symmetric.

**Direct** — key `type|id`. Built at write time by `_ScanJsonForDepandancy` walking the response body:
every object carrying both `t` and `id`. Invalidated by `Update`/`Delete`. Meaning: "this response
embeds that entity."

**Indirect** — key `parentType|parentId|collectionNavigation|relatedEntityFullName|fkProperty`, all
lowercase. Built at write time when a cached object has an array property that EF metadata identifies as
a collection navigation, and either the array holds objects with `t`, *or* the array is empty but its
path appears in the request's `?include=` / `?includes=` query string. That second case is the reason an
empty collection still invalidates when a child is added later. Invalidated by `Add`/`Update`.

The trick: the key is built from the **parent's** perspective at write time, but reconstructed from the
**child's** FK at invalidation time via `ForeignKeyMetadataDto.InverseCollectionNavigation`. Both sides
must produce byte-identical lowercase strings or invalidation silently misses. Indirect invalidation is
a no-op when `ContextMetadata` is empty.

After entries drop, `_CleanCachDepandancy` sweeps both dependency maps with `Parallel.ForEach` over a
`Partitioner` to strip dead guids.

### EF Core metadata handoff

Indirect invalidation needs the host's entity graph. `CacheManager.GenerateContextMetadata(DbContext)`
walks `context.Model`, describing navigations and skip navigations (implicit many-to-many join
property-bag types are skipped) into `EntityMetadataDto`, and ships them once via `Action.ExportMetadata`.
A **static** `isMetaDataSent` flag guards resend — pass `force: true` after a model change. The server
persists the JSON to `ContextMetadata/context_metadata.json` beside the executing assembly and reloads it
in the `Engine` constructor, so metadata survives restarts.

EF Core pairs navigations directly via `Inverse`, which is why the inverse no longer has to be inferred
by matching FK names across entities the way the EF6 original did.

### Configuration

`CacheSettings` in `appsettings.json` binds to `CacheConfigDto` (`IsEnabled`, `ServerAddress`, `ReadPort`,
`OthersPort`, `LogFilePath`). Host apps wire up with `services.AddEpyaCache(configuration)` and
`app.UseEpyaCache()`.

Both calls also publish the config to the **static `Extensions.Current`**. That static is not incidental:
`CacheAttribute` is instantiated by the MVC runtime rather than the container, so it cannot take
constructor injection. It resolves per request through `httpContext.RequestServices.GetCacheConfigs()`,
which falls back to `Extensions.Current` when the provider has no registration. Same reason the WinForms
form and `CacheManager()`'s parameterless overload reach for it. **Prefer passing `CacheConfigDto`
explicitly wherever a container is at hand.**

`IsEnabledAndConfigured` gates every code path — with the section missing, every flag is off and the
cache no-ops, matching the pre-migration behaviour when `ConfigurationManager` found no section.

### The `[Cache]` filter's two halves

`CacheAttribute` implements both `IAsyncActionFilter` and `IAsyncResultFilter`; GET is the only method it
touches. The split is forced by ASP.NET Core: **the response body does not exist until the action result
has executed**, so reading and writing cannot happen in the same filter stage.

- **Read (action filter)**: on `"200"` it short-circuits with a `FileContentResult` (which sets an exact
  `Content-Length` for free), replays cached headers, adds `X-Cache-Header: true` plus an
  `access-control-expose-headers` entry. Headers in `NonReplayableHeaders` (`Content-Length`,
  `Content-Type`, `Transfer-Encoding`, `Connection`, `Keep-Alive`, `Upgrade`, `Date`, `Server`) are
  dropped — replaying them fights the values the live response computes and breaks connection framing.
- **Write (result filter)**: buffers via a `MemoryStream` swap, copies to the real stream in a `finally`,
  then fire-and-forgets the cache write. `__Epya.Cache.ServedFromCache` stops a body that just came out
  of the cache from being written straight back into it.

## Patterns used

- **Shared-contract assembly** — `Common` is the only thing both sides reference; the wire format is
  defined once as DTOs rather than duplicated.
- **DI extension-method pair** (`AddEpyaCache` / `UseEpyaCache`) with a **static service-locator
  fallback** for call sites the container cannot reach (attributes, WinForms, static helpers).
- **Filter pipeline split across two stages** to work around body availability, coordinated through an
  `HttpContext.Items` flag.
- **Guid indirection / inverted index** — dependency keys point at guids, guids point at data, so
  invalidation is a set lookup rather than a scan of cached bodies.
- **Full replication for read fan-out** — round-robin reads, broadcast writes.
- **Fire-and-forget side effects** — cache writes and all file logging are `Task.Run` so they never sit
  on the request path.
- **`ArrayPool<byte>` buffer pooling** in the TCP read loop; `ConcurrentDictionary` + `Interlocked`
  counters instead of locks; `Partitioner` + `Parallel.ForEach` for the dependency sweep.
- **Callback-based transport** — `TcpManager` exposes `Func<>` hooks (`OnMessageReceivedAsync`) rather
  than inheritance, so `Engine` wires two listeners from one class.
- **Sentinel framing** (`<END>`) rather than length-prefixing.

## Coding conventions

- Private methods are prefixed with `_` (`_HandleWriteAsync`, `_ScanJsonForDepandancy`). Public members
  are PascalCase; fields on `cacheReplica` are deliberately lowercase/public for direct dashboard access.
- Public API keeps its existing misspellings — `IsChacheEnabled`, `_BuildExecptionCacheResponse`,
  `directCacheDependances` — because they ship in a NuGet package. **Do not "fix" these names**; match
  the surrounding spelling in new code that touches them.
- `Nullable` and `ImplicitUsings` are **both disabled** in `Directory.Build.props`. Write explicit
  `using` directives; no nullable annotations. Files carry the full legacy `using` block (`System.Linq`,
  `System.Text`, `System.Threading.Tasks`) even when unused.
- **Newtonsoft.Json throughout**, not System.Text.Json. Bodies are typed `dynamic`.
- Errors are returned as data, not thrown: `CacheResponse.ResponseCode` is a **string** `"200"`/`"404"`/
  `"500"`, with `ErrorMessage` / `ErrorStackTrace` / `FullErrorInfo` alongside. Client-side helpers
  `_BuildExecptionCacheResponse` unwrap the full `InnerException` chain into `FullErrorInfo`.
- `try { ... } catch { throw; }` blocks appear throughout as placeholders for logging that was never
  added — several carry a bare `//write log` comment.
- Modern C# is allowed and used selectively (`ArgumentNullException.ThrowIfNull`, `using` declarations,
  local functions, tuple returns) but the bulk reads as pre-migration C#.

## Dead code and rough edges

Confirmed by grep; treat these as current state rather than bugs to fix unprompted:

- **The entire server logging subsystem is unused.** `ILogger`, `FileLogger`, `ConsoleLogger` and
  `ClientContext` are never instantiated — the only reference is a commented-out line at
  `Engine.cs:56`. `Engine` therefore has no logging at all, only `//write log` comments in catch blocks.
- `CacheManager._logExceptions` has **empty method bodies** — client-side exceptions are swallowed
  without trace. Start here when a client silently does nothing.
- `TcpClientPool` is fully implemented but **never wired up** (commented out in the `CacheManager`
  constructor), so the per-request `TcpClient` churn it was written to avoid is still there.
- `CacheManager.BufferPool` is declared but never rented from; only `TcpManager`'s pool is live.
- `Epya.Cache.Server/Program.cs` cannot run — library output type.
- `TcpManager._WriteTcpMessageAsync` null-checks `OnMessageSent` but then invokes `OnMessageSentAsync`;
  setting only the async callback would throw. Neither is currently assigned.
Two NREs sit on the write path. Both are swallowed by `_HandleWriteAsync`'s catch, which turns them into
a `"500"` response and an incremented `fail_writes` — so the symptom is "nothing gets cached and the
dashboard's fail counter climbs", not a crash:

- **`Engine._ExtractQueryString` returns null when the URL has no `?`**, and `_HandleWriteAsync`
  immediately calls `.Any()` on it. Every query-less URL therefore fails to cache.
- **`_ScanJsonForDepandancy` dereferences `tProperty` / `idProperty` before the null check** on the very
  next line, so any traversed object lacking `t` or `id` throws — which includes most nested plain
  objects inside an otherwise cacheable response.

If you are asked why the cache "isn't working", check these before anything else.
