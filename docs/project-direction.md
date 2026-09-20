# Project Direction

## Goal

`all-code-checker` aims to provide a concise, language-agnostic CI/checking experience for source code.

The project does **not** try to judge high-level software design quality. Instead, it focuses on problems that can be detected mechanically or with high confidence from source code and language semantics.

A useful working boundary is:

> Detect issues that can be determined from code itself with high confidence, without requiring subjective architectural judgment.

This includes both ordinary compiler/linter failures and, later, semantic-safety issues that may compile successfully but are still dangerous.

## Initial language support

The initial target languages for the road to v1.0.0 are:

- TypeScript
- C#
- Python
- Go

Planned after v1.0.0:

- Rust
- C++
- Luau
- Ruby

Luau and Ruby are lower-priority than the initial v1.0.0 languages, but are still important because some projects may embed them as application scripting or modding languages. The checker should eventually analyze such embedded script code as first-class source input rather than assuming that only the host application's primary language matters.

Planned when the Bitlang project has progressed enough:

- Bitlang

The initial set is intentionally limited. The goal is to establish a reusable analysis/checking model before expanding to more languages.

### Rationale for language priority

The v1.0.0 language set is also based on the languages currently used most actively across the maintainer's repositories.

From the current active repository set:

- Python is used very frequently.
- Go and C# are also actively used in multiple projects.
- TypeScript is included because its structural type system makes it especially useful for developing semantic-safety checks, even when it is not the dominant language in the current repository set.
- C++ is present in several active repositories, but is intentionally deferred until after v1.0.0 to keep the first implementation scope manageable.
- Rust currently has lower usage and is also planned for post-v1.0.0 support.
- Luau and Ruby may appear inside otherwise unrelated applications as embedded scripting or mod languages, so they are planned as later expansion targets even when they are not the repository's primary implementation language.
- Bitlang support will be added when the language and compiler project are mature enough to expose a stable analysis surface.

This ordering is therefore driven by both real-world usage and the value each language provides for validating the checker's architecture.


## Multi-language repository support

The checker must not assume that a repository has only one meaningful programming language.

Language detection should operate on the repository contents themselves and may return multiple active languages for a single project.

Examples include:

- a C# application containing Luau-based mod scripts
- a Go application embedding Ruby scripts
- a TypeScript project with Python tooling
- a native application with scripting files used for plugins, automation, or user extensions

The repository's "primary language" is therefore not sufficient for deciding what should be analyzed.

The intended model is:

```text
repository
  ↓
detect all relevant source languages
  ↓
group source files by language / project boundary
  ↓
run the appropriate analyzers for each group
  ↓
merge diagnostics into one result
```

This also means that embedded scripting languages such as Luau and Ruby should eventually be treated as first-class analysis targets, even when they represent only a small portion of the repository.

Language detection should be based on source files, project metadata, and directory/project boundaries rather than relying only on repository-level language statistics.

## Analysis layers

The checker is expected to grow in layers.

### Layer 1: Compile-free obvious error detection

The first priority is detecting errors that are already evident from source analysis and would very likely fail compilation, type checking, or execution setup later.

This should happen without requiring a full project build whenever possible.

The intended analysis pipeline is approximately:

```text
source
  ↓
parse
  ↓
AST
  ↓
scope / symbol resolution
  ↓
basic semantic analysis
  ↓
diagnostics
```

Candidate categories include:

- syntax errors
- invalid declarations
- duplicate declarations
- invalid scope usage
- unresolved symbols
- invalid control-flow constructs
- obvious call-signature mismatches
- obvious member-access errors
- basic type inconsistencies that can be determined statically

Some checks are possible with AST alone. Others require symbol resolution or lightweight semantic/type information.

The project should avoid reimplementing entire compilers where a language already exposes a reliable parser or semantic-analysis API.

Examples of language-native analysis foundations that may be used:

- TypeScript: TypeScript Compiler API
- C#: Roslyn
- Python: Python AST and related static-analysis facilities
- Go: `go/parser`, `go/types`, and related standard tooling

## Future layer: Semantic safety

After the obvious-error layer is established, the checker should also detect code that may compile successfully but is dangerous because distinct concepts are treated as interchangeable.

Examples include:

- passing values with the same primitive representation but different meanings
- directly passing structurally compatible but semantically different object/DTO types
- unsafe casts
- unchecked nullability
- swallowed errors/exceptions
- ignored results
- lossy conversions
- suspicious reuse of data-transfer objects across different semantic boundaries

Example:

```ts
type UserId = string;
type OrderId = string;

function getOrder(id: OrderId) {}

const userId: UserId = "...";
getOrder(userId); // structurally valid, semantically suspicious
```

Another example:

```ts
type CreateUserInput = {
  name: string;
  email: string;
};

type UpdateUserInput = {
  name: string;
  email: string;
};

function updateUser(input: UpdateUserInput) {}

const input: CreateUserInput = {
  name: "Alice",
  email: "alice@example.com",
};

updateUser(input); // structurally compatible, but may cross a semantic boundary
```

These checks belong to semantic safety rather than architectural design review.

## Out of scope for the checker

The project should not attempt to make subjective design decisions such as:

- whether an architecture is good
- whether a domain model is correct
- whether a class or module boundary is conceptually ideal
- whether a database design is appropriate for the business
- whether an API is well designed from a product perspective
- whether a given DDD aggregate or responsibility split is correct

Those require human judgment and context beyond static source-code evidence.

## Diagnostic model

Language-specific diagnostics should eventually be normalized into a common representation.

Example:

```json
{
  "rule": "ACC1003",
  "category": "member-access",
  "severity": "error",
  "language": "typescript",
  "file": "src/user.ts",
  "line": 42,
  "message": "Property 'foo' does not exist on type 'User'"
}
```

A likely severity model is:

- error
- warning
- info

High-confidence failures belong at `error`. Semantic-risk rules may use `warning` or `info` depending on certainty.

## Current implementation priority

The first concrete milestone is:

> For TypeScript, C#, Python, and Go, detect obvious source-level errors without requiring a full project build whenever the language tooling makes that possible.

Once that foundation is reliable, semantic-safety rules can be added incrementally.
