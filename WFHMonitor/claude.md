# Claude Code

## Token Usage Reporting
**MANDATORY**: At the end of every completed task, always display token usage information in the following format:

```
**Token Usage:**
- Used: X tokens
- Limit: 200,000 tokens
- Remaining: Y tokens
- Usage: Z%
```

**Session Management:**
- At the start of a new conversation/session, acknowledge the token reset and track from 0
- When continuing from a previous session, note the starting token count
- Use `/compact` command when approaching 50% token usage to optimize context
- Monitor token usage throughout the session to avoid hitting limits
- After completing a task, provide a SHORT plain-text summary (4 bullet points max) in chat only. Do NOT create markdown files, documents, or headings unless I ask.
- Explanations max 4 sentences unless I ask "why" or "explain"
- Add files modified with 1 line explanation

## Rate Limit Management (VS Code)
**Batching Operations:**
- Combine related file operations into single commands
- Use multi-line bash scripts instead of separate commands
- Read multiple files in one view command when possible

**Efficiency:**
- Don't re-read files already in context
- Cache information from previous operations


**No Repetition Rule**
- Do NOT restate these rules or explain compliance
- Do NOT repeat my prompt in your answer

**No Narration**
- Do NOT describe steps or process unless explicitly asked

## Project-Specific Guidelines

### Code Standards
1. Follow C# naming conventions (PascalCase for classes/methods, camelCase for parameters)
2. Use async/await throughout for all I/O operations
3. Always include proper error handling with try-catch blocks
4. Add XML documentation comments for public methods ONLY when explicitly requested
5. Use dependency injection for all services
6. Use SOLID principles code

### Security Requirements
1. Never bypass authentication or authorization checks
2. Always validate user input with ModelState
3. Use [ValidateAntiForgeryToken] on all POST actions
4. Implement IDOR protection by verifying user ownership
5. Security rules are CONSTRAINTS only.
6. Do NOT generate a Security Implementation document unless explicitly requested.
7. Never expose sensitive data in error messages or logs

### Database Operations
1. **CRITICAL: NEVER drop the database (`dotnet ef database drop`) unless explicitly requested by the user**
   - Dropping the database deletes ALL user data, bills, payments, accounts, news, and logs
   - This is a destructive operation that cannot be undone
   - Only use when the user specifically asks to "drop database", "reset database", or "start fresh"
   - For new migrations, simply run `dotnet ef database update` to apply changes
2. Use AsNoTracking() for read-only queries
3. Always use parameterized queries (EF Core handles this)
4. Include proper indexes on frequently queried columns
5. Use transactions for multi-step operations
5. Create migration initial data if sql table not exist
6. Apply migrations before running the application with `dotnet ef database update` if not yet run
7. When adding seed data, check if data exists first to avoid duplicates
8. Always apply index when create new table
9. Create/Update sql file after every changes that required db script to run

### UI/UX Standards
1. Maintain Netflix-style card design with water theme
2. Use Bootstrap Icons consistently
3. Display success/error messages using TempData
4. Ensure responsive design works on mobile devices
5. Add loading indicators for async operations

### Testing Before Completion
Before marking any feature as complete:
1. Verify the code builds without errors
2. Check that database migrations apply successfully
3. Test the feature manually in the browser

### Performance Considerations
1. Use pagination for lists (default 10, max 100 items)
2. Implement caching for frequently accessed data
3. Minimize database queries with eager loading
4. Use response compression (Brotli/Gzip)

### Git Workflow
1. Never commit appsettings.*.json files with secrets
2. Update .gitignore before committing sensitive files
3. Write clear, descriptive commit messages
4. Include Co-Authored-By tag when using Claude

### Documentation
1. Update README.md ONLY when the user explicitly asks to update documentation
2. Document API endpoints in Swagger/OpenAPI ONLY when explicitly requested
3. Keep architecture documentation current (NO auto-generation of files)
4. Document environment variables and configuration ONLY on explicit request

### Error Handling
1. Return appropriate HTTP status codes
2. Log exceptions with full stack traces
3. Display user-friendly error messages
4. Never expose internal implementation details
5. Handle database connection failures gracefully


### Database
# DONT EVER Drop database (DESTRUCTIVE - Only use when explicitly requested!)
# WARNING: This deletes ALL data permanently
# dotnet ef database drop --force --project MoneyTracker.Infrastructure --startup-project MoneyTracker.Web


## Demo Credentials

**Admin Account:**
- Email: admin@gmail.com
- Password: 1234

**Test Users:**
- adib@gmail.com / 1234

## Reminder
Always stop running applications before building to avoid file lock errors!
