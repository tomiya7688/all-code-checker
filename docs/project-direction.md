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

## Reference, ownership, and lifetime safety

Dangerous reference passing is a language-independent check category.

C++ is expected to expose this problem particularly often through raw pointers, but the underlying rule is broader: values, references, handles, borrowed objects, resources, or pointers should not be passed or retained in ways that violate ownership, lifetime, nullability, or invalidation rules.

This category is required conceptually for all supported languages. Each language adapter should implement the applicable subset based on that language's semantics.

Candidate patterns include:

- passing or retaining a reference whose underlying value becomes invalid before later use
- using an object, handle, stream, socket, iterator, or resource after it has been closed, disposed, freed, moved, or otherwise invalidated
- returning or storing references to values whose lifetime does not outlive the reference
- dereferencing or using a nullable reference when null is statically certain or highly probable
- retaining references into collections across operations that invalidate those references
- transferring ownership while continuing to use the original value
- ambiguous ownership transfer across API boundaries
- unsafe reference or pointer conversions
- passing temporary values into APIs that may retain references beyond the temporary's lifetime
- double release / double dispose / repeated ownership release where the language permits it

Language-specific manifestations may include:

- C++: raw pointers, references, iterator invalidation, use-after-free, double delete, addresses of temporaries, ownership ambiguity
- Rust: moved-value misuse, borrow/lifetime violations in unsafe code, raw-pointer misuse
- C#: unsafe pointers, disposed objects, invalid Span/Memory lifetime assumptions, handles/resources used after disposal
- Go: invalid resource lifetime assumptions, unsafe.Pointer misuse, references affected by container or buffer reuse where statically detectable
- Python: closed files/resources, invalidated iterators/generators, objects used after explicit teardown when detectable
- TypeScript/JavaScript: disposed/closed application resources, invalidated handles, stale references in APIs that expose explicit lifecycle semantics

Suggested severity guidance:

- `危険`: invalid lifetime, use-after-release, double release, invalid dereference, or equivalent failure is effectively certain from static evidence.
- `警告`: ownership, lifetime, or invalidation handling is highly suspicious but not provably invalid.
- `注意`: the reference or ownership contract is ambiguous or review-worthy, but there is not enough evidence to infer likely failure.

The checker must not flag a construct merely because it uses references or pointers. Diagnostics should be based on concrete ownership, lifetime, invalidation, nullability, or retention evidence.

C++ support may substantially expand this rule family because the language exposes more direct pointer and lifetime hazards, but the category itself is not C++-specific.

## Ambiguous or confusing symbol resolution

The checker should detect cases where multiple functions, methods, or symbols with the same name become visible through imports, includes, namespaces, modules, using directives, or equivalent mechanisms, and a call becomes difficult to interpret safely.

This category is primarily advisory.

Typical severity:

- `注意`: the call resolves successfully, but multiple visible symbols with the same name make the intent unclear or fragile.
- `警告`: resolution is technically valid but depends on subtle precedence, overload, namespace, extension-method, import-order, or shadowing rules that make accidental misuse highly plausible.
- `危険`: only when the language/toolchain itself considers the call unresolved or genuinely ambiguous and failure is effectively certain.

Candidate patterns include:

- a user-defined function has the same name as an imported function
- multiple imported modules expose the same function name
- wildcard/star imports introduce same-named symbols
- namespace/using directives make several overload sets visible
- extension methods or equivalent mechanisms introduce competing call targets
- a local definition shadows an imported function with the same name
- include/import changes could silently change which symbol a call resolves to
- the call relies on subtle overload resolution while several user-defined candidates are visible

Example diagnostic:

```text
ACI2xx 注意 同名の関数が複数のインポート元とユーザー定義コードから参照可能です。呼び出し先が分かりにくいため、明示的な名前空間または修飾名の使用を確認してください
```

The checker should distinguish between:

1. a truly ambiguous call that the compiler/type checker will reject, and
2. a call that resolves successfully but is confusing or fragile for humans.

