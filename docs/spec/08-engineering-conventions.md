# 08 — Engineering Conventions

Layout, solution setup, design, code conventions, naming, error handling, testing,
enumerated values, content, logging, version control, dependencies, automated gates and
platform constraints.

**Prerequisite:** `00-overview.md`, section 1.

**Base convention: Microsoft's .NET guidelines.** This document records only the
**deltas** — where the project's conventions sit on top of, or override, the .NET
defaults. Anything not addressed here follows standard .NET practice.

**Nothing in this document is a suggestion.** Every design and code choice below is
fixed so that the implementer, human or agent, decides none of them. Where a choice is
not covered here or by another chapter, the implementer stops and reports rather than
choosing (D-149).

**Root namespace:** `Janus`. Settled — the library is internal to the company's own
projects and is not published publicly, so no registry check applies.

---

## 1. Project layout

**CONV-LAYOUT-001** — The solution SHALL be organised as **one project per internal
area**, published as a single package.

| Project | Contains | Depends on |
|---|---|---|
| `Janus.Core` | Contracts, abstractions, the public surface | nothing |
| `Janus.Identity` | Accounts, organizations, memberships | Core |
| `Janus.Authentication` | Factors, sessions, flows | Core |
| `Janus.Authorization` | Grants, model builder, gate | Core |
| `Janus.Privacy` | Purposes and bases, consent and objection, legal documents, rights queue, erasure and export (chapter `04`) | Core |
| `Janus.Storage` | EF Core, Dapper, migrations; implements the persistence ports the areas declare; the OIDC provider's stores | Core, Identity, Authentication, Authorization, Privacy (D-149) |
| `Janus.Hosting` | Endpoints, middleware, wiring, the hosted background worker (INF-BG-001), the default mail and SMS transports and the mail-server adapter (chapter `05`, LIB-EXT-001 defaults) | all |
| `Janus.Conformance` | The conformance suite a host runs (LIB-TEST-001); the one further project with public types, shipped as its own package | Core, Hosting |
| `Janus.Analyzers` | The Roslyn analysers the gates rely on (CONV-CODE-008); targets `netstandard2.0` as analysers must, the one exemption to CONV-SETUP-001 AC1 | nothing (D-149) |
| `Janus.Cli` | Bootstrap and key rotation | Core, Storage, Identity, Authentication — it creates the first organization, administrator, enrolment link and the credential-less `emergency` account; **never the break-glass credential** (OPS-BOOT-001, D-133); it also carries the resumable key-encryption-key rotation of OPS-SEC-003, run under the maintenance credential (D-147) |

Dependencies point **inward toward Core**. Nothing points outward. Storage is the one
project that depends on the four area projects: it implements their persistence ports
(CONV-DESIGN-003), and no area depends on Storage. The OIDC provider (`02` section 8) is
the `Oidc` feature of `Janus.Authentication` with its stores in `Janus.Storage`.

*Source: LIB-PKG-001, LIB-PKG-002, D-147, D-149*

Separate projects make the boundaries a compiler concern rather than a review
concern. Under a single project with folders, LIB-PKG-001's acceptance criteria
could only be met by adding an architecture-test tool to simulate what the compiler
does for free.

**Acceptance criteria**
1. Each area compiles without reference to another area's internals.
2. `Janus.Core` compiles with no database dependency.
3. A dependency pointing outward fails the build.

---

**CONV-LAYOUT-002** — Public types SHALL exist **only in `Janus.Core` and
`Janus.Hosting`**. Types in other projects SHALL be internal.

`Janus.Hosting` is public because the middleware pipeline and its ordering are part of
the public contract (LIB-API-001, BFF-OWN-003) — a host must be able to mount it.
Public in that project are exactly the mounting types and the `AddJanus` registration
entry point (CONV-DESIGN-007); request and response DTOs are `internal sealed record`,
their wire shape being the contract, not their type.

*Source: LIB-API-001, LIB-API-002*

Makes the public surface reviewable by reading one project.

**Acceptance criteria**
1. A public type outside `Janus.Core`, `Janus.Hosting`'s mounting types and
   `Janus.Conformance` fails the build (CONV-SETUP-003). The only `InternalsVisibleTo`
   grants permitted are: every non-Core project to its own test project; each area
   project to `Janus.Storage` (persistence ports), to `Janus.Hosting` and to `Janus.Cli`
   (service registration); `Janus.Core` and `Janus.Storage` to `Janus.Hosting` and to
   `Janus.Cli` (registration). Any other grant fails the build (D-135, D-149).
2. The public surface is enumerable from `Janus.Core` plus the mounting types in
   `Janus.Hosting`.

---

**CONV-LAYOUT-003** — A file's namespace SHALL match its folder path. One public
type per file, named for the file.

**Acceptance criteria**
1. No file declares a namespace inconsistent with its location.

---

## 1a. Solution setup

**CONV-SETUP-001** — The solution SHALL target the current long-term-support .NET
release and its C# version, set once in `Directory.Build.props`: at the time of writing
.NET 10 (LTS, supported to November 2028) and C# 14. Every project SHALL inherit the
following from that file and SHALL NOT override them: `Nullable` enable, `ImplicitUsings`
disable, `TreatWarningsAsErrors` true, `AnalysisLevel` latest-all,
`EnforceCodeStyleInBuild` true, `Deterministic` true, `GenerateDocumentationFile` true.

*Source: D-149*

**Acceptance criteria**
1. No project file sets a target framework or any of the listed properties, except
   `Janus.Analyzers`, which sets `netstandard2.0` (CONV-LAYOUT-001).
2. A warning of any analyser fails the build.

