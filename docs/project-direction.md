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

## Optional coverage checking

Test coverage checking should be available as an optional feature that users can enable or disable.

The intended UX is checkbox/toggle based in higher-level integrations, with an equivalent configuration option for CLI/config-file usage.

Coverage checking is not mandatory for every project because:

- some repositories may not have tests yet
- some test frameworks do not expose coverage uniformly
- generated code and integration-heavy projects may have misleading raw percentages
- coverage percentage alone is not a correctness metric

When enabled, the checker may:

- run or consume the project's coverage tool
- read line / branch / function coverage when available
- compare results against configured thresholds
- report missing or unexpectedly low coverage
- exclude generated, vendored, or explicitly ignored paths

Suggested severity behavior:

- `注意`: coverage is below a recommended/advisory threshold
- `警告`: coverage is substantially below a configured required threshold or has dropped significantly
- `危険`: generally not used for coverage percentage alone

A project should be able to choose whether coverage is checked and configure thresholds independently from the static-analysis rules.

Conceptually:

```text
[ ] coverage check

when enabled:
  line coverage threshold
  branch coverage threshold
  function coverage threshold
```

Exact UI and configuration syntax can be decided later.

## Unit / function-level test execution

The checker should also support ordinary CI-style unit testing and function-level test execution.

This is not a special static-analysis feature. It should behave similarly to common CI checkers: detect the project's existing test setup, run the appropriate test command or framework, collect failures, and normalize the results into the all-code-checker diagnostic/reporting model.

The checker should prefer existing project configuration and test frameworks rather than imposing a new test system.

Examples may include:

- TypeScript / JavaScript: project-defined test scripts and frameworks such as Vitest, Jest, or equivalent
- C#: .NET test projects
- Python: pytest, unittest, or project-defined runners
- Go: `go test`

Future language adapters should provide equivalent behavior for their ecosystems.

The intended responsibilities are:

- discover test projects / test files
- identify the configured or conventional test runner
- execute unit/function-level tests
- report failed tests and execution errors
- optionally collect coverage when the coverage option is enabled
- preserve project-specific test configuration

Test failures are ordinary CI failures and should be clearly separated from advisory static-analysis diagnostics.

A failed unit/function-level test is classified as `危険`.

Example:

```text
ACI3xx 危険 単体テストが失敗しました: expected 200, got 500
```

A failed test should cause the checker run to be considered unsuccessful unless the user explicitly configures otherwise.

### Static test simulation when no test environment exists

When a project has no usable unit-test environment, the checker should still attempt a limited static simulation of function behavior where practical.

This may use:

- AST evaluation
- constant propagation
- control-flow analysis
- data-flow analysis
- lightweight symbolic execution
- known standard-library semantics
- statically known function inputs and return paths

The goal is not to replace a real runtime or full test framework. The simulation is best-effort and must remain bounded.

If the checker can determine that a simulated function path is likely to fail or produce invalid behavior, it should emit `警告`, not `危険`, because the result comes from static simulation rather than an actually executed test.

Example:

```text
ACI3xx 警告 テスト環境が見つからなかったため静的シミュレーションを実行しました。この関数は特定の入力経路で正常に完了しない可能性があります
```

The checker should distinguish clearly between:

- real test failure → `危険`
- static simulation indicates likely failure → `警告`
- simulation is inconclusive → no failure diagnostic, or at most `注意` if the lack of verifiability itself is worth surfacing

Static simulation must not claim runtime certainty when external I/O, reflection, dynamic loading, native calls, concurrency, randomness, time-dependent behavior, or other runtime-only state prevents reliable analysis.

## Configuration file validation

The checker should also validate configuration and data files that are consumed by the project.

### JSON

JSON support should include more than syntax validation.

The checker should attempt to detect:

- invalid JSON syntax
- duplicate or suspicious keys where relevant
- available JSON Schema files
- schema declarations such as `$schema`
- project-local mappings between JSON files and schemas
- code locations where each JSON file is loaded or consumed
- mismatches between the JSON structure and the consuming code when statically inferable
- required fields that are missing according to schema or known access patterns
- fields with incompatible types
- values that violate enum/range/pattern constraints from JSON Schema
- configuration files that appear unused
- code that assumes fields which the JSON/schema does not guarantee

