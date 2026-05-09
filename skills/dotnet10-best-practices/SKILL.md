---
name: dotnet10-best-practices
description: 'Review and refactor .NET 10 / ASP.NET Core projects to align with official Microsoft best practices for performance, reliability, and maintainability.'
---

# .NET 10 / ASP.NET Core Best Practices Review

## Overview

Review the specified .NET 10 / ASP.NET Core project and apply best practices from the [official Microsoft guidelines](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0). Focus on actionable code changes — not style or cosmetic improvements.

## Review Checklist

Scan the codebase for the following categories. For each issue found, apply the fix directly. Skip categories that are not applicable to the project.

---

### 1. Avoid Blocking Calls

**Anti-patterns to find and fix:**
- `Task.Wait()`, `Task.Result`, `.GetAwaiter().GetResult()` — replace with `await`
- `Task.Run()` immediately awaited in ASP.NET Core — remove the `Task.Run` wrapper
- Synchronous I/O on `HttpRequest`/`HttpResponse` body — use async overloads

**Rules:**
- All controller/Razor Page actions must be `async Task<T>`
- All data access and I/O calls must use async APIs
- Never block on async code in request-handling paths

---

### 2. Pool HTTP Connections with HttpClientFactory

**Anti-patterns to find and fix:**
- `new HttpClient()` — replace with `IHttpClientFactory` or named/typed clients
- `HttpClient` created in constructors and stored as fields without DI

**Fix pattern:**
```csharp
// Registration (Program.cs / Startup)
builder.Services.AddHttpClient("GitHub", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0");
});

// Usage (via DI)
public class MyService(IHttpClientFactory httpFactory)
{
    public async Task DoWork()
    {
        using var http = httpFactory.CreateClient("GitHub");
        // ...
    }
}
```

---

### 3. Minimize Large Object Allocations

**Anti-patterns to find and fix:**
- `HttpClient.GetByteArrayAsync()` for large files — use streaming instead
- `File.ReadAllBytesAsync()` / `File.WriteAllBytesAsync()` for large files
- Large `byte[]` allocations in hot code paths

**Fix pattern (streaming download):**
```csharp
// Before (LOH allocation):
var data = await http.GetByteArrayAsync(url);

// After (streaming):
using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
response.EnsureSuccessStatusCode();
using var stream = await response.Content.ReadAsStreamAsync(ct);
using var fileStream = File.Create(path);
await stream.CopyToAsync(fileStream, ct);
```

**Consider:**
- `ArrayPool<T>` for frequently allocated buffers
- `Span<T>` / `Memory<T>` for slice operations

---

### 4. Response Compression

**Check:** Is `UseResponseCompression()` configured?

**Fix pattern:**
```csharp
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes
        .Concat(["application/json"]);
});

// In pipeline (before UseStaticFiles):
app.UseResponseCompression();
```

---

### 5. Response Caching

**Check:** Are cacheable endpoints marked with `[ResponseCache]`?

**Fix pattern:**
```csharp
// Rarely-changing endpoints
[HttpGet("information")]
[ResponseCache(Duration = 300)]  // 5 minutes
public ActionResult<InfoResponse> GetInfo() { ... }

// Registration
builder.Services.AddResponseCaching();
app.UseResponseCaching();  // After UseResponseCompression
```

---

### 6. Structured Logging with ILogger

**Anti-patterns to find and fix:**
- `Console.WriteLine()` in services/controllers — replace with `ILogger<T>`
- String interpolation in log messages — use structured logging parameters

**Fix pattern:**
```csharp
// Before:
Console.WriteLine($"Processing package {packageId}");

// After:
_logger.LogInformation("Processing package {PackageId}", packageId);
```

**Rules:**
- All controllers and services should accept `ILogger<T>` via constructor injection
- Use `LogDebug` for diagnostic detail, `LogInformation` for operational events, `LogWarning` for recoverable issues, `LogError` for failures
- Use named placeholders (`{PackageId}`) not string interpolation

---

### 7. CancellationToken Propagation

**Check:** Do async methods accept and forward `CancellationToken`?

**Anti-patterns to find and fix:**
- Async methods without `CancellationToken` parameter
- `CancellationToken` accepted but not passed to downstream calls

**Rules:**
- All public async methods should accept `CancellationToken ct = default`
- Pass `ct` to every downstream async call
- Use `ct.ThrowIfCancellationRequested()` in long loops

---

### 8. IDisposable / IAsyncDisposable

**Check:** Do classes that own disposable resources implement `IDisposable`?

**Resources to check:**
- `HttpClient` (when not from factory)
- `SemaphoreSlim`
- `HttpClientHandler`
- `Stream` derivatives
- `Timer`

**Fix pattern:**
```csharp
public sealed class MyService : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(16);

    public void Dispose() => _semaphore.Dispose();
}
```

**Rules:**
- Callers should use `using` statements for disposable instances
- DI-managed singletons are disposed by the container

---

### 9. Pagination for Large Collections

**Check:** Do API endpoints returning collections support pagination?

**Anti-patterns to find and fix:**
- Returning `IEnumerable<T>` that iterates synchronously — use `IAsyncEnumerable<T>` or materialize with `ToListAsync()`
- Unbounded result sets — add `MaximumResults` or pagination support

---

### 10. Exception Handling

**Anti-patterns to find and fix:**
- Exceptions used for flow control — use conditional checks instead
- Empty `catch` blocks — at minimum log the exception
- Catching `Exception` too broadly in hot paths

**Rules:**
- Exceptions should be rare, not used for normal control flow
- Always log or handle caught exceptions meaningfully
- Use specific exception types when possible

---

## Execution Process

1. Scan the project for each checklist category
2. For each issue found, apply the fix directly
3. Track changes in a `progress.md` file with checkboxes:
   ```markdown
   # .NET 10 Best Practices Review Progress

   ## Findings
   - [ ] HttpClientFactory: Found N instances of `new HttpClient()`
   - [ ] Response Compression: Not configured
   - [ ] ILogger: N services using Console.WriteLine
   ...

   ## Changes Applied
   - [ ] Replaced `new HttpClient()` with IHttpClientFactory in X, Y, Z
   - [ ] Added response compression middleware
   ...
   ```
4. Run `dotnet build` to verify no compilation errors
5. Run `dotnet test` to verify no test regressions
6. Mark tasks as completed: `[ ]` → `[✓]`

## Scope

- ✅ Performance optimizations (async, streaming, pooling, compression, caching)
- ✅ Reliability improvements (IDisposable, CancellationToken, error handling)
- ✅ Observability (structured logging with ILogger)
- ❌ No code style or formatting changes
- ❌ No architecture refactoring beyond what's needed for best practices
- ❌ No new feature development

## References

- [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0)
- [HttpClientFactory](https://learn.microsoft.com/en-us/dotnet/standard/microservices-architecture/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- [Response Compression](https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-10.0)
- [Response Caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-10.0)
- [Logging in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