---

**CONV-SETUP-002** — Package versions SHALL be managed centrally in
`Directory.Packages.props` (Central Package Management) with a committed lockfile per
project (`RestorePackagesWithLockFile`); restore SHALL run in locked mode in the pipeline.

*Source: CONV-DEP-001, D-149*

**Acceptance criteria**
1. No project file carries a package version.
2. A restore whose resolved set differs from the lockfile fails.

---

**CONV-SETUP-003** — The public surface SHALL be tracked with
`Microsoft.CodeAnalysis.PublicApiAnalyzers`, enabled on **every** project:
`PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` are populated in `Janus.Core`,
`Janus.Hosting` and `Janus.Conformance` and empty in every other project, so that a
public member anywhere else is a build error. A public change without its line in the
unshipped file fails the build; the shipped file changes only in a release commit.

*Source: LIB-API-001, LIB-API-002, LIB-TEST-002, D-149*

This is the contract test of CONV-LAYOUT-002 AC1 in its standard form: the compiler
enumerates the surface and the file is the reviewable diff.

**Acceptance criteria**
1. Adding a public member without editing the unshipped file fails the build.
2. Moving lines from unshipped to shipped happens only in the commit that tags a release.

---

**CONV-SETUP-004** — Formatting and style SHALL be defined once in a committed
`.editorconfig` and enforced: `dotnet format --verify-no-changes` is a gate, and the
`IDE` style rules it encodes are errors at build through CONV-SETUP-001. Fixed choices:
file-scoped namespaces; four-space indentation; `var` only when the type is apparent
from the right-hand side; braces on every block; expression-bodied members for
one-line members only; `this.` never; usings outside the namespace, sorted with
`System` first; one type per file; private fields `_camelCase`; constants PascalCase;
no `#region`; one type per file (CONV-LAYOUT-003).

Because `AnalysisLevel` latest-all turns every rule on and CONV-SETUP-001 makes every
warning an error, the `.editorconfig` SHALL set exactly the following rules below error,
and no others; any further deviation is a specification defect, not a local suppression:

| Rule | Severity | Scope | Why |
|---|---|---|---|
| CA1812 (uninstantiated internal class) | none | source projects | every service is `internal sealed` and constructed by dependency injection (CONV-DESIGN-002) |
| CA2007 (`ConfigureAwait`) | none | test projects, `Janus.Hosting`, `Janus.Cli` | required of library code only (CONV-CODE-002) |
| CA1515 (make public types internal) | none | `Janus.Core`, `Janus.Hosting`, `Janus.Conformance` | these projects hold the contract |
| CA1062 (validate public arguments) | none | all | guards are placed by CONV-CODE-006, not on every public member |

CA1848 stays at error: logging uses the `LoggerMessage` source generator (CONV-LOG-001).

*Source: CONV-NAME-001, D-149*

**Acceptance criteria**
1. A file that `dotnet format` would change fails the gate.
2. The `.editorconfig` is the only place a style rule is configured.
3. No rule other than the four above is set below error in any configuration file, and
   no `#pragma warning disable` or `SuppressMessage` exists without a justification on the
   same line and a phase-report entry.

---

## 1b. Design

The library's shape is fixed by the chapters (CONV-LAYOUT-001, LIB-API-005,
LIB-SEAM-001, LIB-EXT-001, OPS-DATA-001). This section fixes what happens inside those
boundaries so that no pattern is chosen at the keyboard.

**CONV-DESIGN-001** — The architectural style SHALL be **layered per area with feature
folders**. Inside each area project (`Identity`, `Authentication`, `Authorization`) the
top-level folders are the area's features (for example `Accounts`, `Identifiers`,
`Sessions`, `Passwords`, `Grants`), and each feature folder holds, side by side, its
domain types, its service implementation, its persistence port and its internal
contracts. There SHALL be no horizontal folders (`Services`, `Models`, `Interfaces`,
`Helpers`) at any level.

*Source: D-149*

A feature folder is read as one unit; a horizontal layer scatters one feature across
the project. The layers still exist, as the dependency direction inside a folder
(domain types depend on nothing; the service depends on the domain and the port; the
port is an interface the domain never calls).

**Acceptance criteria**
1. No folder named for a technical role exists in an area project.
2. A feature's types, service and port are in one folder.

---

**CONV-DESIGN-002** — The unit of application logic SHALL be a **service class per
operation group**, implementing the corresponding contract from `Janus.Core`
(LIB-API-005), `internal sealed`, constructed by dependency injection, with one public
method per operation. Cross-cutting behaviour (the authorization gate, audit, the
step-up check, the transaction) SHALL be invoked **explicitly** in the method body in a
fixed order: **gate** (`RequireAsync` for the operation and access context; for a read,
the port method also takes the access context and applies the rendered filter inside
the query, AUTHZ-GATE-001), **validate** (semantic rules; shape validation already
happened at the boundary, CONV-CODE-006), **load**, **decide** (domain methods), **persist**
(writes and the outbox row in the one transaction, IDN-LIFE-003a), **audit** (the audit
row in the same transaction), **commit**, **publish** (after commit, the in-process events
of LIB-API-001, raised from the committed outbox row). No mediator,
pipeline, behaviour, interceptor or aspect library SHALL be used.

*Source: LIB-API-005, AUTHZ-IMP-001, D-149*

Explicit calls read top to bottom and are what a reviewer and a test can see. A
pipeline hides the order in registration code, and the two mainstream mediator and
mapping libraries for .NET moved to commercial licences in 2025, which the dependency
rule (CONV-DEP-003) would refuse in any case.

