# PII Obfuscation Portal - Test Suite

This directory contains the comprehensive test suite for the PII Obfuscation Portal using Playwright.

## Setup

1. **Install Dependencies**
   ```bash
   cd portal/tests
   npm install
   npx playwright install
   ```

2. **Start Development Servers**
   ```bash
   # Terminal 1 - API
   cd portal/api
   dotnet run
   
   # Terminal 2 - Web
   cd portal/web  
   dotnet run
   ```

3. **Run Tests**
   ```bash
   cd portal/tests
   npm test
   ```

## Test Structure

```
tests/
├── api/                    # API endpoint tests
│   ├── connection-strings.spec.ts
│   ├── pipeline-stages.spec.ts
│   ├── scripts.spec.ts
│   └── pipeline-execution.spec.ts
├── ui/                     # User interface tests
│   ├── product-dashboard.spec.ts
│   ├── pipeline-config.spec.ts
│   ├── monitoring-dashboard.spec.ts
│   └── connection-management.spec.ts
├── database/               # Database schema tests
│   └── schema-validation.spec.ts
├── security/              # Security validation tests
│   ├── script-validation.spec.ts
│   └── comprehensive-security.spec.ts
├── performance/           # Performance tests
│   └── load-testing.spec.ts
├── integration/           # End-to-end tests
│   └── end-to-end.spec.ts
├── acceptance/            # User acceptance tests
│   └── user-workflows.spec.ts
└── realtime/             # SignalR tests
    └── signalr.spec.ts
```

## Test Categories

### API Tests (`api/`)
- Connection string management with encryption/masking
- Pipeline stage configuration
- Script management and validation
- Pipeline execution control

### UI Tests (`ui/`)
- Product dashboard functionality
- Pipeline configuration interface
- Real-time monitoring dashboard
- Connection management interface

### Database Tests (`database/`)
- Schema validation after migrations
- Index and constraint verification
- Default data validation

### Security Tests (`security/`)
- SQL injection prevention
- Script validation rules
- Connection string security
- Authentication/authorization

### Performance Tests (`performance/`)
- API endpoint performance
- Database query optimization
- UI responsiveness
- SignalR performance under load

### Integration Tests (`integration/`)
- Complete pipeline execution flows
- Cross-database validation
- Error handling and recovery
- Real-time monitoring accuracy

### Real-time Tests (`realtime/`)
- SignalR connection handling
- Real-time progress updates
- Live log streaming
- Connection management

## Test Data

Test data is managed through:
- `fixtures/test-data.json` - Static test data
- `utils/test-helpers.ts` - Helper functions and constants
- `utils/database-setup.ts` - Database setup/teardown utilities

## Running Tests

```bash
# Run all tests
npm test

# Run specific test category
npm test -- tests/api/
npm test -- tests/ui/

# Run tests in headed mode (see browser)
npm run test:headed

# Run tests in debug mode
npm run test:debug

# Run tests with UI
npm run test:ui

# View test report
npm run test:report
```

## Test Configuration

The test configuration is managed in `playwright.config.ts`:
- Browser configurations (Chrome, Firefox, Safari, Mobile)
- API testing configuration
- Development server startup
- Test timeouts and retries
- Screenshot and video capture

## Test Helpers

### ApiTestHelpers
- Product management (create/read/delete)
- Connection string validation
- Pipeline execution monitoring
- Authentication helpers

### DatabaseTestHelpers  
- Schema validation
- Test data cleanup
- Database state verification

### TestConstants
- Common test data
- API endpoints
- Timeout values
- Validation patterns

## CI/CD Integration

Tests are designed to run in CI/CD pipelines with:
- Parallel execution support
- Retry logic for flaky tests
- Screenshot/video capture on failure
- HTML test reports
- Database cleanup between runs

## Best Practices

1. **Test Isolation**: Each test creates its own test data and cleans up afterward
2. **API First**: Test API functionality before UI to catch issues early
3. **Security Focus**: Every feature includes security validation tests
4. **Performance Awareness**: Include performance assertions in relevant tests
5. **Real-world Scenarios**: Tests reflect actual user workflows
6. **Comprehensive Coverage**: Test both happy path and error conditions

## Troubleshooting

### Common Issues

1. **Server Not Starting**: Ensure API and Web servers are running on correct ports
2. **Database Connection**: Verify database connection string in appsettings.json
3. **Test Flakiness**: Use appropriate waits and retries for async operations
4. **Browser Issues**: Update browsers with `npx playwright install`

### Debug Mode

Run tests in debug mode to step through test execution:
```bash
npm run test:debug -- tests/api/connection-strings.spec.ts
```

### Test Reports

View detailed test reports with screenshots and videos:
```bash
npm run test:report
```

## Contributing

When adding new features:

1. **Add API Tests First**: Test the backend functionality
2. **Add UI Tests**: Test the user interface
3. **Add Security Tests**: Validate security aspects
4. **Update Test Data**: Add necessary test fixtures
5. **Update Documentation**: Update this README if needed

All tests must pass before merging code changes.