# E2E Testing Troubleshooting Guide

## Issue: Tests Hang Indefinitely

### Problem Description
When running E2E tests, they hang indefinitely without completing. This is a common issue with TestServer-based tests that can be caused by several factors:

### Potential Causes

1. **Application Startup Dependencies**
   - External service connections (databases, APIs, message queues)
   - OpenTelemetry or monitoring configurations
   - Complex initialization that requires external resources

2. **Configuration Issues**
   - Missing required configuration values
   - Invalid connection strings
   - Environment-specific settings not available in test mode

3. **Port Binding Issues**
   - Application trying to bind to specific ports
   - Conflicts with running services

4. **Async Deadlocks**
   - Synchronous calls to async methods
   - Thread pool starvation

### Solutions Implemented

#### 1. TestWebApplicationFactory Configuration
```csharp
// Clear existing configuration sources to avoid conflicts
config.Sources.Clear();

// Add minimal test configuration
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["OpenTelemetry:Enabled"] = "false",
    ["HttpLogging:Enabled"] = "false"
});
```

#### 2. Service Overrides
```csharp
// Remove potentially problematic services
var telemetryDescriptor = services.FirstOrDefault(d => d.ServiceType.Name.Contains("OpenTelemetry"));
if (telemetryDescriptor != null)
{
    services.Remove(telemetryDescriptor);
}
```

#### 3. Timeout Handling
```csharp
[CancelAfter(15000)] // 15 second timeout
public async Task Server_ShouldStart_WithoutHanging()
```

#### 4. Diagnostic Logging
```csharp
Console.WriteLine("Creating test web application factory...");
// Add logging to track where the hang occurs
```

### Alternative Approaches

If the issues persist, consider these alternatives:

#### Option 1: Integration Tests with Real Server
```csharp
// Start the actual application in a separate process
// Test against HTTP endpoints
```

#### Option 2: Component-Level Testing
```csharp
// Test individual controllers and services in isolation
// Mock external dependencies
```

#### Option 3: Minimal TestServer Setup
```csharp
// Create a minimal test server with only essential services
// Gradually add components to identify the problematic dependency
```

### Current Status

- ✅ Project builds successfully
- ❌ Tests hang during execution
- ⚠️ Need to identify the specific dependency causing the hang

### Next Steps

1. **Identify Startup Dependency**: Check what external services the application tries to connect to during startup
2. **Mock External Services**: Replace external dependencies with mocks in test environment
3. **Simplify Startup**: Create a test-specific startup configuration that excludes problematic components
4. **Use Process Isolation**: Consider running tests in separate processes to avoid conflicts

### Diagnostic Commands

```bash
# Build the test project
dotnet build "C:/Git/Private/FillInTheTextBot/src/FillInTheTextBot.Api.E2ETests/FillInTheTextBot.Api.E2ETests.csproj"

# Run with timeout (manually kill after 30 seconds if hanging)
timeout 30 dotnet test "C:/Git/Private/FillInTheTextBot/src/FillInTheTextBot.Api.E2ETests/FillInTheTextBot.Api.E2ETests.csproj"

# Check for specific test categories
dotnet test --filter "Category=Health"
```

### Recommended Solution

For now, it's recommended to:

1. **Use Unit Tests** for controller logic with mocked dependencies
2. **Use Integration Tests** for specific endpoints with minimal setup
3. **Implement E2E tests gradually** by starting with the simplest possible configuration

The E2E testing infrastructure is in place and ready to use once the startup hanging issue is resolved.