The first belongs to obvious-error detection. The second belongs to this advisory rule family.

## Missing comments in non-trivial code

The checker should emit an advisory diagnostic when a non-trivial source file or sufficiently large code region contains no comments at all.

Typical severity:

- `注意`: no comments are present in code large enough that some explanation may be useful.
- `警告`: not used by default for this category.
- `危険`: not used for this category.

The purpose of this rule is not to require comments on every function or line. Comment quantity is not a direct measure of code quality, and clear code may legitimately need few comments.

The rule should therefore avoid triggering on:

- very small files
- generated code
- trivial data-only declarations
- simple configuration wrappers
- files explicitly excluded from documentation/comment checks

Candidate trigger conditions may include:

- a source file exceeds a configurable size threshold and contains no comments
- a large class/module contains no comments at all
- a substantial implementation region contains no explanatory comments despite high complexity

Example diagnostic:

```text
ACI2xx 注意 このファイルにはコメントがありません。処理量が多いため、意図や前提条件を説明するコメントが必要ないか確認してください
```

This rule should remain advisory and should not attempt to judge whether the code is poorly designed.

## Excessive control-flow nesting

The checker should detect control-flow structures that are nested unusually deeply.

This is primarily a readability and maintainability diagnostic rather than a correctness error.

Typical severity:

- `注意`: nesting is deeper than the normal advisory threshold.
- `警告`: nesting is extremely deep and the control flow is difficult to follow safely.
- `危険`: not normally used for this category.

Candidate constructs include language-equivalent forms of:

- `if / else`
- `for`
- `while / do-while`
- `switch / case`
- `match`
- `try / catch / finally`
- nested callbacks / closures where they materially increase control-flow depth

The metric should be based on AST/control-flow nesting depth rather than raw indentation or line count.

The implementation should allow language-specific adjustments because some languages naturally express certain constructs with different AST shapes.

Example diagnostics:

```text
ACI2xx 注意 制御構造のネストが深くなっています。処理の流れが追いにくくないか確認してください
```

```text
ACI2xx 警告 制御構造のネストが非常に深く、処理経路の把握が困難になっています
```

Suggested behavior:

- emit `注意` relatively aggressively once the configured advisory depth is exceeded
- promote to `警告` only at a substantially deeper threshold or when deep nesting is combined with many branches
- do not prescribe a specific refactoring; only report the measurable nesting condition

## Additional rule families accepted for implementation

The following rule families are accepted as part of the project's intended scope.

### Ignored return values and errors

Detect important return values or error objects that are discarded even though the called API indicates they should be checked.

Examples include:

- ignored `error` values in Go
- discarded status/result values
- ignored failure-return values
- APIs marked or documented as requiring result handling

Typical severity:

- `警告`: failure handling is likely required and ignoring the result can easily create bugs
- `注意`: the result is review-worthy but not clearly mandatory
- `危険`: only when ignoring the result makes failure effectively certain

### Swallowed exceptions and errors

Detect error handling that suppresses failures without meaningful handling.

Examples include:

- empty `catch`
- `except: pass`
- receiving an error and doing nothing
- logging nothing and returning success after a failed operation
- broad exception handlers that silently continue

Typical severity:

- `警告` by default
- `注意` when intentional suppression is plausible
- `危険` when the suppressed failure makes incorrect behavior effectively certain

### Conditions that are always true or always false

Detect conditions that can be proven from static analysis to never change result.

Examples include:

- comparisons against compile-time constants that are impossible
- contradictory boolean expressions
- branches made unreachable by previous conditions
- repeated range checks that cannot both be true
- null checks on values statically known to be non-null or null

Typical severity:

- `危険`: the condition is statically proven and indicates broken logic
- `警告`: the condition is highly likely to be constant but depends on incomplete analysis

### Variable and symbol shadowing

