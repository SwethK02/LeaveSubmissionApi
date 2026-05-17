# Leave Submission API — Azure Function App

A serverless REST API built with **C# / .NET 8 Azure Functions (Isolated Worker)** that accepts worker leave submissions and persists them at day-level granularity into **Azure SQL Database**.

---

## Architecture

```
POST /api/v1/leave-submissions
        │
        ▼
┌─────────────────────────┐
│  LeaveSubmissionFunction │  Azure Function (HTTP Trigger)
│  - Parse & validate JSON │
│  - Return HTTP responses │
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│  LeaveSubmissionService  │  Business Logic
│  - Decompose leave period│
│  - Generate working days │
│  - Distribute quantities │
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│    LeaveRepository       │  Data Layer (Dapper)
│  - Insert LeaveSubmission│
│  - Insert LeaveDays      │
│  - Transactional         │
└────────────┬────────────┘
             │
             ▼
    Azure SQL Database
  ┌───────────────────┐
  │  LeaveSubmission  │
  │  LeaveDay         │
  └───────────────────┘
```

---

## Project Structure

```
LeaveSubmissionApi.sln
├── LeaveSubmissionFunction/
│   ├── Functions/
│   │   └── LeaveSubmissionFunction.cs   # HTTP Trigger
│   ├── Models/
│   │   └── Models.cs                    # DTOs & DB entities
│   ├── Services/
│   │   └── LeaveSubmissionService.cs    # Business logic
│   ├── Data/
│   │   └── LeaveRepository.cs           # Dapper repository
│   ├── Validators/
│   │   └── LeaveSubmissionValidator.cs  # FluentValidation rules
│   ├── Program.cs                       # DI & host setup
│   ├── host.json
│   └── local.settings.json              # Local dev only
├── tests/
│   └── LeaveSubmissionFunction.Tests/
│       └── LeaveSubmissionTests.cs      # xUnit + Moq tests
├── sql/
│   └── schema.sql                       # SQL Server schema
└── .github/workflows/deploy.yml         # CI/CD pipeline
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- Azure Free Account (Function App + Azure SQL)
- SQL Server (local) or [Azure SQL Database](https://azure.microsoft.com/en-au/products/azure-sql/database)

---

## Local Development

### 1. Set up the database

Run `sql/schema.sql` against a local SQL Server or Azure SQL instance.

### 2. Configure connection string

Edit `LeaveSubmissionFunction/local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SqlConnectionString": "Server=localhost;Database=LeaveDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 3. Run the function locally

```bash
cd LeaveSubmissionFunction
func start
```

### 4. Run tests

```bash
dotnet test
```

---

## Azure Deployment (Free Tier)

### Step 1 — Create Azure resources

```bash
# Login
az login

# Variables — change these
RESOURCE_GROUP="rg-leave-api"
LOCATION="australiaeast"
STORAGE_ACCOUNT="stleaveapi$RANDOM"
FUNCTION_APP="leave-submission-api-$RANDOM"
SQL_SERVER="sql-leave-api-$RANDOM"
SQL_DB="LeaveDb"
SQL_ADMIN="sqladmin"
SQL_PASSWORD="YourStr0ngP@ssword!"

# Resource Group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Storage Account (required by Function App)
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS

# Function App (Consumption = Free tier)
az functionapp create \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --storage-account $STORAGE_ACCOUNT \
  --consumption-plan-location $LOCATION \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --os-type Windows

# Azure SQL Server
az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $SQL_ADMIN \
  --admin-password $SQL_PASSWORD

# Azure SQL Database (Free tier: 32GB/month free)
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name $SQL_DB \
  --edition Free

# Allow Azure services to access SQL
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### Step 2 — Run the schema script

Connect to your Azure SQL Database (via Azure Portal Query Editor or SSMS) and run `sql/schema.sql`.

### Step 3 — Configure connection string on Function App

```bash
CONNECTION_STRING="Server=tcp:$SQL_SERVER.database.windows.net,1433;Database=$SQL_DB;User ID=$SQL_ADMIN;Password=$SQL_PASSWORD;Encrypt=True;TrustServerCertificate=False;"

az functionapp config appsettings set \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --settings "SqlConnectionString=$CONNECTION_STRING"
```

### Step 4 — Deploy

```bash
cd LeaveSubmissionFunction
func azure functionapp publish $FUNCTION_APP
```

---

## API Reference

### POST `/api/v1/leave-submissions`

**Headers:** `Content-Type: application/json`  
**Auth:** Function key required (pass as `?code=<key>` or `x-functions-key` header)

#### Request Body

```json
{
  "leaveSubmission": {
    "submissionId": "LS-2026-000123",
    "submittedDate": "2026-02-15",
    "status": "Submitted",
    "worker": {
      "workerId": "W123456",
      "employeeNumber": "90030366",
      "sourceSystem": "HRIS"
    },
    "leavePeriod": {
      "startDate": "2026-03-02 00:00:00.00",
      "endDate": "2026-03-20 23:59:59.99",
      "totalWeeks": 3,
      "totalWorkingDays": 15
    },
    "leaveDetails": [
      {
        "leaveTypeCode": "AL",
        "leaveTypeDescription": "Annual Leave",
        "leaveCategory": "Paid",
        "unitOfMeasure": "Days",
        "quantity": 15
      }
    ],
    "approver": {
      "approverId": "M987654",
      "approvalStatus": "Pending"
    },
    "comments": "Planned annual leave for personal travel."
  }
}
```

#### Responses

| Status | Meaning |
|--------|---------|
| `201 Created` | Successfully processed |
| `400 Bad Request` | Missing/malformed JSON |
| `409 Conflict` | SubmissionId already exists |
| `422 Unprocessable Entity` | Validation errors (details returned) |
| `500 Internal Server Error` | Unexpected server error |

#### 201 Response Example

```json
{
  "submissionId": "LS-2026-000123",
  "status": "Submitted",
  "workingDaysPersisted": 15,
  "message": "Leave submission processed successfully. 15 working day(s) persisted."
}
```

#### 422 Response Example

```json
{
  "error": "Validation failed.",
  "details": [
    "SubmissionId is required.",
    "StartDate must be less than or equal to EndDate."
  ]
}
```

---

## Business Rules

- Only **Monday–Friday** are persisted as leave days (weekends skipped)
- Public holidays are **out of scope**
- Duplicate `SubmissionId` returns `409 Conflict`
- Leave quantity is evenly distributed across working days
- All DB writes are **transactional** — both tables succeed or both roll back

---

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.Azure.Functions.Worker` | Azure Functions isolated worker |
| `Microsoft.Data.SqlClient` | SQL Server connectivity |
| `Dapper` | Lightweight ORM |
| `FluentValidation` | Request validation |
| `Newtonsoft.Json` | JSON serialisation |
| `xunit` + `Moq` + `FluentAssertions` | Testing |
