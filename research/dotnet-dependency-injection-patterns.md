# .NET Dependency Injection Patterns: Comprehensive Guide

This comprehensive guide covers all major aspects of dependency injection (DI) in modern .NET applications, from fundamentals through advanced patterns and best practices.

## Table of Contents

1. [Fundamentals of .NET DI](#1-fundamentals-of-net-di)
2. [Setting Up DI with Generic Host](#2-setting-up-di-with-generic-host)
3. [Registration Patterns](#3-registration-patterns)
4. [Keyed Services (.NET 8+)](#4-keyed-services-net-8)
5. [Constructor Injection Patterns](#5-constructor-injection-patterns)
6. [IConfiguration Integration](#6-iconfiguration-integration)
7. [IOptions Pattern Family](#7-ioptions-pattern-family)
8. [Advanced Patterns](#8-advanced-patterns)
9. [Common Pitfalls and Anti-Patterns](#9-common-pitfalls-and-anti-patterns)
10. [Testing with DI](#10-testing-with-di)
11. [Third-Party DI Containers](#11-third-party-di-containers)

---

## 1. Fundamentals of .NET DI

### IServiceCollection and IServiceProvider

The .NET dependency injection system is built around two core abstractions:

**IServiceCollection**: A collection of `ServiceDescriptor` objects used to register services before building the service provider.

**IServiceProvider**: The built-in service container that creates and manages instances of registered services.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Create the host builder
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// IServiceCollection - register services
builder.Services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
builder.Services.AddScoped<IUserRepository, SqlUserRepository>();
builder.Services.AddTransient<IEmailService, SmtpEmailService>();

// Build the host - creates IServiceProvider internally
using IHost host = builder.Build();

// IServiceProvider - resolve services
using IServiceScope scope = host.Services.CreateScope();
IServiceProvider provider = scope.ServiceProvider;

IMessageWriter writer = provider.GetRequiredService<IMessageWriter>();
writer.Write("Hello, DI!");

host.Run();
```

### Service Lifetimes

.NET DI supports three service lifetimes:

| Lifetime | Behavior | Use Case |
|----------|----------|----------|
| **Singleton** | Single instance for entire application lifetime | Configuration, logging, caching, stateless services |
| **Scoped** | One instance per scope (HTTP request in web apps) | DbContext, request-specific data, unit of work |
| **Transient** | New instance created each time requested | Lightweight, stateless utilities |

```csharp
// Singleton - one instance for the entire application
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Scoped - one instance per HTTP request (or per scope)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Transient - new instance every time
builder.Services.AddTransient<IGuidGenerator, GuidGenerator>();
```

### When to Use Each Lifetime

**Singleton:**
- Thread-safe, stateless services
- Configuration providers
- Logging infrastructure
- Expensive-to-create services that can be shared
- Background services

```csharp
// Good singleton candidates
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<ILogger<Program>, Logger<Program>>();
builder.Services.AddSingleton<HttpClient>(); // Should use IHttpClientFactory instead
```

**Scoped:**
- Entity Framework DbContext (default)
- Request-specific data
- Services that maintain state for a single request
- Unit of Work pattern implementations

```csharp
// Good scoped candidates
builder.Services.AddScoped<ApplicationDbContext>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
```

**Transient:**
- Lightweight, stateless services
- Services that are inexpensive to create
- Services that shouldn't share state

```csharp
// Good transient candidates
builder.Services.AddTransient<IDateTimeProvider, DateTimeProvider>();
builder.Services.AddTransient<IValidator<User>, UserValidator>();
```

### Service Descriptors

Under the hood, `IServiceCollection` stores `ServiceDescriptor` objects:

```csharp
// Manual ServiceDescriptor creation
var descriptor = new ServiceDescriptor(
    serviceType: typeof(IMessageWriter),
    implementationType: typeof(ConsoleMessageWriter),
    lifetime: ServiceLifetime.Singleton);

builder.Services.Add(descriptor);

// Factory-based ServiceDescriptor
var factoryDescriptor = new ServiceDescriptor(
    serviceType: typeof(IConnectionFactory),
    factory: sp => new ConnectionFactory(
        sp.GetRequiredService<IConfiguration>()["ConnectionString"]),
    lifetime: ServiceLifetime.Scoped);

builder.Services.Add(factoryDescriptor);

// Instance-based ServiceDescriptor
var instance = new ConfigurationService();
var instanceDescriptor = new ServiceDescriptor(
    serviceType: typeof(IConfigurationService),
    instance: instance);

builder.Services.Add(instanceDescriptor);
```

---

## 2. Setting Up DI with Generic Host

### Host.CreateApplicationBuilder() vs Host.CreateDefaultBuilder()

**.NET 7+ (Modern - Recommended):**

```csharp
// Host.CreateApplicationBuilder - returns HostApplicationBuilder
// Linear, procedural style - easier to read and maintain
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Direct access to services, configuration, and environment
builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<IMyService, MyService>();

// Access configuration directly
string connectionString = builder.Configuration.GetConnectionString("Default");

// Access environment
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailService, FakeEmailService>();
}

IHost host = builder.Build();
host.Run();
```

**Legacy (.NET Core 2.1+):**

```csharp
// Host.CreateDefaultBuilder - returns IHostBuilder
// Callback-based style - more complex
IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<IMyService, MyService>();

        // Access configuration through context
        string connectionString = context.Configuration.GetConnectionString("Default");

        // Access environment through context
        if (context.HostingEnvironment.IsDevelopment())
        {
            services.AddSingleton<IEmailService, FakeEmailService>();
        }
    });

IHost host = hostBuilder.Build();
host.Run();
```

### WebApplication.CreateBuilder() for Web Apps

```csharp
// ASP.NET Core web applications
var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add custom services
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductRepository, SqlProductRepository>();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Default Services Registered by the Host

The generic host automatically registers these services:

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `IHostApplicationLifetime` | Singleton | App startup/shutdown events |
| `IHostLifetime` | Singleton | Controls when host starts/stops |
| `IHostEnvironment` | Singleton | Environment information |
| `ILogger<T>` | Singleton | Logging |
| `ILoggerFactory` | Singleton | Create loggers |
| `IConfiguration` | Singleton | Configuration access |
| `IOptions<T>` | Singleton | Options pattern |
| `IServiceScopeFactory` | Singleton | Create service scopes |

### ConfigureServices Patterns

**Extension Method Pattern (Recommended):**

```csharp
// ServiceCollectionExtensions.cs
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

        return services; // Enable method chaining
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<IEmailService, SmtpEmailService>();

        return services;
    }
}

// Program.cs - clean and organized
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddControllers();

var app = builder.Build();
```

---

## 3. Registration Patterns

### Basic Registration Methods

```csharp
// Type-based registration (most common)
builder.Services.AddSingleton<IService, ServiceImplementation>();
builder.Services.AddScoped<IRepository, SqlRepository>();
builder.Services.AddTransient<IValidator, DataValidator>();

// Implementation-only (no interface)
builder.Services.AddSingleton<ConcreteService>();

// Instance registration (no automatic disposal)
var instance = new ConfigService();
builder.Services.AddSingleton<IConfigService>(instance);
```

### Interface-to-Implementation Mapping

```csharp
public interface IMessageWriter
{
    void Write(string message);
}

public class ConsoleMessageWriter : IMessageWriter
{
    public void Write(string message) => Console.WriteLine(message);
}

public class FileMessageWriter : IMessageWriter
{
    private readonly string _filePath;

    public FileMessageWriter(string filePath)
    {
        _filePath = filePath;
    }

    public void Write(string message) => File.AppendAllText(_filePath, message);
}

// Registration
builder.Services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();

// Easy to swap implementations without changing consuming code
// builder.Services.AddSingleton<IMessageWriter, FileMessageWriter>();
```

### Factory Registrations with Delegates

```csharp
// Simple factory
builder.Services.AddSingleton<IDateTimeProvider>(sp => new DateTimeProvider());

// Factory with dependencies
builder.Services.AddScoped<IUserRepository>(sp =>
{
    var dbContext = sp.GetRequiredService<ApplicationDbContext>();
    var logger = sp.GetRequiredService<ILogger<SqlUserRepository>>();
    return new SqlUserRepository(dbContext, logger);
});

// Factory with configuration
builder.Services.AddSingleton<IEmailService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var smtpSettings = config.GetSection("Smtp").Get<SmtpSettings>();
    return new SmtpEmailService(smtpSettings!);
});

// Conditional factory
builder.Services.AddScoped<IPaymentProcessor>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["PaymentProvider"];

    return provider switch
    {
        "Stripe" => new StripePaymentProcessor(
            sp.GetRequiredService<IHttpClientFactory>()),
        "PayPal" => new PayPalPaymentProcessor(
            sp.GetRequiredService<IHttpClientFactory>()),
        _ => throw new InvalidOperationException($"Unknown payment provider: {provider}")
    };
});
```

### Multiple Implementations of the Same Interface

```csharp
public interface INotificationSender
{
    Task SendAsync(string message);
}

public class EmailNotificationSender : INotificationSender
{
    public Task SendAsync(string message) => Task.CompletedTask; // Send email
}

public class SmsNotificationSender : INotificationSender
{
    public Task SendAsync(string message) => Task.CompletedTask; // Send SMS
}

public class PushNotificationSender : INotificationSender
{
    public Task SendAsync(string message) => Task.CompletedTask; // Send push
}

// Register all implementations
builder.Services.AddSingleton<INotificationSender, EmailNotificationSender>();
builder.Services.AddSingleton<INotificationSender, SmsNotificationSender>();
builder.Services.AddSingleton<INotificationSender, PushNotificationSender>();

// Consuming class
public class NotificationService
{
    private readonly IEnumerable<INotificationSender> _senders;
    private readonly INotificationSender _defaultSender;

    public NotificationService(
        IEnumerable<INotificationSender> senders,  // All implementations
        INotificationSender defaultSender)          // Last registered (PushNotificationSender)
    {
        _senders = senders;
        _defaultSender = defaultSender;
    }

    public async Task NotifyAllChannelsAsync(string message)
    {
        // Send to all channels
        foreach (var sender in _senders)
        {
            await sender.SendAsync(message);
        }
    }

    public Task NotifyDefaultAsync(string message)
    {
        // Send only via default (last registered)
        return _defaultSender.SendAsync(message);
    }
}
```

### TryAdd Methods to Avoid Duplicate Registrations

```csharp
// TryAdd - only registers if service type not already registered
builder.Services.AddSingleton<ICache, RedisCache>();
builder.Services.TryAddSingleton<ICache, MemoryCache>(); // Ignored - ICache already registered

// Useful for library authors
public static class MyLibraryExtensions
{
    public static IServiceCollection AddMyLibrary(this IServiceCollection services)
    {
        // Won't override if user already registered their own implementation
        services.TryAddScoped<IMyService, DefaultMyService>();
        services.TryAddTransient<IMyHelper, DefaultMyHelper>();

        return services;
    }
}

// TryAddEnumerable - prevents duplicate implementations (not just service types)
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<INotificationSender, EmailNotificationSender>());
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<INotificationSender, EmailNotificationSender>()); // Ignored - same implementation
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<INotificationSender, SmsNotificationSender>()); // Added - different implementation
```

### Open Generics Registration

```csharp
// Generic interface and implementation
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

public class GenericRepository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _context;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(int id)
        => await _context.Set<T>().FindAsync(id);

    public async Task<IEnumerable<T>> GetAllAsync()
        => await _context.Set<T>().ToListAsync();

    public async Task AddAsync(T entity)
        => await _context.Set<T>().AddAsync(entity);

    public async Task UpdateAsync(T entity)
        => _context.Set<T>().Update(entity);

    public async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
            _context.Set<T>().Remove(entity);
    }
}

// Open generic registration
builder.Services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

// Now any IRepository<T> is automatically resolved
public class ProductService
{
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<Category> _categoryRepository;

    public ProductService(
        IRepository<Product> productRepository,    // Resolves to GenericRepository<Product>
        IRepository<Category> categoryRepository)  // Resolves to GenericRepository<Category>
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }
}

// Override for specific types if needed
builder.Services.AddScoped<IRepository<User>, UserRepository>();
```

---

## 4. Keyed Services (.NET 8+)

Keyed services allow registering multiple implementations with different keys, eliminating the need for factory patterns in many scenarios.

### Basic Keyed Service Registration

```csharp
public interface ICache
{
    object? Get(string key);
    void Set(string key, object value, TimeSpan? expiration = null);
}

public class MemoryCache : ICache
{
    private readonly Dictionary<string, object> _cache = new();

    public object? Get(string key) => _cache.GetValueOrDefault(key);
    public void Set(string key, object value, TimeSpan? expiration = null)
        => _cache[key] = value;
}

public class RedisCache : ICache
{
    public object? Get(string key) => /* Redis implementation */;
    public void Set(string key, object value, TimeSpan? expiration = null)
        => /* Redis implementation */;
}

public class DistributedCache : ICache
{
    public object? Get(string key) => /* Distributed implementation */;
    public void Set(string key, object value, TimeSpan? expiration = null)
        => /* Distributed implementation */;
}

// Register with keys
builder.Services.AddKeyedSingleton<ICache, MemoryCache>("memory");
builder.Services.AddKeyedSingleton<ICache, RedisCache>("redis");
builder.Services.AddKeyedSingleton<ICache, DistributedCache>("distributed");
```

### Using [FromKeyedServices] Attribute

```csharp
// Constructor injection with keyed services
public class CacheService
{
    private readonly ICache _localCache;
    private readonly ICache _distributedCache;

    public CacheService(
        [FromKeyedServices("memory")] ICache localCache,
        [FromKeyedServices("distributed")] ICache distributedCache)
    {
        _localCache = localCache;
        _distributedCache = distributedCache;
    }

    public object? GetWithFallback(string key)
    {
        // Try local cache first, then distributed
        return _localCache.Get(key) ?? _distributedCache.Get(key);
    }
}

// Minimal API endpoint injection
app.MapGet("/cache/{key}", (
    string key,
    [FromKeyedServices("redis")] ICache cache) =>
{
    var value = cache.Get(key);
    return value is not null ? Results.Ok(value) : Results.NotFound();
});

// Controller action injection
[ApiController]
[Route("api/[controller]")]
public class CacheController : ControllerBase
{
    [HttpGet("{key}")]
    public IActionResult Get(
        string key,
        [FromKeyedServices("memory")] ICache cache)
    {
        var value = cache.Get(key);
        return value is not null ? Ok(value) : NotFound();
    }
}
```

### IKeyedServiceProvider

```csharp
public class DynamicCacheService
{
    private readonly IServiceProvider _serviceProvider;

    public DynamicCacheService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ICache GetCache(string cacheType)
    {
        // Resolve keyed service at runtime
        return _serviceProvider.GetRequiredKeyedService<ICache>(cacheType);
    }

    public object? GetFromCache(string cacheType, string key)
    {
        var cache = GetCache(cacheType);
        return cache.Get(key);
    }
}

// Usage
var dynamicService = serviceProvider.GetRequiredService<DynamicCacheService>();
var value = dynamicService.GetFromCache("redis", "user:123");
```

### Use Cases for Keyed Services

```csharp
// 1. Different storage backends
builder.Services.AddKeyedScoped<IFileStorage, LocalFileStorage>("local");
builder.Services.AddKeyedScoped<IFileStorage, S3FileStorage>("s3");
builder.Services.AddKeyedScoped<IFileStorage, AzureBlobStorage>("azure");

// 2. Environment-specific implementations
builder.Services.AddKeyedSingleton<IEmailService, FakeEmailService>("development");
builder.Services.AddKeyedSingleton<IEmailService, SmtpEmailService>("production");

// 3. Feature-specific services
builder.Services.AddKeyedScoped<IPaymentProcessor, StripeProcessor>("stripe");
builder.Services.AddKeyedScoped<IPaymentProcessor, PayPalProcessor>("paypal");
builder.Services.AddKeyedScoped<IPaymentProcessor, SquareProcessor>("square");

// 4. Versioned APIs
builder.Services.AddKeyedScoped<IApiClient, ApiClientV1>("v1");
builder.Services.AddKeyedScoped<IApiClient, ApiClientV2>("v2");
```

---

## 5. Constructor Injection Patterns

### Primary Constructor Injection (.NET 8+)

```csharp
// Modern C# primary constructor syntax
public class OrderService(
    IOrderRepository orderRepository,
    IInventoryService inventoryService,
    IPaymentService paymentService,
    ILogger<OrderService> logger)
{
    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);

        // Check inventory
        if (!await inventoryService.CheckAvailabilityAsync(request.Items))
        {
            throw new InsufficientInventoryException();
        }

        // Process payment
        var paymentResult = await paymentService.ProcessAsync(request.Payment);
        if (!paymentResult.Success)
        {
            throw new PaymentFailedException(paymentResult.ErrorMessage);
        }

        // Create order
        var order = new Order
        {
            CustomerId = request.CustomerId,
            Items = request.Items,
            PaymentId = paymentResult.PaymentId
        };

        await orderRepository.AddAsync(order);

        logger.LogInformation("Order {OrderId} created successfully", order.Id);
        return order;
    }
}
```

### Traditional Constructor Injection

```csharp
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IInventoryService inventoryService,
        IPaymentService paymentService,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Methods use the private readonly fields
}
```

### [FromServices] Attribute in Minimal APIs and Controllers

```csharp
// Minimal API - services injected via [FromServices]
app.MapPost("/orders", async (
    CreateOrderRequest request,
    [FromServices] IOrderService orderService,
    [FromServices] ILogger<Program> logger) =>
{
    logger.LogInformation("Received order request");
    var order = await orderService.CreateOrderAsync(request);
    return Results.Created($"/orders/{order.Id}", order);
});

// Controller - [FromServices] in action parameters
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    // Constructor injection for frequently used services
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ILogger<OrdersController> logger)
    {
        _logger = logger;
    }

    // [FromServices] for action-specific services
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOrderRequest request,
        [FromServices] IOrderService orderService)
    {
        var order = await orderService.CreateOrderAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        int id,
        [FromServices] IOrderRepository repository)
    {
        var order = await repository.GetByIdAsync(id);
        return order is not null ? Ok(order) : NotFound();
    }
}
```

### Optional Dependencies with Default Values

```csharp
public class NotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender? _smsSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEmailSender emailSender,
        ILogger<NotificationService> logger,
        ISmsSender? smsSender = null)  // Optional dependency
    {
        _emailSender = emailSender;
        _logger = logger;
        _smsSender = smsSender;
    }

    public async Task SendNotificationAsync(string userId, string message)
    {
        await _emailSender.SendAsync(userId, message);

        if (_smsSender is not null)
        {
            await _smsSender.SendAsync(userId, message);
        }
        else
        {
            _logger.LogDebug("SMS sender not configured, skipping SMS notification");
        }
    }
}
```

### IServiceProvider Injection (Use Sparingly)

```csharp
// Generally avoid - this is the service locator anti-pattern
// Only use when you truly need dynamic resolution

public class PluginManager
{
    private readonly IServiceProvider _serviceProvider;

    public PluginManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    // Legitimate use case: dynamic plugin loading
    public IPlugin LoadPlugin(string pluginType)
    {
        var type = Type.GetType(pluginType);
        if (type is null || !typeof(IPlugin).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Invalid plugin type: {pluginType}");
        }

        return (IPlugin)ActivatorUtilities.CreateInstance(_serviceProvider, type);
    }
}

// Prefer this approach instead:
public class BetterPluginManager
{
    private readonly IEnumerable<IPlugin> _plugins;

    public BetterPluginManager(IEnumerable<IPlugin> plugins)
    {
        _plugins = plugins;
    }

    public IPlugin? GetPlugin(string name)
    {
        return _plugins.FirstOrDefault(p => p.Name == name);
    }
}
```

---

## 6. IConfiguration Integration

### How Configuration Integrates with DI

```csharp
var builder = WebApplication.CreateBuilder(args);

// IConfiguration is automatically registered and populated from:
// - appsettings.json
// - appsettings.{Environment}.json
// - Environment variables
// - Command line arguments
// - User secrets (Development)

// Access configuration during service registration
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
```

### Injecting IConfiguration Directly

```csharp
public class ConfigurationDemoService
{
    private readonly IConfiguration _configuration;

    public ConfigurationDemoService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetConnectionString()
    {
        // Direct access to configuration values
        return _configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string not configured");
    }

    public int GetMaxRetries()
    {
        // Get with type conversion
        return _configuration.GetValue<int>("AppSettings:MaxRetries", defaultValue: 3);
    }

    public SmtpSettings GetSmtpSettings()
    {
        // Bind to a section
        var settings = new SmtpSettings();
        _configuration.GetSection("Smtp").Bind(settings);
        return settings;
    }
}
```

### Why to Prefer IOptions<T> over Direct IConfiguration Injection

| Aspect | IConfiguration | IOptions<T> |
|--------|---------------|-------------|
| Type Safety | No - string keys | Yes - strongly typed |
| Validation | Manual | Built-in with `ValidateDataAnnotations` |
| Change Tracking | No built-in support | `IOptionsMonitor<T>` supports |
| Testability | Harder to mock | Easy to mock |
| IntelliSense | No | Yes |
| Refactoring | Error-prone | Safe |

```csharp
// Avoid this
public class BadService
{
    private readonly IConfiguration _config;

    public BadService(IConfiguration config)
    {
        _config = config;
    }

    public void DoWork()
    {
        // Magic strings, no compile-time checking
        var maxRetries = _config.GetValue<int>("AppSettings:MaxRetires"); // Typo!
        var timeout = _config.GetValue<int>("AppSettings:Timeout");
    }
}

// Prefer this
public class AppSettings
{
    public int MaxRetries { get; set; }
    public int Timeout { get; set; }
}

public class GoodService
{
    private readonly AppSettings _settings;

    public GoodService(IOptions<AppSettings> options)
    {
        _settings = options.Value;
    }

    public void DoWork()
    {
        // Type-safe, IntelliSense, compile-time checking
        var maxRetries = _settings.MaxRetries;
        var timeout = _settings.Timeout;
    }
}

// Registration
builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("AppSettings"));
```

---

## 7. IOptions Pattern Family

### IOptions<T> - Singleton Snapshot

Best for static configuration that doesn't change at runtime.

```csharp
public class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpServer { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
}

// Registration
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));

// Usage - IOptions<T> reads once at startup
public class EmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value; // Cached, never changes
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        using var client = new SmtpClient(_settings.SmtpServer, _settings.Port);
        client.EnableSsl = _settings.UseSsl;
        client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);

        await client.SendMailAsync(/* ... */);
    }
}
```

### IOptionsSnapshot<T> - Scoped with Reload

Best for web applications that need per-request configuration updates.

```csharp
// Registration (same as IOptions)
builder.Services.Configure<FeatureFlags>(
    builder.Configuration.GetSection("FeatureFlags"));

// Usage - IOptionsSnapshot<T> recomputes per scope (request)
public class FeatureService
{
    private readonly FeatureFlags _features;

    public FeatureService(IOptionsSnapshot<FeatureFlags> optionsSnapshot)
    {
        _features = optionsSnapshot.Value; // Fresh value per request
    }

    public bool IsFeatureEnabled(string featureName)
    {
        return featureName switch
        {
            "DarkMode" => _features.DarkModeEnabled,
            "BetaFeatures" => _features.BetaFeaturesEnabled,
            _ => false
        };
    }
}

// IMPORTANT: Cannot inject IOptionsSnapshot into singleton services!
// This will throw at runtime:
// builder.Services.AddSingleton<MySingletonService>(); // Has IOptionsSnapshot<T> dependency
```

### IOptionsMonitor<T> - Singleton with Change Notifications

Best for singleton services that need to respond to configuration changes.

```csharp
public class CacheSettings
{
    public int DefaultExpirationMinutes { get; set; }
    public int MaxItems { get; set; }
}

// Registration
builder.Services.Configure<CacheSettings>(
    builder.Configuration.GetSection("Cache"));

// Usage - IOptionsMonitor<T> is singleton but provides current values
public class CacheService : IDisposable
{
    private readonly IOptionsMonitor<CacheSettings> _optionsMonitor;
    private readonly IDisposable? _changeListener;
    private CacheSettings _currentSettings;

    public CacheService(IOptionsMonitor<CacheSettings> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
        _currentSettings = optionsMonitor.CurrentValue;

        // Subscribe to configuration changes
        _changeListener = optionsMonitor.OnChange(settings =>
        {
            _currentSettings = settings;
            Console.WriteLine($"Cache settings updated: MaxItems = {settings.MaxItems}");
            // React to changes (e.g., clear cache, resize, etc.)
        });
    }

    public void AddToCache(string key, object value)
    {
        // Always uses current settings
        var expiration = TimeSpan.FromMinutes(_currentSettings.DefaultExpirationMinutes);
        // Add to cache with expiration...
    }

    public void Dispose()
    {
        _changeListener?.Dispose();
    }
}
```

### IOptionsFactory<T> for Custom Creation

```csharp
public class CustomOptionsFactory<TOptions> : IOptionsFactory<TOptions>
    where TOptions : class, new()
{
    private readonly IEnumerable<IConfigureOptions<TOptions>> _setups;
    private readonly IEnumerable<IPostConfigureOptions<TOptions>> _postConfigures;
    private readonly IEnumerable<IValidateOptions<TOptions>> _validations;

    public CustomOptionsFactory(
        IEnumerable<IConfigureOptions<TOptions>> setups,
        IEnumerable<IPostConfigureOptions<TOptions>> postConfigures,
        IEnumerable<IValidateOptions<TOptions>> validations)
    {
        _setups = setups;
        _postConfigures = postConfigures;
        _validations = validations;
    }

    public TOptions Create(string name)
    {
        var options = new TOptions();

        // Apply configurations
        foreach (var setup in _setups)
        {
            if (setup is IConfigureNamedOptions<TOptions> namedSetup)
            {
                namedSetup.Configure(name, options);
            }
            else if (name == Options.DefaultName)
            {
                setup.Configure(options);
            }
        }

        // Apply post-configurations
        foreach (var post in _postConfigures)
        {
            post.PostConfigure(name, options);
        }

        // Validate
        var failures = new List<string>();
        foreach (var validation in _validations)
        {
            var result = validation.Validate(name, options);
            if (result.Failed)
            {
                failures.AddRange(result.Failures);
            }
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(name, typeof(TOptions), failures);
        }

        return options;
    }
}

// Register custom factory
builder.Services.AddSingleton(typeof(IOptionsFactory<>), typeof(CustomOptionsFactory<>));
```

### Options Validation with ValidateDataAnnotations() and ValidateOnStart()

```csharp
using System.ComponentModel.DataAnnotations;

public class DatabaseSettings
{
    public const string SectionName = "Database";

    [Required(ErrorMessage = "Connection string is required")]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "MaxPoolSize must be between 1 and 100")]
    public int MaxPoolSize { get; set; } = 10;

    [Range(1, 300, ErrorMessage = "CommandTimeout must be between 1 and 300 seconds")]
    public int CommandTimeoutSeconds { get; set; } = 30;

    [Required]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$",
        ErrorMessage = "DatabaseName must start with a letter and contain only alphanumeric characters")]
    public string DatabaseName { get; set; } = string.Empty;
}

// Registration with validation
builder.Services
    .AddOptions<DatabaseSettings>()
    .Bind(builder.Configuration.GetSection(DatabaseSettings.SectionName))
    .ValidateDataAnnotations()  // Enables DataAnnotations validation
    .ValidateOnStart();         // Validates immediately at startup

// Alternative: AddOptionsWithValidateOnStart (combines both)
builder.Services
    .AddOptionsWithValidateOnStart<DatabaseSettings>()
    .Bind(builder.Configuration.GetSection(DatabaseSettings.SectionName))
    .ValidateDataAnnotations();
```

### Custom Validation with IValidateOptions<T>

```csharp
public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; }
}

public class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add("SecretKey is required");
        }
        else if (options.SecretKey.Length < 32)
        {
            failures.Add("SecretKey must be at least 32 characters for security");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Issuer is required");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Audience is required");
        }

        if (options.ExpirationMinutes <= 0)
        {
            failures.Add("ExpirationMinutes must be positive");
        }
        else if (options.ExpirationMinutes > 1440) // 24 hours
        {
            failures.Add("ExpirationMinutes should not exceed 1440 (24 hours)");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

// Registration
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

// Or use TryAddEnumerable for library scenarios
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>());
```

---

## 8. Advanced Patterns

### Decorator Pattern with DI

The decorator pattern allows adding behavior to services without modifying them.

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);
    Task<IEnumerable<Order>> GetAllAsync();
    Task AddAsync(Order order);
}

public class SqlOrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;

    public SqlOrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(int id)
        => await _context.Orders.FindAsync(id);

    public async Task<IEnumerable<Order>> GetAllAsync()
        => await _context.Orders.ToListAsync();

    public async Task AddAsync(Order order)
        => await _context.Orders.AddAsync(order);
}

// Caching decorator
public class CachedOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly IMemoryCache _cache;

    public CachedOrderRepository(IOrderRepository inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        var cacheKey = $"order:{id}";

        if (_cache.TryGetValue(cacheKey, out Order? cached))
        {
            return cached;
        }

        var order = await _inner.GetByIdAsync(id);
        if (order is not null)
        {
            _cache.Set(cacheKey, order, TimeSpan.FromMinutes(5));
        }

        return order;
    }

    public Task<IEnumerable<Order>> GetAllAsync() => _inner.GetAllAsync();
    public Task AddAsync(Order order) => _inner.AddAsync(order);
}

// Logging decorator
public class LoggingOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly ILogger<LoggingOrderRepository> _logger;

    public LoggingOrderRepository(IOrderRepository inner, ILogger<LoggingOrderRepository> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Getting order {OrderId}", id);
        var order = await _inner.GetByIdAsync(id);
        _logger.LogInformation("Retrieved order {OrderId}: {Found}", id, order is not null);
        return order;
    }

    public async Task<IEnumerable<Order>> GetAllAsync()
    {
        _logger.LogInformation("Getting all orders");
        return await _inner.GetAllAsync();
    }

    public async Task AddAsync(Order order)
    {
        _logger.LogInformation("Adding order for customer {CustomerId}", order.CustomerId);
        await _inner.AddAsync(order);
    }
}

// Manual registration (without Scrutor)
builder.Services.AddScoped<SqlOrderRepository>();
builder.Services.AddScoped<IOrderRepository>(sp =>
{
    var inner = sp.GetRequiredService<SqlOrderRepository>();
    var cache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<LoggingOrderRepository>>();

    // Chain: Logging -> Caching -> SQL
    var cached = new CachedOrderRepository(inner, cache);
    return new LoggingOrderRepository(cached, logger);
});

// Using Scrutor (recommended for complex scenarios)
// Install-Package Scrutor
builder.Services.AddScoped<IOrderRepository, SqlOrderRepository>();
builder.Services.Decorate<IOrderRepository, CachedOrderRepository>();
builder.Services.Decorate<IOrderRepository, LoggingOrderRepository>();
```

### Factory Pattern with IServiceProvider

```csharp
public interface IReportGenerator
{
    Task<byte[]> GenerateAsync(ReportRequest request);
}

public class PdfReportGenerator : IReportGenerator
{
    public Task<byte[]> GenerateAsync(ReportRequest request) => /* PDF generation */;
}

public class ExcelReportGenerator : IReportGenerator
{
    public Task<byte[]> GenerateAsync(ReportRequest request) => /* Excel generation */;
}

public class CsvReportGenerator : IReportGenerator
{
    public Task<byte[]> GenerateAsync(ReportRequest request) => /* CSV generation */;
}

// Factory interface
public interface IReportGeneratorFactory
{
    IReportGenerator Create(ReportFormat format);
}

// Factory implementation
public class ReportGeneratorFactory : IReportGeneratorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ReportGeneratorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IReportGenerator Create(ReportFormat format)
    {
        return format switch
        {
            ReportFormat.Pdf => _serviceProvider.GetRequiredService<PdfReportGenerator>(),
            ReportFormat.Excel => _serviceProvider.GetRequiredService<ExcelReportGenerator>(),
            ReportFormat.Csv => _serviceProvider.GetRequiredService<CsvReportGenerator>(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }
}

// Registration
builder.Services.AddTransient<PdfReportGenerator>();
builder.Services.AddTransient<ExcelReportGenerator>();
builder.Services.AddTransient<CsvReportGenerator>();
builder.Services.AddSingleton<IReportGeneratorFactory, ReportGeneratorFactory>();

// Usage
public class ReportService
{
    private readonly IReportGeneratorFactory _factory;

    public ReportService(IReportGeneratorFactory factory)
    {
        _factory = factory;
    }

    public async Task<byte[]> GenerateReportAsync(ReportRequest request)
    {
        var generator = _factory.Create(request.Format);
        return await generator.GenerateAsync(request);
    }
}
```

### Lazy Initialization with Lazy<T>

```csharp
public class ExpensiveService
{
    public ExpensiveService()
    {
        // Expensive initialization
        Thread.Sleep(5000); // Simulating expensive setup
    }

    public string DoWork() => "Work done!";
}

// Register Lazy<T>
builder.Services.AddSingleton<ExpensiveService>();
builder.Services.AddSingleton(sp =>
    new Lazy<ExpensiveService>(() => sp.GetRequiredService<ExpensiveService>()));

// Usage - service created only when .Value is accessed
public class MyService
{
    private readonly Lazy<ExpensiveService> _expensiveService;

    public MyService(Lazy<ExpensiveService> expensiveService)
    {
        _expensiveService = expensiveService;
        // ExpensiveService NOT created yet
    }

    public void DoWorkIfNeeded(bool needed)
    {
        if (needed)
        {
            // ExpensiveService created HERE on first access
            var result = _expensiveService.Value.DoWork();
        }
    }
}

// Generic registration for any Lazy<T>
builder.Services.AddTransient(typeof(Lazy<>), typeof(LazyServiceProvider<>));

public class LazyServiceProvider<T> : Lazy<T> where T : class
{
    public LazyServiceProvider(IServiceProvider serviceProvider)
        : base(() => serviceProvider.GetRequiredService<T>())
    {
    }
}
```

### Creating Scopes Manually with IServiceScopeFactory

```csharp
// Essential for background services that need scoped services
public class OrderProcessingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderProcessingBackgroundService> _logger;

    public OrderProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a new scope for each iteration
                using (var scope = _scopeFactory.CreateScope())
                {
                    var orderRepository = scope.ServiceProvider
                        .GetRequiredService<IOrderRepository>();
                    var dbContext = scope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();

                    var pendingOrders = await orderRepository.GetPendingOrdersAsync();

                    foreach (var order in pendingOrders)
                    {
                        await ProcessOrderAsync(order, scope.ServiceProvider);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                // Scope disposed - DbContext and scoped services cleaned up
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing orders");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessOrderAsync(Order order, IServiceProvider scopedProvider)
    {
        var paymentService = scopedProvider.GetRequiredService<IPaymentService>();
        var inventoryService = scopedProvider.GetRequiredService<IInventoryService>();

        // Process order with scoped services...
    }
}

// Async scope creation (.NET 6+)
public class AsyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AsyncBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // CreateAsyncScope for better async disposal
        await using var scope = _scopeFactory.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<IMyService>();
        await service.DoWorkAsync(stoppingToken);
    }
}
```

---

## 9. Common Pitfalls and Anti-Patterns

### Captive Dependency Problem (Scoped in Singleton)

```csharp
// THE PROBLEM
public class SingletonService
{
    private readonly ScopedService _scoped; // CAPTIVE DEPENDENCY!

    public SingletonService(ScopedService scoped)
    {
        _scoped = scoped; // Scoped service now lives as long as singleton
    }
}

builder.Services.AddSingleton<SingletonService>();
builder.Services.AddScoped<ScopedService>();
// Runtime exception: Cannot consume scoped service from singleton

// THE SOLUTION: Use IServiceScopeFactory
public class SingletonService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SingletonService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task DoWorkAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<ScopedService>();
        await scoped.ProcessAsync();
    }
}

// Enable scope validation to catch these errors
builder.Services.BuildServiceProvider(validateScopes: true);
```

### Service Locator Anti-Pattern

```csharp
// ANTI-PATTERN: Service Locator
public class BadService
{
    private readonly IServiceProvider _serviceProvider;

    public BadService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void DoWork()
    {
        // Hidden dependencies - hard to test, unclear requirements
        var logger = _serviceProvider.GetRequiredService<ILogger<BadService>>();
        var repo = _serviceProvider.GetRequiredService<IRepository>();
        var cache = _serviceProvider.GetService<ICache>();

        // Work with resolved services...
    }
}

// CORRECT: Explicit Constructor Injection
public class GoodService
{
    private readonly ILogger<GoodService> _logger;
    private readonly IRepository _repo;
    private readonly ICache? _cache;

    public GoodService(
        ILogger<GoodService> logger,
        IRepository repo,
        ICache? cache = null)  // Optional dependency
    {
        _logger = logger;
        _repo = repo;
        _cache = cache;
    }

    public void DoWork()
    {
        // Dependencies are clear, testable, and explicitly defined
    }
}
```

### Circular Dependencies

```csharp
// THE PROBLEM: Circular dependency
public class ServiceA
{
    public ServiceA(ServiceB serviceB) { } // A depends on B
}

public class ServiceB
{
    public ServiceB(ServiceA serviceA) { } // B depends on A - CIRCULAR!
}

builder.Services.AddScoped<ServiceA>();
builder.Services.AddScoped<ServiceB>();
// Runtime exception: A circular dependency was detected

// SOLUTION 1: Introduce an abstraction
public interface ISharedFunctionality
{
    void SharedMethod();
}

public class SharedService : ISharedFunctionality
{
    public void SharedMethod() { }
}

public class ServiceA
{
    private readonly ISharedFunctionality _shared;
    public ServiceA(ISharedFunctionality shared) => _shared = shared;
}

public class ServiceB
{
    private readonly ISharedFunctionality _shared;
    public ServiceB(ISharedFunctionality shared) => _shared = shared;
}

// SOLUTION 2: Lazy resolution (use sparingly)
public class ServiceA
{
    private readonly Lazy<ServiceB> _serviceB;

    public ServiceA(Lazy<ServiceB> serviceB)
    {
        _serviceB = serviceB;
        // Don't access _serviceB.Value in constructor!
    }

    public void UseServiceB()
    {
        _serviceB.Value.DoSomething(); // Safe to access here
    }
}

// SOLUTION 3: Method injection instead of constructor injection
public class ServiceA
{
    public void DoWork(ServiceB serviceB)
    {
        serviceB.DoSomething();
    }
}
```

### Disposable Services and Proper Cleanup

```csharp
// PROBLEM: Transient disposables captured by container
builder.Services.AddTransient<DisposableService>(); // Memory leak potential!

// Container tracks all IDisposable instances
// They won't be disposed until container is disposed

// SOLUTION 1: Use scoped instead
builder.Services.AddScoped<DisposableService>();

// SOLUTION 2: Use factory pattern for manual control
builder.Services.AddSingleton<IDisposableServiceFactory, DisposableServiceFactory>();

public interface IDisposableServiceFactory
{
    DisposableService Create();
}

public class DisposableServiceFactory : IDisposableServiceFactory
{
    private readonly IServiceProvider _provider;

    public DisposableServiceFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public DisposableService Create()
    {
        return ActivatorUtilities.CreateInstance<DisposableService>(_provider);
    }
}

// Usage - caller controls lifetime
public class Consumer
{
    private readonly IDisposableServiceFactory _factory;

    public Consumer(IDisposableServiceFactory factory)
    {
        _factory = factory;
    }

    public void DoWork()
    {
        using var service = _factory.Create();
        service.Process();
    } // Properly disposed here
}

// IMPORTANT: Never dispose injected services
public class BadConsumer
{
    private readonly IDisposableService _service;

    public BadConsumer(IDisposableService service)
    {
        _service = service;
    }

    public void DoWork()
    {
        _service.Process();
        _service.Dispose(); // DON'T DO THIS - container manages lifetime!
    }
}
```

---

## 10. Testing with DI

### Mocking Services in Unit Tests

```csharp
using Moq;
using Xunit;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _repositoryMock;
    private readonly Mock<IPaymentService> _paymentMock;
    private readonly Mock<ILogger<OrderService>> _loggerMock;
    private readonly OrderService _sut; // System Under Test

    public OrderServiceTests()
    {
        _repositoryMock = new Mock<IOrderRepository>();
        _paymentMock = new Mock<IPaymentService>();
        _loggerMock = new Mock<ILogger<OrderService>>();

        _sut = new OrderService(
            _repositoryMock.Object,
            _paymentMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenPaymentSucceeds_ShouldSaveOrder()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = 1,
            Items = new List<OrderItem> { new() { ProductId = 1, Quantity = 2 } }
        };

        _paymentMock
            .Setup(p => p.ProcessAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResult { Success = true, PaymentId = "pay_123" });

        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateOrderAsync(request);

        // Assert
        Assert.NotNull(result);
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenPaymentFails_ShouldThrowException()
    {
        // Arrange
        var request = new CreateOrderRequest { CustomerId = 1 };

        _paymentMock
            .Setup(p => p.ProcessAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResult { Success = false, ErrorMessage = "Card declined" });

        // Act & Assert
        await Assert.ThrowsAsync<PaymentFailedException>(
            () => _sut.CreateOrderAsync(request));

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Never);
    }
}
```

### Integration Testing with WebApplicationFactory

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class OrdersControllerIntegrationTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OrdersControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrders_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/orders");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreateOrder_WithValidData_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var order = new { CustomerId = 1, Items = new[] { new { ProductId = 1, Quantity = 2 } } };

        // Act
        var response = await client.PostAsJsonAsync("/api/orders", order);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

### Replacing Services for Testing

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database context
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
            });

            // Replace external services with fakes
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, FakeEmailService>();

            services.RemoveAll<IPaymentService>();
            services.AddSingleton<IPaymentService, FakePaymentService>();
        });

        builder.UseEnvironment("Testing");
    }
}

// Usage in tests
public class OrdersIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrdersIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_ShouldSendEmail()
    {
        // Arrange
        var order = new CreateOrderRequest { CustomerId = 1 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", order);

        // Assert
        response.EnsureSuccessStatusCode();

        // Verify fake service was called
        using var scope = _factory.Services.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>() as FakeEmailService;
        Assert.Single(emailService!.SentEmails);
    }
}

// Fake implementations for testing
public class FakeEmailService : IEmailService
{
    public List<SentEmail> SentEmails { get; } = new();

    public Task SendAsync(string to, string subject, string body)
    {
        SentEmails.Add(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }
}

public record SentEmail(string To, string Subject, string Body);
```