Detect inner-scope declarations that hide outer variables, parameters, imported names, or other visible symbols in ways that can cause confusion or accidental misuse.

Typical severity:

- `注意` by default
- `警告` when the shadowed and shadowing symbols have different types, meanings, or are very close in scope and likely to be confused

### Excessive parameter count

Detect functions or methods with unusually many parameters.

This is advisory and should not be treated as a design error by itself.

Typical severity:

- `注意` by default
- `警告` only when combined with other strong signals such as many same-typed adjacent parameters, very high function complexity, or a large number of optional/boolean control parameters
- `危険` is not used for parameter count alone

### Same-type argument swap risk

Detect APIs where multiple adjacent arguments have the same or compatible types and are easy to accidentally swap.

Examples include:

```text
move(x, y, width, height)
copy(source, destination)
connect(host, user, database)
```

This rule should use parameter names, argument names, types, and call-site context where available.

Typical severity:

- `注意` by default
- `警告` when a call site's argument names or data flow strongly suggest that arguments may have been reversed
- `危険` only when the swap can be proven to produce invalid behavior

### Resource release leaks

Detect execution paths where acquired resources are not released correctly.

Examples include:

- file handles
- sockets
- database connections
- locks
- streams
- temporary resources
- disposable/closable objects

Typical severity:

- `危険`: a release leak is statically certain on an execution path
- `警告`: a leak is highly likely
- `注意`: resource lifetime is ambiguous and worth review

### Async and concurrency misuse

Detect common async/concurrency patterns that are valid syntax but likely to misbehave.

Examples include:

- missing `await`
- fire-and-forget operations with lost failures
- synchronously blocking on asynchronous work
- ignored task/future/promise errors
- lock misuse
- obvious deadlock-prone ordering when statically inferable
- async callbacks passed to APIs that do not await them
- concurrent mutation without the expected synchronization where strongly detectable

Typical severity:

- `危険`: failure/deadlock/lost execution is effectively certain
- `警告`: the pattern has a high probability of creating a runtime bug
- `注意`: the pattern is suspicious but depends on surrounding runtime behavior

These rules should be implemented incrementally and language-by-language. A rule may exist conceptually across all supported languages even when only some language adapters can enforce it at first.

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


### Severity philosophy

### Severity and bug-risk wording

Severity should also reflect how strongly a detected pattern is connected to future bugs.

A useful wording scale is:

- `危険`: バグの原因になる、またはほぼ確実にバグの原因になる
- `警告`: かなり近い将来、バグを生む可能性が高い
- `注意`: バグの原因になりかねない、またはレビューしておく価値がある

This is not intended as a mathematically precise probability model. It is a practical wording guideline for keeping diagnostics consistent.

The distinction is especially important for maintainability-oriented rules:

- deep nesting is usually `注意`
- extremely deep and branch-heavy nesting may become `警告`
- deep nesting alone should not become `危険`, because it does not inherently make the program fail

Likewise, advisory rules may still be emitted frequently when they merely indicate a pattern that can become a bug source later.


The three severity levels are not intended to have equal emission thresholds.

- `危険` should be conservative and require strong evidence of an actual failure, invalid operation, or serious safety problem.
- `警告` should also be relatively conservative and indicate a high probability of failure, misuse, or problematic behavior.
- `注意` is intentionally lightweight and may be emitted much more aggressively.

`注意` should be understood literally as "something worth noticing or reviewing", not as a near-error condition.

This follows the same general policy used by other Tomiya-produced checkers: low-severity advisory diagnostics are allowed to be frequent as long as they remain understandable and useful.

Examples appropriate for `注意` include:

- no comments in a non-trivial file
- confusing but technically valid same-name symbol resolution
- unusually large functions that may deserve review
- ambiguous ownership or lifecycle patterns without enough evidence for a warning
- maintainability or readability concerns that are measurable but not necessarily wrong

Users should therefore expect `注意` diagnostics to appear more often than `警告` or `危険`.

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