When a JSON Schema is available, schema validation should be preferred over heuristic inference.

When no schema is available, the checker may still infer expected structure from project code that reads the JSON.

Example flow:

```text
config.json
  ↓
detect JSON Schema if available
  ↓
find project code that loads config.json
  ↓
compare schema + actual JSON + code access patterns
  ↓
emit diagnostics
```

Suggested severity:

- `危険`: invalid JSON, definite schema violation, or a statically certain mismatch that will prevent correct loading
- `警告`: the configuration is likely incompatible with the consuming code or expected schema
- `注意`: suspicious/unused keys, missing documentation/schema, or ambiguous configuration usage

### INI

INI files should also be supported as configuration inputs.

Candidate checks include:

- malformed section/key syntax
- duplicate sections or keys
- missing sections/keys required by consuming code
- invalid values when expected types can be inferred
- keys that are read by the code but absent from the file
- keys present in the file but never referenced
- inconsistent naming/casing where the parser semantics make this relevant
- project code loading a different INI file than expected

INI does not have one universal schema standard comparable to JSON Schema, so structure may need to be inferred from:

- project code
- application defaults
- templates/example config files
- explicit checker configuration

Suggested severity follows the same model:

- `危険`: parse failure or definite required-value mismatch
- `警告`: high-confidence incompatibility with consuming code
- `注意`: unused, ambiguous, undocumented, or suspicious configuration

### Future configuration formats

The configuration-analysis subsystem should be designed so that additional formats can be added later without changing the core model.

Likely future formats include:

- YAML
- TOML

The core abstraction should therefore separate:

```text
file format parsing
schema / expected-structure discovery
project load-site discovery
value/type validation
diagnostic generation
```

This allows JSON, INI, and future configuration formats to share the same project-level analysis pipeline.

## Markup and structured document validation

The checker should validate XAML, XML, and HTML files.

For v1.0.0, this support should focus on whether the document is structurally broken or very likely broken. More detailed framework-specific or semantic checks are intentionally deferred until after v1.0.0.

### XAML

Initial checks may include:

- malformed XML/XAML syntax
- unclosed or incorrectly nested elements
- invalid attribute syntax
- duplicate attributes where invalid
- broken namespace declarations
- references that are structurally impossible to resolve when this can be determined without deep framework analysis

Severity:

- `危険`: the XAML is structurally invalid or cannot be parsed correctly
- `警告`: the XAML appears structurally suspicious or likely to fail at runtime, but certainty is insufficient for `危険`

More detailed checks such as framework-specific binding analysis, resource resolution, style/template semantics, or advanced UI-framework validation are post-v1.0.0 scope.

### XML

Initial checks may include:

- malformed XML syntax
- mismatched or unclosed tags
- invalid entity/reference syntax
- duplicate attributes
- invalid namespace structure
- document structure that cannot be parsed as XML

Severity:

- `危険`: definite parse/structure failure
- `警告`: suspicious but not conclusively invalid structure or references

Schema-based XML validation such as XSD or deeper application-specific XML semantics may be added after v1.0.0.

### HTML

Initial checks may include:

- clearly broken tag structure
- impossible or malformed attribute syntax
- severely mismatched nesting
- invalid document fragments where the parser cannot recover reliably
- broken embedded references when statically obvious

HTML parsers are intentionally forgiving, so the checker should distinguish between:

- structurally broken markup that is highly likely to cause incorrect behavior → `危険`
- markup that browsers may recover from but is suspicious or fragile → `警告`

More detailed HTML checks such as accessibility, semantic HTML quality, framework-specific template semantics, SEO, or style recommendations are post-v1.0.0 scope.

### v1.0.0 scope boundary

For XAML, XML, and HTML, v1.0.0 should prioritize:

```text
definitely broken
→ 危険

probably broken / structurally suspicious
→ 警告
```

