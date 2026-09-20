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


### Real-world mixed-language cases

The need for multi-language support is not hypothetical.

One active project, Rowly, contains three implementation/scripting languages in the same repository:

- Rust
- Ruby
- Luau

This makes Rowly a useful future validation case for the repository-analysis architecture. A correct implementation should be able to discover all three languages, identify the relevant source/project boundaries, run the appropriate analyzers independently, and merge the resulting diagnostics.

Another common repository shape is:

- Python
- C++

This combination is important because it often represents a higher-level Python layer together with native code. The checker should therefore avoid assuming that language groups are unrelated simply because they use different toolchains.

Future project discovery may need to recognize boundaries such as:

- multiple language roots in one repository
- embedded scripting directories
- native extension modules
- generated-code boundaries
- vendored or third-party source that should not be analyzed by default
- test fixtures or examples that may need different checking rules

Rowly and Python+C++ repositories should be treated as representative acceptance cases when multi-language project discovery is implemented.

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

## Layer 4: Excessive function responsibility heuristics

The checker may detect functions or methods that appear to be doing too much, even though this approaches software-design territory.

This category must remain advisory. The tool is not intended to decide whether a design is "good" or "bad".

The default severity should be:

- `注意`: the function is unusually large or complex and may be worth reviewing.
- `警告`: multiple strong signals indicate that the function is very likely carrying too many responsibilities.
- `危険`: should generally not be used for this category.

The checker should not rely on line count alone.

Candidate signals include:

- total source lines in the function
- number of branches
- nesting depth
- cyclomatic complexity
- number of local variables
- number of parameters
- number of external calls
- number of distinct state mutations
- number of exception / error handling branches
- mixture of unrelated operation types such as validation, persistence, formatting, I/O, and orchestration in one function

A function should only be promoted from `注意` to `警告` when several strong signals are present together.

Example output:

```text
ACI2xx 注意 この関数は分岐数とネスト深度が大きく、複数の責務を持っている可能性があります
```

or, for a stronger case:

```text
ACI2xx 警告 この関数は非常に大きく、複雑度・分岐数・外部操作数の複数指標で高い値を示しています
```

This rule family should avoid prescribing a specific refactoring or architecture. It should only surface measurable evidence that review may be warranted.

## Future C++ pointer-safety checks

When C++ support is added, the checker should include rules for dangerous pointer passing and lifetime misuse.

This area may legitimately produce `危険` diagnostics when static evidence is strong enough, because pointer misuse can directly lead to undefined behavior, memory corruption, crashes, or use-after-free bugs.

Candidate patterns include:

- passing a pointer to an object whose lifetime ends before the callee may use it
- returning or storing pointers/references to local stack variables
- use-after-free or use-after-delete when detectable
- double deletion or repeated ownership release
- dereferencing a pointer that can be proven null
- passing addresses of temporaries where the lifetime is insufficient
- retaining pointers into containers across operations that may invalidate them
- passing raw owning pointers across APIs without a clear ownership contract
- mixing owning and non-owning pointer semantics in a way that is likely to cause invalid lifetime assumptions
- unsafe conversion between unrelated pointer types
- pointer arithmetic that can be proven or strongly inferred to escape the valid object/array range

Suggested severity guidance:

- `危険`: lifetime violation, use-after-free, double free, invalid dereference, or other undefined behavior is effectively certain from static evidence.
- `警告`: ownership or lifetime handling is highly suspicious but not provably invalid.
- `注意`: raw pointer passing is ambiguous or review-worthy, but there is not enough evidence to infer a likely failure.

The checker should avoid treating all raw pointers as errors. C++ permits valid low-level pointer use, so diagnostics should depend on ownership, lifetime, nullability, and invalidation evidence rather than pointer syntax alone.

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

Rule identifiers use the `ACI` prefix followed by a numeric identifier.

The initial human-readable output format should contain, at minimum:

```text
ACI001 危険 理由
ACI002 警告 理由
ACI003 注意 理由
```

The default output language is Japanese.

English output should be supported in a future release, but for v1.0.0 it is only documented as a planned capability and is not a release requirement.

A normalized internal representation may look like:

```json
{
  "rule": "ACI001",
  "severity": "危険",
  "language": "typescript",
  "file": "src/user.ts",
  "line": 42,
  "reason": "User 型に foo プロパティは存在しません"
}
```

Additional fields such as category, column, source range, related symbols, or suggested fixes may be added later, but the first supported output range is intentionally simple: rule code, severity, and reason.

The checker uses three user-facing severity levels:

- 危険
- 警告
- 注意

These are intentionally different from ordinary compiler-style `error / warning / info` levels.

- `危険`: the checker has strong evidence that the code is invalid, will fail, or is otherwise highly unsafe.
- `警告`: the code is valid, but the operation has a high probability of failing or behaving incorrectly.
- `注意`: the pattern is suspicious or worth reviewing, but the evidence is weaker or the outcome depends more heavily on runtime/context.

The default and v1.0.0-required user-facing language is Japanese.

English output is planned for future support, but it is explicitly outside the v1.0.0 required scope.

## Current implementation priority

The first concrete milestone is:

> For TypeScript, C#, Python, and Go, detect obvious source-level errors without requiring a full project build whenever the language tooling makes that possible.

Once that foundation is reliable, semantic-safety rules can be added incrementally.


## Layer 2: High-risk operations likely to fail or misbehave

After compile-free obvious error detection, the next priority is code that is syntactically valid and may compile, but has a high probability of failing at runtime, doing nothing useful, or behaving differently from what the author likely intended.

This layer should focus on patterns with a strong static signal rather than subjective style preferences.

Candidate examples include:

- accessing an index that can be proven or strongly inferred to be out of range
- dereferencing or calling through a value that can be null / nil / None on the current path
- dividing by a value that can be proven to be zero
- using a file, stream, socket, iterator, or other resource after it has been closed or disposed
- performing operations on an object before required initialization
- ignoring an error/result in APIs where failure handling is essential
- calling blocking operations from contexts where they are highly likely to deadlock or stall
- mutating a collection while iterating over it when the language/runtime semantics make this unsafe
- using a value after move / invalidation / ownership transfer where detectable
- using obviously invalid path, URL, format, encoding, or conversion operations when the relevant value is statically known
- impossible or contradictory conditions that indicate dead code or a logic mistake
- API calls whose arguments form a combination that is valid by type but very likely invalid semantically

The intended distinction is:

```text
Layer 1:
"This is already invalid or will almost certainly be rejected by the language/toolchain."

Layer 2:
"This is valid code, but the operation is highly likely to fail or not behave as intended."

Layer 3:
"This may work, but it is semantically dangerous or crosses conceptual boundaries."
```

Layer 2 diagnostics should generally require stronger evidence than ordinary heuristic lint rules. The checker should prefer low false-positive rates even if that means missing some cases.

The severity mapping for this layer is:

- `危険`: failure or invalid behavior is effectively certain from static evidence.
- `警告`: failure or misbehavior is highly probable.
- `注意`: the operation is suspicious and worth review, but evidence is weaker.

In particular, Layer 2 should normally produce `警告` or `注意`, not `危険`, unless the static evidence makes failure effectively certain.