**Acceptance criteria**
1. Every `Janus.Core` service contract has exactly one implementation in the source
   projects (fakes in test projects do not count; LIB-EXT-001 extension points are not
   service contracts) and every implementation is `internal sealed`.
2. No package whose purpose is request dispatch, pipeline behaviours or object mapping
   is referenced.
3. Each operation method performs the gate call before any load or write.

---

**CONV-DESIGN-003** — Persistence SHALL go through **one port per aggregate**, declared
in the aggregate's feature folder as an `internal interface` with intention-revealing
methods (`FindBySubjectAsync`, `AddAsync`, `MarkVerifiedAsync`), implemented in
`Janus.Storage`. There SHALL be no generic repository, no `IQueryable` crossing a port,
and no EF Core type in an area project. `Janus.Storage` SHALL hold one `DbContext` with
one `IEntityTypeConfiguration<T>` per entity in a folder mirroring the area and feature.
The **unit of work is the operation**: a service method runs inside one transaction
opened by an `IUnitOfWork` port and committed once, at the end, after every write.
Hand-written SQL (OPS-DATA-001) lives in `Janus.Storage` beside the port implementation
it serves, never at a call site. Migrations are EF Core migrations in `Janus.Storage`,
applied in the pipeline as an **EF Core migration bundle** built from the same commit
(OPS-MIG-001); the destructive-operation report (OPS-DEP-002) is the idempotent SQL
script of the pending migrations scanned for `DROP`, `ALTER ... TYPE` and `ADD
CONSTRAINT`; the serialized model of AUTHZ-MODEL-005 is JSON written by
`System.Text.Json` source generation to `artifacts/model.json`.

*Source: OPS-DATA-001 to 003, OPS-MIG-001, OPS-DEP-002, LIB-PKG-002, D-149*

**Acceptance criteria**
1. No area project references EF Core or Npgsql.
2. No port method returns `IQueryable`.
3. A service method with two writes and a failure between them leaves neither.

---

**CONV-DESIGN-004** — Domain types SHALL be **plain, encapsulated classes**: `internal
sealed`, state changed only through methods named for the business action
(`Verify()`, `MakePrimary()`, `Suspend(by, reason)`), invariants checked inside those
methods, no public setters, no domain framework or base class. Identifiers SHALL be
strongly typed `readonly record struct` wrappers (`SubjectId`, `OrganizationId`,
`SessionId`) over a UUID value: **version 4** for `SubjectId`, because it is the OIDC
`sub` and IDN-ACCT-002 requires it opaque and a version 7 value would carry the account's
creation instant; **version 7** from `Guid.CreateVersion7()` for every other identifier,
for index locality. Values with rules
(canonical email, E.164 phone, permission string) SHALL be value types that cannot be
constructed in an invalid state.

*Source: IDN-ACCT-002, IDN-ACCT-004, D-149*

**Acceptance criteria**
1. No entity exposes a public or internal property setter.
2. No method takes a bare `Guid` or `string` where a typed identifier or value exists.
3. Constructing an invalid canonical value is a compile-time or immediate runtime
   failure, never a stored row.

---

**CONV-DESIGN-005** — Expected outcomes SHALL be carried by the library's own
`Result` and `Result<T>` types in `Janus.Core`, holding on failure an `Error` with the
code from `10` section 1 and its structured details; faults throw (CONV-ERR-001).
`Result` exposes `Match` and `Switch` only; no `IsSuccess`, `Value` or `Error` accessor is
reachable outside `Janus.Core`, so a caller cannot read the value without handling the
failure (CONV-ERR-001 AC2), and a discarded `Result` is an analyser error (JAN0005). No
third-party result or discriminated-union library SHALL be used. A service method never
returns `null` for "not found"; it returns a failure result with the named code.

*Source: CONV-ERR-001, API-CONV-002, LIB-API-003, D-149*

**Acceptance criteria**
1. Every public contract method returns `Result`, `Result<T>`, or `Task` or
   `ValueTask` thereof.
2. No `null`-returning lookup exists on a contract.

---

**CONV-DESIGN-006** — HTTP endpoints SHALL be **ASP.NET Core minimal APIs** in
`Janus.Hosting`, grouped per area with `MapGroup`, returning typed results
(`TypedResults`). Session resolution, CSRF validation and step-up gating are the
**middleware stages** of BFF-ORDER-001, never endpoint filters; an endpoint filter is
used only for a library-local concern (DTO shape validation, CONV-CODE-006). Request
and response types are `internal sealed record` DTOs in `Janus.Hosting`; mapping between a DTO and a contract type is a hand-written static
method beside the DTO. Serialization uses `System.Text.Json` source generation. No
controllers, no reflection-based mapping.

*Source: LIB-API-005, API-CONV-001 to 005, D-149*

**Acceptance criteria**
1. No type derives from `ControllerBase`.
2. Every endpoint is a one-line mapping to a contract method plus DTO conversion.

---

**CONV-DESIGN-007** — Dependency injection SHALL use the built-in container only.
Each project exposes exactly one `internal static` registration method
(`AddJanusIdentity(this IServiceCollection)`), called from the single public
`AddJanus` entry point in `Janus.Hosting`. Lifetimes: services and ports scoped;
stateless helpers singleton; nothing transient without a recorded reason. Options
SHALL be bound through `IOptions<T>` with `ValidateOnStart`; the runtime-changeable
keys of `10` section 4 are read through the configuration store abstraction, never
through `IOptions`. Time comes from `TimeProvider`; randomness from
`RandomNumberGenerator`; both injected, never static. The key-encryption key, the
fingerprint key and the maintenance credential are fetched once, at startup or at the
start of the command, through the host-supplied secret source of LIB-EXT-001; the
library ships no secrets-manager client.