Fine-grained validation and framework-specific analysis should be deferred until after v1.0.0.

## Future CSS validation and cascade analysis

CSS support is planned for after v1.0.0.

Initial post-v1 CSS support should include ordinary syntax validation, but the more valuable checks are likely to involve cascade and priority ambiguity.

Candidate checks include:

- malformed CSS syntax
- duplicate or conflicting declarations
- selectors with unexpectedly competing specificity
- rules whose outcome depends heavily on source order
- excessive or conflicting `!important`
- multiple selectors targeting the same elements with near-equal specificity
- overrides that are valid but difficult to reason about
- dead or effectively unreachable declarations where statically inferable
- custom properties that are referenced but not defined in reachable scope
- conflicting media/container-query branches where the final result is difficult to determine

Suggested severity:

- `危険`: definite parse failure or a statically certain invalid CSS construct
- `警告`: cascade/specificity conflicts make the effective style ambiguous, fragile, or highly order-dependent
- `注意`: maintainability concerns such as repeated overrides or unusually complex selector chains

In particular, ambiguous priority should normally produce `警告`, not `危険`, because the stylesheet may still render successfully even when the resulting style is hard to predict or maintain.

CSS support is explicitly post-v1.0.0 scope.

## Implementation language

The main implementation language of `all-code-checker` is C# on .NET.

C# is responsible for the application's central orchestration layer, including:

- checker core
- project/input discovery
- analyzer orchestration
- normalized ACI diagnostics
- CUI
- GUI
- configuration handling
- result aggregation
- CI exit/status handling

The intended architecture is approximately:

```text
                ┌─ CUI
                │
C# Checker Core ├─ GUI
                │
                ├─ C# analyzer
                ├─ TypeScript analyzer
                ├─ Python analyzer
                ├─ Go analyzer
                └─ future analyzers
```

C# being the main implementation language does not require every language parser or analyzer to be reimplemented in C#.

Where a target language already provides a reliable parser, compiler API, semantic model, or type-analysis implementation, the checker may use that language's native tooling and normalize the result into the common ACI diagnostic model.

Examples include:

- C#: Roslyn directly from the .NET process
- TypeScript: TypeScript Compiler API through an analyzer adapter/helper
- Go: Go parser/type tooling through an analyzer adapter/helper
- Python: Python AST/static-analysis tooling where appropriate

This avoids recreating full compiler frontends while keeping the user-facing checker, configuration, reporting, CUI, and GUI unified in C#/.NET.

The CUI and GUI must share the same C# checker core.

## Input model

The checker should accept three primary input forms.

### 1. Project parent directory

A directory may be supplied as the input root.

Example:

```text
all-code-checker ./my-project
```

The checker should inspect the directory contents, detect project boundaries, supported languages, project files, configuration files, test environments, and entry points.

A directory input may contain multiple projects or multiple languages.

### 2. Project file

A project/build definition file may be supplied directly.

Examples include:

- Visual Studio solution/project files such as `.sln` / `.csproj`
- CMake project files
- equivalent project/build metadata for supported ecosystems

Example:

```text
all-code-checker MySolution.sln
```

When a project file is supplied, the checker should treat that file as the primary project boundary and resolve referenced source/configuration/test files from it.

### 3. Single file containing an entry point

A single source file may be supplied when it contains a program entry point or otherwise represents an executable starting point.

Example:

```text
all-code-checker main.py
all-code-checker Program.cs
all-code-checker main.go
```

For single-file input, the checker should not necessarily limit analysis to that file only.

Where practical, it should follow:

- imports
- includes
- modules/packages
- referenced source files
- configuration/data files loaded by the entry point
- directly related project metadata

to construct the smallest practical analysis scope around the entry point.

### Input resolution principle

The input identifies the analysis root, not always the exact complete file set.

Conceptually:

```text
input
  ↓
resolve project / source root
  ↓
discover language + project boundaries
  ↓
discover referenced files and configuration
  ↓
run analysis
```

The checker should avoid scanning unrelated sibling projects unless the selected input root/project explicitly includes them.

