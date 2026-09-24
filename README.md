# 🐄 DairyFlow — Full Stack Dairy Farm Management System

Multi-tenant, offline-first dairy farm management system built with:

- **Backend**: ASP.NET Core 8 Web API
- **Frontend**: Blazor WebAssembly (PWA)
- **Database**: SQL Server 2019+
- **Offline**: Service Worker + IndexedDB + Background Sync
- **Auth**: JWT + Refresh Tokens
- **i18n**: English 🇬🇧 | Amharic 🇪🇹 | Oromo 🇪🇹

---

## 📁 Project Structure

```
DairyFlow/
├── src/
│   ├── DairyFlow.API/              ← ASP.NET Core 8 Web API
│   │   ├── Controllers/            ← Auth, Cows, Milk, Sales, Health, Dashboard, Sync
│   │   ├── Middleware/             ← Tenant resolution, request logging
│   │   ├── Models/DTOs/            ← Request/Response models
│   │   └── Program.cs              ← App configuration, DI, middleware pipeline
│   │
│   ├── DairyFlow.Core/             ← Domain layer (no dependencies)
│   │   ├── Entities/               ← Farm, User, Cow, MilkLog, Sale, HealthRecord...
│   │   ├── Interfaces/             ← Repository + service contracts
│   │   └── Enums/                  ← CowStatus, RecordType, etc.
│   │
│   ├── DairyFlow.Infrastructure/   ← Data + service implementations
│   │   ├── Data/
│   │   │   └── DairyFlowDbContext.cs ← EF Core + multi-tenant query filters
│   │   ├── Repositories/           ← Generic + specific repositories
│   │   └── Services/               ← Auth, Dashboard, Sync, Notifications...
│   │
│   └── DairyFlow.Web/              ← Blazor WebAssembly PWA
│       ├── Pages/                  ← Dashboard, Cows, Milk, Sales, Health, Reports...
│       ├── Components/             ← Reusable UI components
│       ├── Services/               ← SyncService, OfflineStorageService, ApiService
│       └── wwwroot/
│           ├── service-worker.js   ← Offline caching + background sync
│           ├── js/offline-storage.js ← IndexedDB wrapper
│           ├── css/dairyflow.css   ← Full UI stylesheet
│           └── i18n/translations.json ← EN + AM + OM translations
│
└── database/
    └── schema.sql                  ← Complete SQL Server schema + seed data
```

---

## 🚀 Setup & Installation

### Prerequisites
- .NET 8 SDK
- SQL Server 2019+ (or Azure SQL)
- Node.js 18+ (for Blazor WASM tooling)

### 1. Database Setup
```sql
-- Run in SQL Server Management Studio or Azure Data Studio:
USE master;
GO
-- Run /database/schema.sql
```

### 2. Configure Connection String
Edit `src/DairyFlow.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=DairyFlow;..."
  },
  "Jwt": {
    "Key": "YOUR_STRONG_SECRET_KEY_AT_LEAST_32_CHARS"
  }
}
```

### 3. Run the Application
```bash
# From solution root:
cd src/DairyFlow.API
dotnet run

# API runs at: https://localhost:5000
# Swagger UI: https://localhost:5000/api/docs
# Web App:    https://localhost:5000
```

### 4. First Login
After running, register your farm at `https://localhost:5000/register`
or use the seeded test account (update password via app first).

---

## 🔌 API Endpoints

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register new farm + owner |
| POST | `/api/auth/login` | Login, get JWT |
| POST | `/api/auth/refresh` | Refresh access token |
| POST | `/api/auth/logout` | Revoke refresh token |
| GET  | `/api/auth/me` | Current user info |

### Cows
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET  | `/api/cows` | List all cows (filter by status) |
| POST | `/api/cows` | Add new cow |
| PUT  | `/api/cows/{id}` | Update cow |
| DELETE | `/api/cows/{id}` | Remove cow |
| GET  | `/api/cows/{id}/milk-history` | Milk logs for a cow |
| GET  | `/api/cows/{id}/health-records` | Health records for a cow |
| POST | `/api/cows/{id}/sell` | Record cow sale |

### Milk
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET  | `/api/milk` | All milk logs (date range filter) |
| GET  | `/api/milk/today` | Today's production |
| GET  | `/api/milk/summary?days=30` | Daily summary |
| POST | `/api/milk` | Log milk session |
| PUT  | `/api/milk/{id}` | Update log |

### Sales, Expenses, Health
Similar CRUD endpoints at `/api/sales`, `/api/expenses`, `/api/health`

### Dashboard
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET  | `/api/dashboard` | Full dashboard data |
| GET  | `/api/dashboard/profitability` | Profitability calculator |
| GET  | `/api/dashboard/herd-rotation` | Lactation cycle analysis |

### Sync (Offline)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/sync/push` | Push offline changes to server |
| GET  | `/api/sync/pull?since=...` | Pull server changes since timestamp |
| GET  | `/api/sync/snapshot` | Full data snapshot (first load) |

---

## 📴 Offline Mode — How It Works

```
User makes a change (e.g., logs milk)
          ↓
App writes to IndexedDB immediately (instant response)
          ↓
Change added to Sync Queue in IndexedDB
          ↓
UI updates optimistically (shows change right away)
          ↓
     Is online?
    ↙         ↘
  YES          NO
   ↓            ↓
Push to API   Show 🟡 pending indicator
immediately   Queue stays in IndexedDB
   ↓            ↓
 Success      User sees changes locally
   ↓            ↓
Mark synced   Internet restored → auto-sync
```

### Conflict Resolution
- Every record has `updatedAt` timestamp
- Last-Write-Wins: newest timestamp from either device wins
- Server always the ultimate source of truth on conflicts
- Conflicts flagged in dashboard for review

---

## 🌍 Multi-Tenant Architecture

Each farm is a completely isolated tenant:
- Every table has a `FarmId` column
- EF Core Global Query Filters automatically scope all queries to the current farm
- JWT contains `farmId` claim — extracted by `TenantMiddleware` on every request
- No cross-farm data leakage possible

---

## 🌐 Language Support

Supported languages (switchable per user, persisted in profile):
- 🇬🇧 **English** (`en`)
- 🇪🇹 **Amharic** (`am`) — አማርኛ
- 🇪🇹 **Oromo** (`om`) — Afaan Oromoo

Translations live in `wwwroot/i18n/translations.json`
Blazor accesses them via a lightweight `ILocalizationService`

---

## 👥 User Roles

| Role | Permissions |
|------|-------------|
| **Owner** | Full access, manage users, financial reports |
| **Manager** | All farm operations, cannot manage users |
| **Worker** | Log milk, health records — no financial data |

---

## 📱 PWA / Mobile Install

The Blazor WASM app is a full PWA:
1. Open in Chrome on Android
2. Tap ⋮ → "Add to Home Screen"
3. Works offline after first load
4. Background sync when internet restored

---

## 🗄️ Key Database Views

- `vw_DailyMilkSummary` — Daily production totals per farm
- `vw_MonthlyFinancials` — Revenue, sales volume by month
- `vw_HerdSummary` — Herd composition snapshot

---

## 🔒 Security Notes

Before production:
1. Change `Jwt:Key` to a strong random 64+ character string
2. Set `AllowedOrigins` to your actual domain
3. Enable HTTPS only
4. Configure SQL Server with a dedicated app user (not sa)
5. Enable SQL Server auditing

---

## 📞 Support & Customization

Built for Ethiopian dairy operations.
Contact for customization, hosting support, or Amharic voice input integration.
