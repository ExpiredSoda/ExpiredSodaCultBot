# Database Migration Instructions

## For Railway (Production)

Railway will automatically create the database tables when the bot first starts using `EnsureCreatedAsync()`.

**No manual migration needed for Railway deployment.**

## For Local Development (Optional)

If you want to use EF Core migrations instead of `EnsureCreatedAsync()`:

### 1. Install EF Core Tools

```bash
dotnet tool install --global dotnet-ef
```

### 2. Create Initial Migration

```bash
cd CultBot
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
```

### 3. Apply Migration to Database

```bash
# Make sure DATABASE_URL environment variable is set
dotnet ef database update
```

### 4. Update Program.cs (Optional)

If you want to use migrations instead of `EnsureCreatedAsync()`, replace in `BotService.StartAsync`:

```csharp
// OLD:
await context.Database.EnsureCreatedAsync(cancellationToken);

// NEW:
await context.Database.MigrateAsync(cancellationToken);
```

And add this using:
```csharp
using Microsoft.EntityFrameworkCore;
```

## Common Commands

```bash
# Add a new migration after model changes
dotnet ef migrations add MigrationName

# Apply all pending migrations
dotnet ef database update

# Rollback to a specific migration
dotnet ef database update PreviousMigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove

# Generate SQL script for migrations
dotnet ef migrations script
```

## Railway Database Access

To connect to your Railway PostgreSQL database directly:

1. Go to Railway project → PostgreSQL service
2. Click "Connect" tab
3. Use provided credentials with a PostgreSQL client (pgAdmin, DBeaver, etc.)

Or use Railway CLI:
```bash
railway connect
```