*Source: OPS-CFG-001, OPS-CFG-008, D-149*

**Acceptance criteria**
1. A host calls one method to register the library.
2. No `DateTime.UtcNow`, `DateTimeOffset.Now` or `new Random` appears outside tests.
3. Startup fails when a required option is missing (LIB-HOST-001).

---

**CONV-DESIGN-008** — The following dependencies are the **only** third-party
packages permitted, and a package outside this table needs a decision-log entry before
it is referenced. The base class library is preferred wherever it suffices.

| Purpose | Package | Why this one |
|---|---|---|
| Relational access | `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` (Storage only, for the migration bundle), `Dapper` | OPS-DATA-001 |
| Cache | `StackExchange.Redis` | INF-CACHE-001; the reference client |
| Password hashing | `Konscious.Security.Cryptography.Argon2` | Argon2id is absent from the base class library; managed implementation, MIT licence (package listing inspected 2026-09-18) |
| WebAuthn | `Fido2` (Fido2NetLib) | The maintained .NET attestation and assertion library; MIT; 4.0.1 targets net8.0 and runs on net10.0 (package listing inspected 2026-09-18) |
| TOTP | `Otp.NET` | RFC 6238 defaults; small |
| OIDC provider | `OpenIddict.AspNetCore`, `OpenIddict.Server`, `OpenIddict.Validation`; stores are hand-written in `Janus.Storage` over the single `DbContext` (no `OpenIddict.EntityFrameworkCore`) | The maintained free OpenID Connect server for ASP.NET Core; a hand-written provider is a security risk this library does not take; hand-written stores keep one `DbContext` (CONV-DESIGN-003) |
| OIDC client (Google, Apple) | `Microsoft.AspNetCore.Authentication.OpenIdConnect` | Microsoft's own handler, shipped as a NuGet package |
| Public surface tracking | `Microsoft.CodeAnalysis.PublicApiAnalyzers` | CONV-SETUP-003 |
| Analyser authoring (`Janus.Analyzers` only) | `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.Analyzers` | The five rules the gates rely on (CONV-CODE-008) |
| Versioning | `MinVer` | Version from the git tag, nothing to maintain (CONV-VCS-005) |
| Tests | `xunit.v3`, `Microsoft.Testing.Platform` (as xunit.v3's runner), `Testcontainers.PostgreSql`, `Testcontainers.Redis` | CONV-TEST-002 |

**Banned**, whatever the reason offered: mediator and pipeline libraries, object
mappers, third-party result or functional libraries, mocking frameworks (hand-written
fakes only, CONV-TEST-007), scheduler libraries (background work is `BackgroundService`
over library tables, IDN-LIFE-003a), identity frameworks (ASP.NET Core Identity), and
any package that duplicates a base class library capability.

*Source: CONV-DEP-003, D-149*

**Acceptance criteria**
1. The set of direct package references, read from `Directory.Packages.props`, equals
   the identifiers in this table.
2. A pull request adding a package not in the table fails a check.

---

## 2. Naming

**CONV-NAME-001** — Standard .NET naming applies throughout: PascalCase for types
and members, camelCase for locals and parameters, `I` prefix on interfaces, `Async`
suffix on asynchronous methods.

*Source: base convention*

The `I` prefix is retained deliberately. It is near-universal in .NET; departing
costs readability for anyone joining later, including future maintainers.

**Acceptance criteria**
1. Analyzer rules enforce the standard set.

---

**CONV-NAME-002** — Permission strings SHALL be `resource:action`, lowercase,
singular resource.

*Source: D-015*

Coarse rather than fine. Twenty permissions is healthy; two hundred means data scope
is being encoded into permission names, which produces role explosion.

**Acceptance criteria**
1. A permission string not matching the pattern fails model validation.
2. No permission name contains a scope qualifier such as a branch or region.

---

**CONV-NAME-003** — Error codes SHALL be hierarchical, lowercase, dot-separated, and
**stable**: `auth.session.expired`, `authz.grant.notfound`.

*Source: LIB-API-001, LIB-API-003*

A code is an identifier, not a message. Rewording the human-facing message is free;
changing the code is breaking.

**Acceptance criteria**
1. Every code is documented with meaning and remediation.
2. A contract test fails when a code changes without a version bump.

---

## 2a. Code conventions

**CONV-CODE-001** — Every type SHALL be `sealed` unless it is designed for
inheritance, and `internal` unless it is part of the contract (CONV-LAYOUT-002).
Members SHALL be ordered: constants, fields, constructors, properties, public methods,
private methods. One type per file, named for the file (CONV-LAYOUT-003).

*Source: D-149*

**Acceptance criteria**
1. An unsealed non-abstract class fails an analyser.

---

**CONV-CODE-002** — Every asynchronous method SHALL take a `CancellationToken` as its
last parameter and pass it through; SHALL be named with the `Async` suffix; SHALL
return `Task` or `ValueTask`, never `void`; and SHALL NOT block on a task (`.Result`,
`.Wait()`, `GetAwaiter().GetResult()`). Library code SHALL use `ConfigureAwait(false)`.

*Source: D-149*

**Acceptance criteria**
1. The analysers for blocking calls and missing cancellation tokens are errors.

---

**CONV-CODE-003** — Data that crosses a boundary SHALL be immutable: `sealed record`
for DTOs and events, `readonly record struct` for identifiers and small values,
`IReadOnlyList<T>` and `IReadOnlyDictionary<K, V>` on every public or internal contract.
Collections SHALL never be exposed as mutable types.

*Source: D-149*

**Acceptance criteria**
1. No contract member exposes `List<T>`, `Dictionary<K, V>` or an array, except
   `ReadOnlyMemory<byte>` for secret material (CONV-CODE-007).

---

**CONV-CODE-004** — Static mutable state SHALL NOT exist. Reflection SHALL NOT be used
where a type, a generic or a source generator serves. `dynamic` SHALL NOT be used.
Partial classes SHALL exist only where a source generator requires them.

*Source: D-149*

**Acceptance criteria**
1. No `static` field is writable after construction.
2. No use of `System.Reflection` outside the model builder and tests.

---

**CONV-CODE-005** — Every public type and member SHALL carry XML documentation stating
what it does and the spec item it implements (`<remarks>Implements REG-IDENT-004.</remarks>`).
Internal types SHALL carry documentation only where the name does not say it. Comments
SHALL explain *why*, never *what*; a comment that restates the code is removed. No
commented-out code. No `TODO`, `FIXME` or `HACK` marker: unfinished work is a task in
the phase report, not a note in the source.

*Source: GenerateDocumentationFile (CONV-SETUP-001), D-149*

**Acceptance criteria**
1. A public member without documentation fails the build.
2. The strings `TODO`, `FIXME`, `HACK` and commented-out code fail a check.

---

**CONV-CODE-006** — Input SHALL be validated at the boundary where it enters: shape at
the endpoint DTO in `Janus.Hosting`, semantics in the first lines of the service
implementation in the area project (the validate step of CONV-DESIGN-002), with guard
methods, not attributes, returning the named `10` code. Inside a feature, a value that
reached a domain type is trusted because the value type made it valid (CONV-DESIGN-004).

*Source: API-CONV-002, D-149*

**Acceptance criteria**
1. No validation attribute appears on a domain type.
2. Every endpoint rejects a malformed body with a `10` code before calling a service.

---

**CONV-CODE-007** — Security-sensitive comparisons SHALL use constant-time methods
(`CryptographicOperations.FixedTimeEquals`); secrets SHALL live in `byte[]` inside a method,
`ReadOnlySpan<byte>` where they do not cross an `await`, and `ReadOnlyMemory<byte>` when
they cross a port, and be cleared after use; nothing SHALL derive its own primitive
where the base class library or a permitted package provides one (CONV-DESIGN-008).

*Source: AUTH-PASS-007, AUTH-KEY-002, D-149*

**Acceptance criteria**
1. No string comparison is used on a hash, token or code.
2. No custom implementation of a hash, cipher, key derivation or random source exists.

---

**CONV-CODE-008** — `Janus.Analyzers` SHALL ship the following rules, each an error,
and the gates that name them (CONV-GATE-001) SHALL rely on them and on nothing else.

| Rule | Detects | Serves |
|---|---|---|
| JAN0001 | A `catch` block whose path returns a permitted, authenticated or successful value | CONV-ERR-002 |
| JAN0002 | A logging call whose argument is a type or member marked as never-logged (`[NeverLogged]` in `Janus.Core`) | CONV-LOG-003 |
| JAN0003 | A `public` or `internal` non-abstract class that is not `sealed` | CONV-CODE-001 |
| JAN0004 | `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` on a task; an `async` method without a `CancellationToken` parameter | CONV-CODE-002 |
| JAN0005 | A `Result` or `Result<T>` expression whose value is discarded | CONV-DESIGN-005 |

*Source: D-149*

**Acceptance criteria**
1. Each rule has a test project case that fails and a case that passes.
2. The analyser project is referenced by every source project as an analyser, never as
   a library.

---

## 3. Error handling

**CONV-ERR-001** — **Expected outcomes SHALL return a result**; **faults SHALL
throw.**

| Kind | Examples | Mechanism |
|---|---|---|
| Expected outcome | Wrong password, grant not found, consent already withdrawn | Result with value or error code |
| Fault | Database unreachable, invalid configuration, missing policy registration | Throw |
| Validation | Bad model declaration, out-of-range configuration | Throw at startup |

*Source: LIB-API-003, and the startup validation requirements throughout*

Failed authentication is routine, not exceptional. Modelling it as an exception is
slow and — more importantly — easy to swallow accidentally, which defeats the
fail-closed requirements in documents 02, 03 and 04.

**Acceptance criteria**
1. No authentication or authorization denial is signalled by an exception.
2. A caller cannot ignore a result — the compiler requires handling both cases.
3. Configuration faults surface at startup, never at first request.

---

**CONV-ERR-002** — A `catch` block SHALL NEVER return a permitted, authenticated, or
successful outcome.

*Source: AUTH-PRIN-001, AUTHZ-PRIN-003, P-003*

This is the enforcement mechanism behind every fail-closed requirement in the
specification. One well-meaning `catch { return true; }` written under deadline
pressure defeats the entire security model, and it looks reasonable in review.

**Acceptance criteria**
1. An analyzer rule detects and fails the build on this pattern.
2. The rule cannot be suppressed without an explicit, reviewed annotation.

---

**CONV-ERR-003** — Exceptions SHALL NOT be swallowed. A caught exception is either
handled meaningfully or rethrown. Empty catch blocks SHALL NOT exist.

**Acceptance criteria**
1. An analyzer rule fails the build on an empty catch.
2. Catch-and-log-and-continue does not appear on a security path.

---

## 4. Testing

**CONV-TEST-001** — One test project per source project, mirroring its layout.

**Acceptance criteria**
1. A test's location identifies what it covers without reading it.

---

**CONV-TEST-002** — Four test kinds SHALL be kept separate.

| Kind | Runs against | Purpose |
|---|---|---|
| Unit | Nothing external | Fast, run constantly |
| Integration | Real PostgreSQL and Redis in containers | Where most value lies — this system is about how data and permissions behave together |
| Contract | The public surface | Guards LIB-API-001; fails on unintended change |
| Conformance | A host's own configuration | Shipped per LIB-TEST-001 |

*Source: LIB-TEST-001, LIB-TEST-002*

**Acceptance criteria**
1. Unit tests run without containers.
2. Integration tests tear down their containers.
3. Kinds are independently runnable.

---

**CONV-TEST-003** — Four areas SHALL have mandatory coverage.

| Area | Requirement |
|---|---|
| **Permission truth tables** | Every case through both check and filter, asserting agreement |
| **Ancestry maintenance** | Create, move, bulk, subtree move, concurrent move |
| **Policy coverage** | Every queryable entity has a registered policy |
| **Derived grants** | Deny defeats them, inheritance reaches through them, and materialisation changes no outcome |

*Source: AUTHZ-TEST-001, AUTHZ-INHERIT-003, AUTHZ-GATE-001, AUTHZ-DERIVE-002*

These are where a gap produces a **silent** security failure rather than a crash.

**Acceptance criteria**
1. Each area has a dedicated suite that fails the build.
2. A new entity without a policy produces a red build.

---

**CONV-TEST-004** — A coverage percentage target SHALL NOT be set as a gate.

*Source: convention decision*

A percentage measures the wrong thing: satisfiable by testing trivial code and
skipping the hard parts, and it creates pressure to write tests that assert nothing.
The mandatory areas above are the bar. Coverage MAY be tracked as information.

**Acceptance criteria**
1. No pipeline step fails on a coverage percentage.
2. A change to permission logic without a corresponding truth-table change is
   caught.

---

**CONV-TEST-005** — Fixtures SHALL use realistic structure — actual organizations,
nested groups, multi-level containment. Permission bugs hide in structure, and
structureless fixtures find nothing.

**Acceptance criteria**
1. No fixture consists solely of flat, unrelated entities.

---

**CONV-TEST-006** — A test requiring a comment to explain its scenario SHOULD be a
truth-table row instead.

Keeps scenarios in one reviewable place rather than scattered across test methods.

---

**CONV-TEST-007** — Tests SHALL use xunit.v3 on Microsoft.Testing.Platform. A test
class mirrors the type under test (`AccountServiceTests`); an acceptance-criterion test
is named `ITEM_ACn_Outcome` (`REG_IDENT_006_AC2_UndoRestoresWithinWindow`); every other
test is named `Method_Scenario_Outcome` (`Remove_PrimaryIdentifier_RefusedWithPrimaryCode`);
the body is arranged in three blocks separated by one blank line, with no comments
marking them. Collaborators are replaced by **hand-written fakes** in the test project,
never by a mocking framework. Integration tests use Testcontainers and share one
container per test class. Every acceptance criterion a test can decide maps to one
test method carrying the item and criterion; a criterion a test cannot decide (an
operational, physical or review step) is listed in the phase report with how it was
verified.

*Source: CONV-TEST-001, CONV-TEST-002, CONV-DESIGN-008, D-149*

**Acceptance criteria**
1. No mocking package is referenced.
2. Every testable acceptance criterion of an implemented item has a test carrying its
   identifier; the untestable ones are named in the phase report.

---

## 4a. Enumerated values

**CONV-ENUM-001** — A value the **code branches on** SHALL be a constrained column. A
value that **changes without code changes** SHALL be a table.

*Source: D-094*

| Kind | Storage | Examples |
|---|---|---|
| Code branches on it | Constrained column | Account state, erasure status, concealment behaviour |
| Changes without a deploy | Table | Roles, permissions, the courier's district list |
| **Declared with properties the code branches on** | Table seeded from the declaration | **Lawful bases, sensitive-data categories** (D-108) |

**The test:** could someone add a value and have it work without an engineer? If no, a
lookup table buys nothing — the value would be inert until code handled it, and every
query pays a join.

**Native database enum types SHALL NOT be used.** Altering one is a schema migration with
locking behaviour; a check constraint gives the same guarantee and changes freely.

**The case that looked mixed, resolved.** Lawful bases appeared to be both branched on
and jurisdiction-specific (D-091). They were branched on **by name** — which made a
different jurisdiction a code change however the values were stored (D-094 §94.3).
D-108 removed the name-branching: each declared basis carries the properties the code
reads (`IsConsent`, `RequiresAssessment`, and so on), exactly as factors do
(AUTH-FACT-001), so the list is a table seeded from the host's declaration and a new
jurisdiction adds rows, not code. The third row above is that case.

**Acceptance criteria**
1. An unrecognised value is rejected by the database, not only by application code.
2. No native enum type appears in the schema.
3. Adding a role requires no deploy; adding an account state does; adding a lawful
   basis is a declaration.

---

## 4b. Content

**CONV-CONTENT-001** — A specification SHALL state **what a message or screen must
achieve**, never its wording. Where an example helps, it SHALL be given in italics and
marked *illustrative*. The words themselves are a **frontend deliverable**
(`18-frontend-integration`): reviewed for UX, written natively in each language, and
**never translated from specification text**. Three exceptions keep a requirement on
the words: the **identical-regardless** responses of AUTH-ABUSE-002 and AUTH-ABUSE-003,
where the requirement is that two outcomes read the same; **legal texts**, which are
counsel's and versioned (PRIV-CONS-005, PRIV-CONS-006); and **never-disclose rules**,
where a fact is required to be absent from a message.

*Source: D-146; D-054, LIB-API-003*

The library never renders user-facing text (LIB-API-003), so a sentence quoted in a
specification could only ever have been a suggestion; marking it as one stops it being
implemented verbatim, translated word for word, or treated as a conformance target. A
message is judged by whether the person can do what the requirement needs them to do
next, which is a UX review, not a diff against the specification. In the three
exceptions the words carry a security or legal property that a rewording could break,
so there the specification does constrain them.

**Acceptance criteria**
1. No requirement outside the three exceptions is failed by a change of wording alone;
   a review of the acceptance criteria in documents 01 to 20 finds none that quotes a
   string as the pass condition.
2. Every quoted user-facing string in the specifications is marked *illustrative*, or
   belongs to one of the three exceptions and names which.
3. A frontend message is not a translation of a specification sentence; each language
   is written from the requirement.

---

## 5. Logging

**CONV-LOG-001** — Logging SHALL be **structured** — named fields, not sentences with
values interpolated. Every log call SHALL go through a `LoggerMessage` source-generated
method (D-149), which fixes the event name and the field names at compile time.

**Acceptance criteria**
1. No log call uses string interpolation for values.
2. Logs are queryable by field.

---

**CONV-LOG-002** — Every log entry SHALL carry a correlation identifier, the same one
returned in concealment responses.

*Source: AUTHZ-CONCEAL-004*

A customer's "I can't see my order" resolves to the exact decision that denied them.

**Acceptance criteria**
1. The identifier in a denial response resolves to log entries for that request.

---

**CONV-LOG-003** — The following SHALL NEVER be logged.

**Credentials and secrets:** passwords, tokens, session identifiers, TOTP secrets or
codes, recovery codes, verification codes, card data, the hash prefix sent for
password screening.

**Health-implying data:** order contents, in any log. Order contents are health data
per PRIV-SENS-003. A debug line logging a cart looks harmless and is a health-data
disclosure.

**Compliance text:** the content of consent or notice text in any language. Log the
notice **version**.

*Source: PRIV-SENS-003, PRIV-PRIN-001, D-031*

**Acceptance criteria**
1. An analyzer rule detects forbidden values and fails the build.
2. A test asserts no log output contains an order line item.

---

**CONV-LOG-004** — Personal identifiers SHOULD NOT be logged. Log the **subject
identifier** instead of email addresses or phone numbers. Where an attribute must
appear, it is a deliberate, reviewed choice.

*Source: PRIV-PRIN-001*

Practical consequence: a log containing only identifiers requires no erasure pass
under PRIV-RIGHT-005. Logs holding personal data fall under retention and erasure
like any other store.

**Acceptance criteria**
1. Default log output for a request contains no email address or phone number.
2. Deliberate exceptions carry an annotation naming the reason.

---

**CONV-LOG-005** — Security events SHALL be logged regardless of level configuration:
failed authentication, denied authorization, step-up, configuration change,
break-glass use.

*Source: OPS-CFG-005, OPS-BOOT-002*

**Acceptance criteria**
1. Raising the minimum log level does not suppress these.

---

**CONV-LOG-006** — A logged denial SHALL use the same explanation mechanism as the
gate, not a parallel code path.

*Source: AUTHZ-GATE-004*

Two paths could disagree, and the log would be the one nobody notices is wrong.

**Acceptance criteria**
1. Log and explanation output derive from one source.

---

## 6. Version control

**CONV-VCS-001** — Trunk-based development with short-lived branches, merged via pull
request.

*Source: OPS-MIG-005*

Long-lived branches combine badly with expand-and-contract migrations.

**Acceptance criteria**
1. Branches are measured in days, not weeks.

---

**CONV-VCS-002** — The protected branch SHALL require **status checks**, not
approvals.

*Source: D-042.2*

The platform does not permit approving one's own pull request, so requiring approvals
on a single-developer repository would block all merges. The automated gates are the
enforcement; the pull request exists so the diff is read as a unit.

**When a team exists:** required approvals are enabled — already supported on the
current plan.

**Acceptance criteria**
1. Merging with a failing status check is blocked.
2. Merging without an approval is permitted while there is one developer.

---

**CONV-VCS-003** — Commit messages SHALL follow **Conventional Commits 1.0.0**: a
type from `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `ci`, `chore`, an
optional scope naming the area or feature (`feat(identifiers): ...`), a description in
the imperative, and `BREAKING CHANGE:` in the footer or `!` after the type for any
change that breaks LIB-API-001. One logical change per commit.

*Source: D-149*

**Acceptance criteria**
1. A commit message that does not parse under Conventional Commits 1.0.0 fails a
   check on push and on pull request.
2. Every breaking change to the contract carries the breaking marker.

---

**CONV-VCS-004** — A commit touching permission logic SHALL reference the
corresponding truth-table change.

*Source: AUTHZ-TEST-001, CONV-TEST-003*

If there is no truth-table change, that is the signal to stop and write one. This is
the one place the discipline is hard rather than advisory.

**Acceptance criteria**
1. A permission-logic change without a truth-table change is flagged.

---

**CONV-VCS-005** — A `CHANGELOG.md` SHALL be kept at the repository root in the
**Keep a Changelog 1.1.0** format (an `Unreleased` section; one section per released
version with its date; entries grouped under `Added`, `Changed`, `Deprecated`,
`Removed`, `Fixed`, `Security`; written for a reader, not copied from commit
messages). Every pull request that changes behaviour adds its line under `Unreleased`.
A release moves `Unreleased` to a version section, moves `PublicAPI.Unshipped.txt` into
`PublicAPI.Shipped.txt` (CONV-SETUP-003) and tags the commit `vMAJOR.MINOR.PATCH`;
**MinVer** derives the package version from that tag (LIB-VER-001). Nothing else sets a
version number.

*Source: LIB-VER-001, LIB-VER-002, D-149*

**Acceptance criteria**
1. A behaviour-changing pull request without a changelog line fails a check.
2. The package version equals the nearest tag; no version literal exists in a project
   file.
3. A major-version section of the changelog links the migration note of LIB-VER-002.

---

## 7. Dependencies

**TL;DR.** Your code will use other people's packages. If one is compromised, its
code runs inside your system with full access. Three cheap habits close most of it.

**CONV-DEP-001** — Package versions SHALL be pinned via a committed lockfile. Updates
SHALL be deliberate, never implicit.

*Source: D-046*

An unpinned dependency means a build on a different day produces different code, and
a compromised release reaches production without anyone choosing it.

**Acceptance criteria**
1. A lockfile is committed and restore is deterministic.
2. A build with no source change produces the same dependency set.

---

**CONV-DEP-002** — Automated vulnerability alerting SHALL be enabled on the
repository.

*Source: D-046*

Available free on the current plan for private repositories, unlike secret scanning.

**Acceptance criteria**
1. Alerting is on for the repository.
2. A known-vulnerable dependency produces an alert.

---

**CONV-DEP-003** — Adding a dependency SHALL be a deliberate choice. Each is another
author trusted with full application privilege.

*Source: D-046*

**Acceptance criteria**
1. A new direct dependency is introduced in its own commit, stating why.
2. Transitive dependency count is visible in review.

---

**CONV-DEP-004** — Security updates SHALL be applied promptly; other updates SHALL be
taken **as they arrive**, merged when checks pass. Neither is a calendar task.

*Source: D-046, D-110*

Vulnerability alerting opens a change when an advisory lands; routine updates arrive
as they are published. Acting on them is a habit at the point of a pull request, not a
recurring entry — which is what keeps the accepted recurring human tasks at two
(`06` §9). Never updating accumulates known holes; chasing releases by the calendar is
churn.

**Acceptance criteria**
1. Security alerts are actioned rather than accumulating.
2. No recurring calendar entry exists for dependency updates.

---

## 8. Automated gates

With one developer, the checks a reviewer would perform are mechanical.

**CONV-GATE-001** — The pipeline SHALL block on all of the following.

| Gate | Source |
|---|---|
| Contract tests | LIB-TEST-002 |
| Policy coverage test | AUTHZ-GATE-001 |
| Truth-table suite | AUTHZ-TEST-001 |
| Double migration run | OPS-MIG-007 |
| Analyzer: no permitted outcome from a catch block | CONV-ERR-002 |
| Analyzer: no forbidden values in logs | CONV-LOG-003 |
| Secret scanning | OPS-DEP-004 |
| Dependency vulnerability alerting | CONV-DEP-002 |
| Destructive-operation detection report | OPS-DEP-002 |
| `dotnet format --verify-no-changes` | CONV-SETUP-004 |
| Public surface files up to date | CONV-SETUP-003 |
| Locked restore | CONV-SETUP-002 |
| Commit message format | CONV-VCS-003 |
| Changelog line present | CONV-VCS-005 |
| Dependency allow-list | CONV-DESIGN-008 |
| Forbidden markers and commented-out code | CONV-CODE-005 |
| `InternalsVisibleTo` allow-list | CONV-LAYOUT-002 |
| Acceptance-criterion test names present for each item of the phase | CONV-TEST-007 |
| `Janus.Analyzers` rules JAN0001 to JAN0005 | CONV-CODE-008 |

**Acceptance criteria**
1. Each runs on pull request.
2. Any failing blocks the merge.

---

**CONV-GATE-002** — Full integration and migration suites SHALL NOT run on every push
to every branch.

*Source: D-042.4, OPS-DEP-005, D-147*

The plan allowance is 3,000 Actions minutes per month; containerised integration
tests plus the double migration run consume it quickly.

**Acceptance criteria**
1. Unit and analyzer checks run on every push.
2. Heavy suites run on pull request and on the default branch.
3. Consumption is measured against the allowance and reviewed by the operator,
   monthly; the review is recorded in the maintenance log of OPS-MAINT-001
   (OPS-DEP-005 AC3).

---

## 9. What changes when someone joins

Recorded now so it is a switch rather than a redesign.

| Change | Currently | Source |
|---|---|---|
| Pull requests require approval | Status checks only | D-042.2 |
| Destructive-DDL gate enabled | Disabled | D-019, D-042.1 |
| Recovery approver count above one | One | D-008 |
| Gate toggle protected from pull-request edit | Unprotected | OPS-DEP-003 |

All four are built and inactive.

---

## 10. Platform constraints

Recorded so they are not rediscovered.

| Constraint | Consequence |
|---|---|
| Environment required reviewers and wait timers unavailable on private repositories | Destructive-DDL gate implemented as a pipeline split (D-042.1) |
| Cannot approve one's own pull request | Status checks rather than approvals (CONV-VCS-002) |
| Secret scanning and push protection unavailable on personal plans | Scanning moved into the pipeline (OPS-DEP-004) |
| 3,000 Actions minutes per month | Heavy suites not on every push (CONV-GATE-002) |
| 2 GB package storage | Prune pre-release versions |

---

## 11. Open items

None. The root namespace is **`Janus`**. No registry check is required — the library
is internal to the company's own projects and is not published publicly. The known
collisions (a graph database, a media server) are in unrelated domains and cannot be
confused with an internal identity library.