---

## 11. Third-Party DI Containers

### When to Consider Autofac, Ninject, etc.

The built-in .NET DI container handles most scenarios. Consider third-party containers when you need:

- **Property injection** (not supported by default container)
- **Interception/AOP** (cross-cutting concerns via proxies)
- **Advanced lifetime management**
- **Module-based registration**
- **Dynamic assembly scanning**
- **Convention-based registration**

### How to Replace the Default Container with Autofac

```csharp
// Install-Package Autofac.Extensions.DependencyInjection

using Autofac;
using Autofac.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Replace the default service provider with Autofac
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

// Configure Autofac container
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    // Register services using Autofac syntax
    containerBuilder.RegisterType<OrderService>()
        .As<IOrderService>()
        .InstancePerLifetimeScope();

    // Property injection (not available in default container)
    containerBuilder.RegisterType<PropertyInjectedService>()
        .As<IPropertyInjectedService>()
        .PropertiesAutowired();

    // Assembly scanning
    containerBuilder.RegisterAssemblyTypes(typeof(Program).Assembly)
        .Where(t => t.Name.EndsWith("Repository"))
        .AsImplementedInterfaces()
        .InstancePerLifetimeScope();

    // Module-based registration
    containerBuilder.RegisterModule<DataAccessModule>();
    containerBuilder.RegisterModule<ServicesModule>();

    // Decorator support
    containerBuilder.RegisterDecorator<CachedOrderRepository, IOrderRepository>();
    containerBuilder.RegisterDecorator<LoggingOrderRepository, IOrderRepository>();
});

var app = builder.Build();
app.Run();

// Autofac modules for organized registration
public class DataAccessModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ApplicationDbContext>()
            .AsSelf()
            .InstancePerLifetimeScope();

        builder.RegisterGeneric(typeof(GenericRepository<>))
            .As(typeof(IRepository<>))
            .InstancePerLifetimeScope();
    }
}

public class ServicesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterAssemblyTypes(ThisAssembly)
            .Where(t => t.Name.EndsWith("Service"))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();
    }
}
```

