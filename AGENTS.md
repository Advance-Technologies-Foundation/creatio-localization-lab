# Agent instructions

## Repository purpose

This repository is the independent Advance Technologies Foundation reference example for schema-owned
localizable values in Creatio backend code and Freedom UI. Keep it small, executable, and suitable for
teaching. It is not part of the Clio source repository.

Read [README.md](README.md) before making changes. The README owns the lab walkthrough and the commands a
developer follows. This file owns contribution and implementation rules.

## Repository boundaries

- Work only inside this workspace unless the user explicitly authorizes coordinated changes elsewhere.
- Do not add lab packages, samples, screenshots, or tests to the Clio repository.
- Do not modify Clio or clio-knowledge while preparing this repository for inspection.
- Do not commit `Phase1.yaml`, `Phase2.yaml`, or `Phase3.yaml`. They are operator-specific environment
  lifecycle artifacts.
- Do not change the DLL-copying or dependency mechanics produced by Clio. The assemblies under the package
  and test `Libs` directories are part of the Clio-created package and unit-test scaffold.
- Do not commit `.application`, environment credentials, connection strings, or
  `.clio/workspaceEnvironmentSettings.json`.
- Do not commit or publish unless the user explicitly requests it.

## Canonical workspace structure

- `MainSolution.slnx` is the only solution entry point. It MUST contain both:
  - `packages/AtfLocalizationLab/Files/AtfLocalizationLab.csproj`
  - `tests/AtfLocalizationLab/AtfLocalizationLab.Tests.csproj`
- `packages/AtfLocalizationLab` is the single composable application package created with
  `clio ap AtfLocalizationLab -a`.
- `tests/AtfLocalizationLab` is the unit-test project created with
  `clio unit-test --package AtfLocalizationLab`.
- `.application` contains ignored Creatio build dependencies downloaded with `clio dconf`.
- Do not introduce a secondary test solution or obsolete task scripts that point to `.solution`.

## Localization ownership

Every localizable value MUST belong to a Creatio schema.

- Put a page value on the page schema that renders it.
- Put process, object, and other schema-specific values on their natural owning schema.
- Use `UsrAtfLocalizationLabMessages` only for package-level backend values that have no more natural owner.
- Do not turn the source-code schema into a package-wide registry for unrelated strings.
- Persist resource items as `LocalizableStrings.<Key>.Value`.
- Freedom UI code binds page values as `$Resources.Strings.<Key>`.

The lab MUST retain deterministic examples for:

1. A value translated in `en-US` and `es-ES`.
2. A value present only in the default language.
3. A missing key.

## Backend architecture

- Application code depends on `ILocalizableStringResolver`, not directly on Creatio Core's
  `LocalizableString`.
- `ILocalizableStringResolver` and `LocalizableStringResolver` remain together in
  `LocalizableStringResolver.cs`.
- Only `LocalizableStringResolver` constructs `LocalizableString`.
- The resolver exposes current-culture, strict-culture, and explicit-culture fallback operations.
- Pass `throwIfNoManager: false` by name and document that an unavailable resource produces `null`.
- Resolve behavior through constructor injection. Do not add an `I*Helper`, static service locator, or
  manually constructed behavior service.
- `ILocalizationLabService` and `LocalizationLabService` remain together in `LocalizationLabService.cs`.
- `AtfLocalizationLabService` is a thin transport entry point. It validates input, creates a DI scope,
  resolves `ILocalizationLabService`, delegates once, and maps a concrete response.
- The web service MUST NOT construct `LocalizableString` or implement localization behavior.
- Expected validation failures are returned as messages through `ErrorOr`; do not use `null` as a
  validation sentinel.
- Parse the requested culture once and reuse the resulting `CultureInfo`.

## C# coding style

- Place all `using` directives at the top of the file, outside the namespace.
- Assign computed values to a local variable before returning them. The example must remain easy to debug
  with breakpoints; rely on the compiler to optimize unnecessary locals.
- Do not use nested ternary expressions. Prefer explicit, breakpoint-friendly statements.
- Keep the interface and implementing class in one file for each small abstraction in this lab.
- Add XML documentation to public types and members. Put the authoritative contract documentation on the
  interface and use `inheritdoc` on the implementation.
- Preserve the platform-owned `UserConnection` lifetime. Inject `Func<UserConnection>`; do not register the
  connection as a container-owned scoped or transient service.
- Cache and reuse `JsonSerializerOptions`.
- Use `_` for intentionally discarded values.

## Test contract

Use NUnit, FluentAssertions, and NSubstitute. Tests use explicit Arrange, Act, Assert sections.
Every test method has a `Description` attribute, and every FluentAssertions assertion explains its
reason with `because`.

Use exactly these categories:

- `ResourceContent`: checks actual schema-owned resource files, item names, values, Freedom UI bindings,
  metadata, and deliberate fallback omissions. Do not inspect production C# source text in this category.
- `Implementation`: executes the concrete resolver, domain service, composition root, and thin web-service
  behavior without a live Creatio environment.
- `Creatio`: exercises the deployed package and merged Freedom UI bundle against the dedicated environment.

Do not replace behavioral tests with source-text assertions. Unit-test `LocalizableStringResolver` itself by
substituting `IResourceStorage` and `IResourceManager`, and assert the exact strict or fallback platform call.

Stand-free tests MUST maintain 100% line, branch, and method coverage for the production
`AtfLocalizationLab` assembly. Keep the Coverlet include scope and thresholds in the test project.

## Build and verification

Run commands from the workspace root. The verified acceptance configuration is Creatio .NET 8:

```powershell
clio dconf -e <environment>
dotnet build .\MainSolution.slnx -c dev-n8
dotnet test .\tests\AtfLocalizationLab\AtfLocalizationLab.Tests.csproj -c dev-n8 --filter "Category=ResourceContent"
dotnet test .\tests\AtfLocalizationLab\AtfLocalizationLab.Tests.csproj -c dev-n8 --filter "Category=Implementation"
dotnet test .\tests\AtfLocalizationLab\AtfLocalizationLab.Tests.csproj -c dev-n8 --filter "Category!=Creatio" /p:CollectCoverage=true
```

Run live tests only against an exclusively owned Creatio environment after synchronizing and compiling the
package:

```powershell
$env:CLIO_ENVIRONMENT = "<environment>"
dotnet test .\tests\AtfLocalizationLab\AtfLocalizationLab.Tests.csproj -c dev-n8 --filter "Category=Creatio"
```

## Completion gate

Before presenting the lab for inspection:

1. Confirm `MainSolution.slnx` lists the package and test project.
2. Build with zero errors.
3. Pass `ResourceContent` and `Implementation` independently.
4. Pass the combined 100% coverage gate.
5. Run the `Creatio` tests when the dedicated environment is available.
6. Confirm all `using` directives precede namespaces.
7. Confirm the English and Spanish screenshots still match the demonstrated Freedom UI behavior when UI
   resources change.

## Workspace diary

Keep `.codex/workspace-diary.md` concise and append-only. Record only durable discoveries, decisions, and
verification boundaries that are not already obvious from the code. Do not publish credentials, local
environment details, or investigative noise.