## Severity filtering

Users should be able to enable or disable each diagnostic severity independently.

The three user-facing severity groups are:

- `危険`
- `警告`
- `注意`

Default behavior should enable all three.

Conceptually:

```text
[x] 危険
[x] 警告
[x] 注意
```

Disabling a severity means diagnostics at that severity are not shown in the normal result output.

The internal analyzer may still perform analysis needed to determine stronger diagnostics or shared data-flow results. Severity filtering should therefore be treated primarily as output/report filtering rather than blindly disabling all analysis work associated with that level.

This allows users to choose different operating styles, for example:

```text
CI gate:
[x] 危険
[x] 警告
[ ] 注意

strict review:
[x] 危険
[x] 警告
[x] 注意

critical-only:
[x] 危険
[ ] 警告
[ ] 注意
```

The exact exit-code policy for enabled/disabled severities can be finalized together with CI behavior.

## User interfaces

The project should provide both a CUI version and a GUI version.

### CUI

The CUI is intended for:

- CI environments
- local terminal use
- scripts and automation
- GitHub Actions and other CI services
- headless environments

The CUI should accept the same project inputs defined by the input model and expose configuration options for enabled severities and optional checks.

Illustrative syntax:

```text
all-code-checker ./project
all-code-checker ./project --no-notice
all-code-checker ./project --no-warning
all-code-checker ./project --danger-only
```

The exact command-line option names are not fixed yet.

### GUI

The GUI is intended for interactive local use.

It should provide, at minimum:

- input selection for folder / project file / entry-point file
- checkboxes for `危険 / 警告 / 注意`
- optional-feature toggles such as coverage checking
- start/check action
- grouped diagnostic results
- file/location information for each diagnostic
- easy navigation from a diagnostic to the relevant source location where possible

The GUI and CUI should share the same checker core rather than implementing separate analysis logic.

Conceptually:

```text
            ┌───────── CUI
checker core┤
            └───────── GUI
```

This shared-core design is required so that the same input produces equivalent analysis results regardless of interface.

## Missing project / definition metadata

The checker should detect when a language or project type normally requires a definition/build/project metadata file, but that file is missing.

This rule must be language- and project-aware.

Some languages or execution modes can be valid without an explicit project-definition file. Those cases should be treated as already defined and should not produce a diagnostic merely because a metadata file is absent.

Examples of project-definition or metadata files may include:

- solution/project files
- build configuration files
- package/dependency manifests
- module/workspace definitions
- language-specific project metadata
- application manifests required by a known project type

The rule should first determine whether the detected language/project mode actually requires such a file.

Conceptually:

```text
detected source/project
  ↓
does this language/project mode require a definition file?
  ├─ no  → treat as valid/defined
  └─ yes
       ↓
     required file exists?
       ├─ yes → continue
       └─ no  → 警告
```

Typical severity:

- `警告`: a required project/definition file is missing and normal build, dependency resolution, or project discovery is likely to fail or behave incorrectly.
- `注意`: may be used only when the file is conventional rather than strictly required.
- `危険`: should generally not be used for absence alone unless the checker can prove that execution/build is impossible.

The checker should avoid assuming that every source tree needs a project file.

Examples:

- a standalone script language file may be valid without a project manifest
- a single-file program may be valid in languages that support standalone execution
- a recognized solution/project format may require its referenced project files to exist
- a package/module mode may require its manifest or module definition

The exact required-file rules should live in each language/project adapter so they can evolve independently.

## Cyclomatic complexity diagnostics

The checker should calculate cyclomatic complexity for functions/methods where the language adapter can provide reliable control-flow information.

This overlaps with excessive nesting and oversized-function checks, but it measures a different property:

- nesting depth measures how deeply control structures are nested
- cyclomatic complexity measures how many independent execution paths exist

A function may have shallow nesting but still have high cyclomatic complexity due to many independent branches.

Typical severity:

- `注意`: cyclomatic complexity exceeds the advisory threshold
- `警告`: complexity is very high and the number of possible execution paths makes maintenance and testing difficult
- `危険`: not used for complexity alone

