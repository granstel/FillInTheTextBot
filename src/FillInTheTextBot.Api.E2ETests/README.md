# FillInTheTextBot API End-to-End Tests

This project contains comprehensive end-to-end tests for the FillInTheTextBot API using TestServer and NUnit.

## Overview

The E2E tests use `Microsoft.AspNetCore.Mvc.Testing` package to create a TestServer that hosts the entire application in-memory. This allows testing the complete request/response pipeline including:

- Controllers and routing
- Middleware
- Dependency injection
- Configuration
- Authentication/authorization
- Error handling

## Test Structure

```
FillInTheTextBot.Api.E2ETests/
├── Controllers/            # Tests for individual controllers
│   ├── YandexControllerE2ETests.cs
│   ├── SberControllerE2ETests.cs
│   └── MarusiaControllerE2ETests.cs
├── Integration/            # Full application integration tests
│   └── ApplicationE2ETests.cs
├── Infrastructure/         # Test infrastructure and base classes
│   ├── BaseE2ETest.cs
│   └── TestWebApplicationFactory.cs
├── Middleware/             # Middleware-specific tests
│   └── MiddlewareE2ETests.cs
├── Performance/            # Performance and load tests
│   └── PerformanceE2ETests.cs
└── Security/               # Security and authentication tests
    └── AuthenticationE2ETests.cs
```

## Running the Tests

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or JetBrains Rider

### Command Line

```bash
# Run all tests
dotnet test

# Run only E2E tests
dotnet test --filter "Category=E2E"

# Run specific test class
dotnet test --filter "FullyQualifiedName~YandexControllerE2ETests"

# Run with verbose output
dotnet test --filter "Category=E2E" --verbosity normal
```

### Visual Studio / Rider

1. Open the solution in your IDE
2. Build the solution (Ctrl+Shift+B)
3. Open Test Explorer
4. Run tests by category "E2E" or individual test classes

## Test Categories

- **E2E**: All end-to-end tests that use TestServer
- You can also run unit tests separately by excluding E2E category: `--filter "Category!=E2E"`

## Configuration

### Test Configuration

Tests use `appsettings.Test.json` for configuration overrides:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft": "Warning"
    }
  }
}
```

### Custom Test Setup

The `TestWebApplicationFactory` can be customized to:

- Override services with mocks
- Configure test-specific settings
- Set up test databases
- Configure external service mocks

## Test Scenarios Covered

### Controller Tests
- HTTP GET requests to info endpoints
- HTTP POST requests with valid/invalid payloads
- HTTP PUT requests for webhook creation
- HTTP DELETE requests for webhook deletion
- Token-based authentication scenarios

### Integration Tests
- Application startup and health checks
- All controllers accessibility
- Error handling for invalid routes
- HTTP method validation
- Content-Type headers validation

### Middleware Tests
- Exception handling middleware
- Security headers validation
- Large payload handling
- Concurrent request processing

### Performance Tests
- Response time validation
- Concurrent request handling
- Resource-intensive operation handling
- Load testing scenarios

### Security Tests
- Token validation scenarios
- Protected endpoint access
- Internal endpoint exposure checks

## Continuous Integration

The tests are integrated into the GitHub Actions workflow:

```yaml
- name: Run E2E Tests
  run: dotnet test FillInTheTextBot.slnx --no-build --verbosity normal --filter "Category=E2E"
```

## Best Practices

1. **Test Isolation**: Each test is independent and doesn't rely on other tests
2. **Realistic Data**: Tests use realistic request/response models
3. **Error Scenarios**: Both success and failure paths are tested
4. **Performance**: Tests include performance validation
5. **Security**: Security scenarios are validated
6. **Maintainability**: Tests are organized by functionality and use shared base classes

## Troubleshooting

### Common Issues

1. **Port Conflicts**: TestServer automatically assigns available ports
2. **Configuration Issues**: Check `appsettings.Test.json` for test-specific settings
3. **Dependency Issues**: Ensure all required NuGet packages are installed
4. **Performance Tests Failing**: Adjust timeout values based on your environment

### Debugging Tests

1. Set breakpoints in test methods
2. Use debug mode in your IDE
3. Add logging to tests if needed
4. Check TestServer logs for application errors

## Contributing

When adding new E2E tests:

1. Inherit from `BaseE2ETest`
2. Add `[Category("E2E")]` attribute to test classes
3. Follow the existing naming conventions
4. Include both positive and negative test scenarios
5. Add performance considerations for critical paths