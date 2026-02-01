# Task Completion Checklist

When completing a task, ensure:

1. **Build Verification**
   ```bash
   dotnet build
   ```
   Ensure no compilation errors

2. **Code Style**
   - XML documentation on public APIs
   - Proper nullable annotations
   - File-scoped namespaces
   - Sealed classes where appropriate

3. **Testing** (when tests exist)
   ```bash
   dotnet test
   ```

4. **Do NOT commit** unless explicitly requested