Candidate contributors include language-equivalent forms of:

- `if / else if`
- loops
- `case` / `match` branches
- conditional expressions
- short-circuit boolean branches where appropriate
- exception/error handling paths

The checker should avoid using one universal threshold blindly across every language. Default thresholds may be shared, but language-specific adjustments should be possible.

Example diagnostics:

```text
ACI2xx 注意 この関数の循環的複雑度が高く、分岐経路が多くなっています
```

```text
ACI2xx 警告 この関数の循環的複雑度が非常に高く、テスト漏れや保守時のバグにつながる可能性があります
```

Cyclomatic complexity should be considered together with other signals such as nesting depth, function size, and branch count, but it should remain available as an independent diagnostic.

## Duplicate code detection

The checker should detect substantial duplicated code that is highly likely to be a maintenance problem and an obvious candidate for extraction into a shared function/method/helper.

This rule is not intended to flag every repeated line, common idiom, boilerplate, generated code pattern, or short guard clause.

The target is duplication that most reviewers would reasonably consider "this should probably be shared".

Candidate signals include:

- large identical or near-identical AST subtrees
- repeated statement sequences with only variable/literal substitutions
- duplicated branching structure
- duplicated validation / transformation / I/O sequences
- the same multi-step operation repeated across multiple functions
- duplicated blocks large enough that future fixes would need to be applied in several places
- three or more similar copies of the same logic, even when each copy has minor differences

Detection should prefer normalized syntax/AST comparison over raw text matching so that harmless renaming or formatting differences do not hide meaningful duplication.

Possible normalization may ignore or abstract:

- variable names
- literal values where appropriate
- whitespace / formatting
- comments
- trivial syntactic differences that preserve the same operation structure

The checker should avoid aggressively flagging:

- very short snippets
- standard language boilerplate
- trivial null/argument checks
- generated code
- test data that is intentionally duplicated
- framework-required repetitive declarations
- simple getters/setters or repetitive mappings unless the repeated logic is substantial

Typical severity:

- `注意`: substantial duplicated logic exists and is worth consolidating
- `警告`: a large or widely repeated block is highly likely to cause inconsistent fixes or future bugs
- `危険`: not used for duplication alone

Example diagnostics:

```text
ACI2xx 注意 似た処理が複数箇所に重複しています。共通関数へまとめられないか確認してください
```

```text
ACI2xx 警告 大きな処理ブロックが複数箇所に重複しており、修正漏れによる不整合を生む可能性が高くなっています
```

Duplicate-code detection may be implemented with a similarity score, but the user-facing result should not imply that similarity percentage alone determines code quality. The score should only support detection of clearly duplicated behavior.

## Unused and apparently disconnected code

The checker should detect code that is clearly unused or disconnected from the active project.

This category is primarily advisory because unused code may be intentional, experimental, temporarily unfinished, or reserved for future work.

Candidate targets include:

- functions/methods with no references
- private types/classes/modules with no references
- unused local variables
- unused imports/includes
- unreachable helper code
- constants or fields that are never read
- configuration values or handlers that are defined but never consumed
- code paths that are structurally disconnected from all known entry points
- duplicate legacy implementations that are no longer referenced

Typical severity:

- `注意`: code is clearly unused, but may be intentionally unfinished or retained for future work
- `警告`: the symbol appears to be part of an expected active flow but is not connected or referenced where it likely should be
- `危険`: not used for unused-code status alone

Possible stronger-warning signals include:

- an event/callback handler follows the expected naming/signature convention but is never registered
- a command/route/endpoint implementation exists but is never wired into the application
- a plugin/mod hook exists but is not registered
- a serializer/deserializer or configuration loader exists but is never called despite corresponding files being present
- a required lifecycle method appears implemented but is disconnected from startup/runtime flow
- an implementation is referenced only by dead code
- a symbol name and surrounding structure strongly imply required usage, but no reachable call/reference exists

The checker should be conservative when promoting unused code to `警告`. "Unused" alone is not evidence of a bug.