### Interception with Autofac (AOP)

```csharp
// Install-Package Autofac.Extras.DynamicProxy
// Install-Package Castle.Core

using Autofac.Extras.DynamicProxy;
using Castle.DynamicProxy;

// Interceptor for logging
public class LoggingInterceptor : IInterceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public void Intercept(IInvocation invocation)
    {
        _logger.LogInformation(
            "Calling {Method} with arguments {Arguments}",
            invocation.Method.Name,
            invocation.Arguments);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            invocation.Proceed();
            stopwatch.Stop();

            _logger.LogInformation(
                "Method {Method} completed in {ElapsedMs}ms",
                invocation.Method.Name,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Method {Method} threw exception",
                invocation.Method.Name);
            throw;
        }
    }
}

// Register with interception
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterType<LoggingInterceptor>();

    containerBuilder.RegisterType<OrderService>()
        .As<IOrderService>()
        .EnableInterfaceInterceptors()
        .InterceptedBy(typeof(LoggingInterceptor));
});
```

---

## Summary

### Quick Reference Table

| Pattern | When to Use | Example |
|---------|-------------|---------|
| Constructor Injection | Default approach | `public MyService(IDep dep)` |
| [FromKeyedServices] | Multiple implementations | `[FromKeyedServices("key")] IDep` |
| IOptions<T> | Static config | `IOptions<Settings>` |
| IOptionsSnapshot<T> | Per-request config | `IOptionsSnapshot<Settings>` |
| IOptionsMonitor<T> | Live config updates | `IOptionsMonitor<Settings>` |
| Factory Pattern | Dynamic creation | `IServiceFactory.Create()` |
| Decorator Pattern | Add behavior | Logging, caching wrappers |
| Open Generics | Generic services | `IRepository<T>` |

### Best Practices Checklist

1. Use constructor injection as the default approach
2. Choose appropriate service lifetimes (singleton vs scoped vs transient)
3. Prefer IOptions<T> over direct IConfiguration injection
4. Use extension methods to organize service registration
5. Enable scope validation in development
6. Avoid the service locator anti-pattern
7. Watch for captive dependencies
8. Use IServiceScopeFactory in singleton services that need scoped dependencies
9. Don't dispose injected services manually
10. Write unit tests with mocked dependencies
11. Use WebApplicationFactory for integration tests

---

## References

- [Dependency injection - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Dependency injection in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)
- [Dependency injection guidelines - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
- [Options pattern - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [.NET Generic Host | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Keyed service dependency injection - Andrew Lock](https://andrewlock.net/exploring-the-dotnet-8-preview-keyed-services-dependency-injection-support/)
- [Decorator pattern in ASP.NET Core - Milan Jovanovic](https://www.milanjovanovic.tech/blog/decorator-pattern-in-asp-net-core)
- [Third-Party DI Container and Autofac - Code Maze](https://code-maze.com/using-autofac-dotnet/)
- [Integration tests in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
