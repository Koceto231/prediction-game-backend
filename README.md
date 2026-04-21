# BPFL API — Backend

ASP.NET Core 8 REST API for the **Best Prediction Football League** — a platform for football predictions, betting and fantasy football.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 |
| ORM | Entity Framework Core 8 + Npgsql |
| Database | PostgreSQL |
| Authentication | JWT + Refresh Tokens + Google OAuth |
| AI | OpenRouter (Claude Haiku) |
| Match Data | football-data.org API |
| Containerisation | Docker |

---

## Project Structure

```
BPFL.API/
├── Controllers/          # API endpoints
├── Services/             # Business logic
│   ├── Agents/           # AI agent (OpenRouter)
│   ├── External/         # football-data.org client
│   └── FantasyServices/  # Fantasy football logic
├── BackgroundJobs/       # Hosted background services
├── Models/               # Domain models
│   ├── DTO/              # Data Transfer Objects
│   ├── FantasyModel/     # Fantasy models
│   └── MatchAnalysis/    # Analysis models
├── Modules/              # Modular entities
│   ├── Odds/             # MarketDefinition, MatchMarketOdds
│   └── Wallet/           # Wallet, WalletTransaction
├── Data/                 # DBContext
├── Migrations/           # EF Core migrations
├── Middleware/           # Global exception handling
├── Exceptions/           # Custom exception types
└── Config/               # Settings classes
```

---

## API Endpoints

### Auth — `/api/auth`
| Method | Endpoint | Description |
|---|---|---|
| POST | `/register` | Register with email verification |
| POST | `/login` | Login — returns JWT + Refresh Token |
| POST | `/refresh` | Renew access token |
| POST | `/logout` | Logout (invalidate refresh token) |
| POST | `/google` | Google OAuth login |
| GET | `/verify-email` | Confirm email address |
| POST | `/forgot-password` | Send password reset email |
| POST | `/reset-password` | Set new password |

### Matches — `/api/match`
| Method | Endpoint | Description |
|---|---|---|
| GET | `/upcoming` | Upcoming matches |
| GET | `/live` | Live matches |
| GET | `/finished` | Finished matches |
| GET | `/{id}` | Match details |

### Predictions — `/api/prediction`
| Method | Endpoint | Description |
|---|---|---|
| POST | `/` | Create a prediction (includes AI analysis) |
| GET | `/me` | All my predictions |

### Betting — `/api/bet`
| Method | Endpoint | Description |
|---|---|---|
| POST | `/` | Place a bet (Winner / ExactScore / BTTS / OverUnder) |
| GET | `/me` | All my bets |

### Odds — `/api/odds`
| Method | Endpoint | Description |
|---|---|---|
| GET | `/{matchId}/{betType}` | Dynamic odds for a bet type |

### Wallet — `/api/wallet`
| Method | Endpoint | Description |
|---|---|---|
| GET | `/` | Balance and transaction history |
| POST | `/deposit` | Add coins |
| POST | `/reset` | Reset to starting balance |

### Fantasy — `/api/fantasy`
| Method | Endpoint | Description |
|---|---|---|
| GET | `/team` | My fantasy team |
| POST | `/team/select` | Select players |
| GET | `/players` | All available players |
| GET | `/gameweek/current` | Current gameweek |
| GET | `/leaderboard/{gameweekId}` | Gameweek leaderboard |
| GET | `/leaderboard/current` | Current gameweek leaderboard |

### Leaderboard — `/api/leaderboard`
| Method | Endpoint | Description |
|---|---|---|
| GET | `/` | Global points leaderboard |

### Leagues — `/api/league`
| Method | Endpoint | Description |
|---|---|---|
| POST | `/create` | Create a private league |
| POST | `/join` | Join via invite code |
| GET | `/{leagueId}` | League details and standings |
| GET | `/my` | My leagues |

### Profile — `/api/profile`
| Method | Endpoint | Description |
|---|---|---|
| GET | `/` | Profile and stats |
| PUT | `/` | Update profile |

### Admin — `/api/admin/sync`
| Method | Endpoint | Description |
|---|---|---|
| POST | `/matches` | Manual match sync |
| POST | `/teams` | Manual team sync |
| POST | `/score` | Manual points calculation |

---

## Core Systems

### Predictions
- Users predict exact scores or match winners
- AI analysis (OpenRouter / Claude Haiku) generates win probabilities and a text explanation
- **Exact Score** = 5 points | **Winner/Draw** = 3 points
- Points are calculated automatically when a match finishes

### Betting
Four bet types with dynamic odds based on Poisson probability models:

| Type | Description | Max Points |
|---|---|---|
| `Winner` | 1 / X / 2 | 1 |
| `ExactScore` | Correct score | 5 |
| `BTTS` | Both teams to score | 1 |
| `OverUnder` | Over/Under 1.5 / 2.5 / 3.5 goals | 1 |

Odds are dynamic — calculated from expected goals (xG) using a Poisson model. Market Pick bets (Winner + BTTS + OU) can be combined for up to 3 points total.

### Fantasy Football
- Each user picks 11 starters + 4 substitutes
- Players earn points based on real match statistics (goals, assists, clean sheets, etc.)
- Gameweek leaderboards track weekly performance

### Background Jobs
| Job | Interval | Description |
|---|---|---|
| `MatchSyncJob` | 15 min | Syncs matches and teams from football-data.org; recalculates odds for upcoming matches; auto-syncs fantasy players if none exist |
| `PredictionScoringJob` | 2 min | Scores predictions and resolves bets for finished matches |

---

## Configuration

Fill in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=...;Username=...;Password=..."
  },
  "Jwt": {
    "Key": "<at least 32 characters>",
    "Issuer": "BPFL",
    "Audience": "BPFL",
    "ExpirationMinutes": 15
  },
  "FootballData": {
    "BaseUrl": "https://api.football-data.org/v4/",
    "Token": "<football-data.org API key>"
  },
  "Google": {
    "ClientId": "<Google OAuth Client ID>",
    "ClientSecret": "<Google OAuth Client Secret>"
  },
  "Email": {
    "FromName": "BPFL App",
    "FromAddress": "noreply@example.com",
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "Username": "...",
    "Password": "..."
  },
  "OpenRouter": {
    "BaseUrl": "https://openrouter.ai",
    "ApiKey": "<OpenRouter API key>",
    "Model": "anthropic/claude-haiku-4-5"
  },
  "App": {
    "FrontendBaseUrl": "https://your-frontend.vercel.app"
  },
  "BackgroundJobs": {
    "LeagueCodes": [ "PL", "PD", "SA", "BL1" ]
  }
}
```

---

## Running Locally

```bash
# 1. Clone
git clone https://github.com/Koceto231/prediction-game-backend.git
cd prediction-game-backend

# 2. Fill in appsettings.json

# 3. Apply migrations
dotnet ef database update

# 4. Run
dotnet run
```

The API will be available at `https://localhost:7xxx` — Swagger UI at `/swagger`.

---

## Docker

```bash
docker build -t bpfl-api .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="..." \
  -e Jwt__Key="..." \
  bpfl-api
```

---

## Authentication

All protected endpoints require:
```
Authorization: Bearer <access_token>
```

The access token expires after 15 minutes. Use `POST /api/auth/refresh` with the refresh token (stored in an HTTP-only cookie) to obtain a new one.