The default interpretation is:

```text
clearly unused
→ 注意

appears intended to be active, but is not wired into the reachable program
→ 警告
```

Language/framework-specific adapters may provide stronger evidence for registration-based systems, reflection-heavy frameworks, plugin systems, dependency injection, routes, event handlers, and similar patterns.

## Incomplete or dangerously weak error handling

The checker should detect error-handling code that exists but is clearly too weak to cover the realistic failure modes of the operation being protected.

This is broader than simply detecting swallowed exceptions. The target is error handling that gives the appearance of safety while leaving important failure paths uncovered.

Candidate patterns include:

- catching only one narrow exception/error while nearby operations can clearly produce several other likely failures
- checking only a success/failure boolean while ignoring an accompanying error/status payload
- handling an error but continuing with state that may already be invalid
- retry logic that does not cover the actual failure mode
- fallback logic that can itself fail without a second-level handler
- cleanup logic that runs only on some failure paths
- assuming a resource or result is valid after a partially failed operation
- broad operations with only one shallow guard around them
- asynchronous operations where only synchronous exceptions are handled
- network/file/database code that handles one obvious error but leaves equally likely failures uncovered
- error branches that log but do not prevent subsequent invalid use
- missing compensation/rollback handling after partial multi-step failure where the need is statically obvious

Typical severity:

- `注意`: error handling appears incomplete or fragile, but missing coverage is not certain
- `警告`: static analysis shows that a likely failure path is not handled and may leave the program in an invalid or inconsistent state
- `危険`: generally not used for error-handling quality alone unless the uncovered path already produces a separately provable fatal error

The checker should not judge error handling stylistically. It should base diagnostics on concrete uncovered failure paths, control-flow gaps, resource/state invalidation, and known API behavior.

Examples:

```text
ACI2xx 注意 この処理にはエラーハンドリングがありますが、想定される失敗経路の一部が処理されていない可能性があります
```

```text
ACI2xx 警告 このエラー処理では失敗後も無効な状態の値を利用する経路が残っています
```

This rule family should work together with:

- swallowed error detection
- resource lifetime analysis
- async/concurrency misuse detection
- return-value/error ignoring
- static test simulation

## Unnecessarily long or indirect execution paths

The checker should detect execution paths that appear to perform significantly more work, indirection, conversion, or control-flow traversal than is necessary for the apparent operation.

This category is advisory and should normally emit `注意`.

The checker must not assume that the shortest path is always correct. Some architectures intentionally route operations through validation, middleware, authorization, logging, transaction boundaries, event pipelines, adapters, or other required layers.

Therefore, this rule should be evaluated relatively rather than by a fixed path-length threshold alone.

Candidate signals include:

- repeated conversions between the same or equivalent representations
- values passed through many wrapper/helper layers without meaningful transformation
- repeated serialization/deserialization within one logical operation
- unnecessary round trips between modules or abstraction layers
- repeated lookup/reload of data already available in the current flow
- duplicated validation or normalization on the same path
- branching that repeatedly converges back to the same operation
- forwarding functions that add no observable behavior across several layers
- indirect call chains substantially longer than equivalent operations elsewhere in the same project
- repeated state read/write cycles where a simpler equivalent path exists

The analysis should consider project-local context such as:

- similar operations elsewhere in the repository
- whether each intermediate layer adds validation, state changes, security checks, or other meaningful behavior
- framework-required pipelines
- documented architecture or conventions where discoverable
- whether bypassing the path would change externally visible behavior

Typical severity:

- `注意`: the path is substantially more indirect or redundant than comparable paths and may deserve simplification
- `警告`: generally not used by default; may be considered only when the excessive path is strongly connected to a likely performance, consistency, or failure problem
- `危険`: not used for path inefficiency alone

Example:

```text
ACI2xx 注意 この処理は同種の処理と比べて多くの中間経路を通っています。各経路が必要か確認してください
```

This rule should avoid prescriptive refactoring advice and should not classify intentional architectural routing as a defect merely because it is longer.

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
