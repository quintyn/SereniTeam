# SereniTeam Developer Documentation

## Table of Contents
1. [Project Overview](#project-overview)
2. [Architecture & Technology Stack](#architecture--technology-stack)
3. [Getting Started](#getting-started)
4. [Project Structure](#project-structure)
5. [Development Workflow](#development-workflow)
6. [Key Features & Implementation](#key-features--implementation)
7. [API Documentation](#api-documentation)
8. [Database Schema](#database-schema)
9. [Real-time Features](#real-time-features)
10. [Testing](#testing)
11. [Deployment](#deployment)
12. [Contributing Guidelines](#contributing-guidelines)

## Project Overview

SereniTeam is a **real-time wellness dashboard** for remote teams that enables anonymous employee check-ins, burnout detection, and data visualization. The system preserves individual privacy while providing actionable insights to team leaders about their team's morale and stress levels.

### Core Features
- **Anonymous Check-ins**: Team members submit mood and stress ratings without personal identification
- **Real-time Dashboard**: Live team wellness metrics with interactive visualizations
- **Burnout Detection**: Automated alerts based on configurable thresholds and trend analysis
- **Team Management**: Multi-team organization support with role-based access
- **Historical Analytics**: Trend analysis and historical data visualization

## Architecture & Technology Stack

### Overall Architecture
SereniTeam uses a **client-server Single Page Application (SPA)** architecture:

```
┌─────────────────┐    HTTP/SignalR    ┌─────────────────┐    EF Core    ┌─────────────────┐
│  Blazor WASM    │ ←────────────────→ │  ASP.NET Core   │ ←───────────→ │   PostgreSQL    │
│   (Frontend)    │                    │   Web API       │               │   (Database)    │
└─────────────────┘                    └─────────────────┘               └─────────────────┘
```

### Technology Stack

#### **Frontend: Blazor WebAssembly**
- **What it does**: Compiles C# code to WebAssembly that runs directly in the browser
- **Benefits**: 
  - Write entire frontend in C# instead of JavaScript
  - Share models and logic with backend
  - Type-safe client-server communication
- **In SereniTeam**: Renders check-in forms, dashboards, charts, and handles real-time updates

#### **Backend: ASP.NET Core Web API**
- **What it does**: RESTful HTTP API handling business logic and data operations
- **Architecture**: Controllers → Services → Data Layer
- **In SereniTeam**: Validates check-ins, calculates team metrics, manages burnout detection

#### **Database: PostgreSQL + Entity Framework Core**
- **Entity Framework Core**: Object-Relational Mapper (ORM) translating C# objects to SQL
- **PostgreSQL**: Production-ready relational database
- **Benefits**: Code-first development, automatic migrations, LINQ queries
- **In SereniTeam**: Stores anonymous wellness data, team information, historical trends

#### **Real-time: SignalR**
- **What it does**: Server-to-client push notifications over WebSockets
- **Benefits**: Instant updates without page refreshes
- **In SereniTeam**: Live dashboard updates, real-time burnout alerts

### Data Flow Example
1. **Check-in Submission**: Blazor frontend → HTTP POST → API Controller
2. **Server Processing**: Controller → Service → Database (via EF Core)
3. **Real-time Broadcast**: Service → SignalR Hub → Connected Dashboards
4. **Dashboard Update**: SignalR message → Blazor component updates

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 13+](https://www.postgresql.org/download/)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- [Git](https://git-scm.com/)

### Local Development Setup

1. **Clone the Repository**
   ```bash
   git clone https://github.com/yourusername/SereniTeam.git
   cd SereniTeam
   ```

2. **Setup PostgreSQL Database**
   ```bash
   # Option A: Local PostgreSQL
   createdb sereniteam_dev
   
   # Option B: Docker
   docker run --name sereniteam-postgres \
     -e POSTGRES_PASSWORD=dev_password \
     -e POSTGRES_DB=sereniteam_dev \
     -p 5432:5432 -d postgres:15
   ```

3. **Configure Database Connection**
   
   Update `src/SereniTeam.Server/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=sereniteam_dev;Username=postgres;Password=dev_password"
     }
   }
   ```

4. **Install Dependencies**
   ```bash
   dotnet restore
   ```

5. **Apply Database Migrations**
   ```bash
   dotnet ef database update --project src/SereniTeam.Server
   ```

6. **Run the Application**
   ```bash
   # Set development environment
   export ASPNETCORE_ENVIRONMENT=Development  # macOS/Linux
   set ASPNETCORE_ENVIRONMENT=Development     # Windows
   
   # Start the server (includes Blazor client)
   dotnet run --project src/SereniTeam.Server
   ```

7. **Access the Application**
   - Main App: `https://localhost:5001`
   - API Documentation: `https://localhost:5001/swagger`

## Project Structure

```
SereniTeam/
├── src/
│   ├── SereniTeam.Client/           # Blazor WebAssembly frontend
│   │   ├── Pages/                   # Razor pages/components
│   │   ├── Services/                # Client-side API services
│   │   ├── Shared/                  # Shared Blazor components
│   │   └── wwwroot/                 # Static assets
│   ├── SereniTeam.Server/           # ASP.NET Core Web API
│   │   ├── Controllers/             # API controllers
│   │   ├── Services/                # Business logic services
│   │   ├── Data/                    # EF Core context
│   │   ├── Hubs/                    # SignalR hubs
│   │   └── Migrations/              # Database migrations
│   └── SereniTeam.Shared/           # Shared models/DTOs
│       ├── Models/                  # Entity models
│       └── DTOs/                    # Data transfer objects
├── tests/
│   └── SereniTeam.Tests/            # Unit and integration tests
├── docs/                            # Additional documentation
└── README.md
```

## Development Workflow

### Making Changes

1. **Create Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Development Process**
   - Make changes in appropriate layer (Client, Server, Shared)
   - Add/update tests for new functionality
   - Test locally with `dotnet run`
   - Update documentation if needed

3. **Database Changes**
   ```bash
   # Add new migration after model changes
   dotnet ef migrations add YourMigrationName --project src/SereniTeam.Server
   
   # Apply migration
   dotnet ef database update --project src/SereniTeam.Server
   ```

4. **Testing**
   ```bash
   # Run all tests
   dotnet test
   
   # Run specific test project
   dotnet test tests/SereniTeam.Tests/
   ```

### Code Style Guidelines

- **Naming**: PascalCase for classes/methods, camelCase for variables
- **Async**: Use async/await for all I/O operations
- **Documentation**: XML comments for public APIs
- **Validation**: Server-side validation for all inputs
- **Error Handling**: Proper exception handling with logging

## Key Features & Implementation

### Anonymous Check-ins

**How it works:**
- Users select team and submit mood (1-10) and stress (1-10) ratings
- Optional notes field (500 char limit)
- No personal identifiers stored in database

**Key Files:**
- `CheckIn.razor` - Submission form
- `CheckInsController.cs` - API endpoint
- `CheckInService.cs` - Business logic
- `CheckIn.cs` - Entity model

### Burnout Detection Algorithm

**Criteria (configurable in appsettings.json):**
- Low mood threshold: ≤ 3.0 average
- High stress threshold: ≥ 7.0 average  
- Consecutive days for alert: 3 days
- Minimum check-ins for analysis: 5

**Implementation:**
```csharp
// In TeamService.cs
private async Task<bool> CalculateBurnoutRisk(int teamId, List<CheckIn> checkIns)
{
    // Groups by date, calculates daily averages
    // Checks for consecutive concerning days
    // Returns true if risk detected
}
```

### Real-time Updates

**SignalR Implementation:**
1. **Hub**: `TeamUpdatesHub.cs` manages connections and groups
2. **Groups**: Clients join team-specific groups (`Team_{teamId}`)
3. **Broadcasting**: Service layer triggers broadcasts after data changes
4. **Client**: Blazor components subscribe to SignalR events

**Event Types:**
- `NewCheckInReceived` - New check-in submitted
- `BurnoutAlertTriggered` - Burnout risk detected

## API Documentation

### Authentication
Currently no authentication required (anonymous check-ins).

### Endpoints

#### Check-ins
```http
POST /api/checkins
Content-Type: application/json

{
  "teamId": 1,
  "moodRating": 7,
  "stressLevel": 4,
  "notes": "Feeling productive today"
}
```

#### Teams
```http
GET /api/teams
# Returns all active teams

GET /api/teams/{id}
# Returns specific team details

GET /api/teams/{id}/summary?daysBack=30
# Returns team analytics and trends

POST /api/teams
# Creates new team

GET /api/teams/alerts
# Returns current burnout alerts
```

### Response Formats

**Team Summary:**
```json
{
  "teamId": 1,
  "teamName": "Development Team",
  "averageMood": 6.8,
  "averageStress": 4.2,
  "totalCheckIns": 45,
  "lastCheckInDate": "2025-06-01T10:30:00Z",
  "isBurnoutRisk": false,
  "recentTrends": [
    {
      "date": "2025-06-01",
      "averageMood": 7.2,
      "averageStress": 3.8,
      "checkInCount": 5
    }
  ]
}
```

## Database Schema

### Core Tables

**Teams**
- `Id` (int, PK)
- `Name` (varchar(100), unique)
- `Description` (varchar(500), nullable)
- `CreatedAt` (timestamp)
- `IsActive` (boolean)

**CheckIns**
- `Id` (int, PK)
- `TeamId` (int, FK → Teams.Id)
- `MoodRating` (int, 1-10)
- `StressLevel` (int, 1-10)
- `Notes` (varchar(500), nullable)
- `SubmittedAt` (timestamp)

### Indexes
- `CheckIns(TeamId, SubmittedAt)` - For team queries with date filtering
- `Teams(Name)` - Unique constraint
- `CheckIns(SubmittedAt)` - For time-based queries

## Real-time Features

### SignalR Hub Configuration

**Server Setup:**
```csharp
// In Program.cs
builder.Services.AddSignalR();
app.MapHub<TeamUpdatesHub>("/teamupdates");
```

**Client Connection:**
```csharp
// In SignalRService.cs
_hubConnection = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/teamupdates")
    .WithAutomaticReconnect()
    .Build();
```

### Group Management
- Clients join team-specific groups: `Team_{teamId}`
- Updates broadcast only to relevant team members
- Automatic cleanup on disconnect

## Testing

### Running Tests
```bash
# All tests
dotnet test

# Specific test class
dotnet test --filter "ClassName=TeamServiceTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Structure

**Unit Tests:**
- Service layer business logic
- Burnout calculation algorithms  
- Data validation

**Integration Tests:**
- API endpoints end-to-end
- Database operations
- SignalR functionality

**Test Data:**
- In-memory database for isolation
- Test data builders for complex scenarios
- Mocked external dependencies

## Deployment

### Environment Configuration

**Required Environment Variables:**
```bash
# Database
DATABASE_URL=postgres://user:pass@host:port/db
# or
ConnectionStrings__DefaultConnection=Host=...;Database=...

# Environment
ASPNETCORE_ENVIRONMENT=Production

# Optional: Burnout thresholds
SereniTeam__BurnoutThresholds__LowMoodThreshold=3.0
```

### Cloud Platforms

#### Azure App Service
1. Create App Service (Linux, .NET 8)
2. Configure connection string in portal
3. Deploy via GitHub Actions or VS publish

#### Render.com
1. Connect GitHub repository
2. Set build command: `dotnet publish -c Release`
3. Add PostgreSQL add-on
4. Configure environment variables

### Database Migrations
```bash
# Apply pending migrations on startup
# (handled automatically in Program.cs)
await context.Database.MigrateAsync();
```

## Contributing Guidelines

### Code Standards
- Follow existing patterns and conventions
- Add tests for new features
- Update documentation for API changes
- Use meaningful commit messages

### Pull Request Process
1. Create feature branch from `main`
2. Implement changes with tests
3. Update relevant documentation
4. Submit PR with clear description
5. Address review feedback

### Common Tasks

**Adding New Check-in Field:**
1. Update `CheckIn` entity model
2. Add migration: `dotnet ef migrations add AddNewField`
3. Update DTOs and validation
4. Modify UI forms and display
5. Update tests

**New Dashboard Metric:**
1. Add calculation logic to `TeamService`
2. Update `TeamSummaryDto`
3. Modify dashboard UI components
4. Add appropriate tests

### Getting Help
- Check existing issues on GitHub
- Review this documentation
- Ask questions in team chat/discussions
- Pair with experienced team member

---

## Quick Reference

### Common Commands
```bash
# Start development
dotnet run --project src/SereniTeam.Server

# Run tests
dotnet test

# Add migration
dotnet ef migrations add MigrationName --project src/SereniTeam.Server

# Update database
dotnet ef database update --project src/SereniTeam.Server

# Restore packages
dotnet restore
```

### Key URLs (Development)
- **Application**: https://localhost:5001
- **API Docs**: https://localhost:5001/swagger
- **SignalR Hub**: wss://localhost:5001/teamupdates

### Configuration Files
- **Development**: `appsettings.Development.json`
- **Production**: Environment variables
- **Client**: `wwwroot/appsettings.json` (if needed)

---

*This documentation is maintained alongside the codebase. Please update it when making significant changes to the project.*