# Task Completion Checklist

When a coding task is completed, verify the following:

## 1. Build
```bash
dotnet build
```
- Must pass with zero warnings (TreatWarningsAsErrors=true)
- Check for nullable reference type warnings
- Check for code style violations (EnforceCodeStyleInBuild=true)

## 2. Tests
```bash
dotnet test
```
- All existing tests must pass
- New code should have corresponding unit tests in `tests/CompoundDocs.Tests.Unit/`
- Use **Moq** for mocking, **Shouldly** for assertions, **xUnit** for test framework
- Integration/E2E tests if applicable

## 3. Coverage (for significant changes)
```bash
bash scripts/coverage-merge.sh
```
- Enforces 100% line+branch coverage threshold
- Coverage config in `coverlet.runsettings`
- Excludes: `Program` classes, `*ServiceCollectionExtensions`, `[ExcludeFromCodeCoverage]` attributed members

## 4. Code Quality
- File-scoped namespaces
- Private fields: `_camelCase`
- Interfaces: `I` prefix
- `var` usage preferred
- No compiler warnings
- Nullable annotations correct

## 5. Commit
- Follow Conventional Commits: `type(scope): lowercase description`
- Pre-commit gitleaks hook will run automatically
- CI validates PR titles against conventional commit format
