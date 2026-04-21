# BPFL API — Backend

ASP.NET Core 8 REST API за **Best Prediction Football League** — платформа за прогнози, залози и фентъзи футбол.

---

## Технологии

| Слой | Технология |
|---|---|
| Framework | ASP.NET Core 8 |
| ORM | Entity Framework Core 8 + Npgsql |
| База данни | PostgreSQL |
| Автентикация | JWT + Refresh Tokens + Google OAuth |
| AI | OpenRouter (Claude Haiku) |
| Данни за мачове | football-data.org API |
| Контейнеризация | Docker |

---

## Структура на проекта

```
BPFL.API/
├── Controllers/          # API endpoints
├── Services/             # Бизнес логика
│   ├── Agents/           # AI агент (OpenRouter)
│   ├── External/         # football-data.org клиент
│   └── FantasyServices/  # Фентъзи логика
├── BackgroundJobs/       # Фонови задачи (Hosted Services)
├── Models/               # Domain модели
│   ├── DTO/              # Data Transfer Objects
│   ├── FantasyModel/     # Фентъзи модели
│   └── MatchAnalysis/    # Анализ модели
├── Modules/              # Модулни entity-та
│   ├── Odds/             # MarketDefinition, MatchMarketOdds
│   └── Wallet/           # Wallet, WalletTransaction
├── Data/                 # DBContext
├── Migrations/           # EF Core миграции
├── Middleware/           # Exception handling middleware
├── Exceptions/           # Custom exception типове
└── Config/               # Settings класове
```

---

## API Endpoints

### Auth — `/api/auth`
| Метод | Endpoint | Описание |
|---|---|---|
| POST | `/register` | Регистрация с email потвърждение |
| POST | `/login` | Вход с JWT + Refresh Token |
| POST | `/refresh` | Подновяване на access token |
| POST | `/logout` | Изход (изтриване на refresh token) |
| POST | `/google` | Google OAuth вход |
| GET | `/verify-email` | Потвърждение на email |
| POST | `/forgot-password` | Изпращане на reset email |
| POST | `/reset-password` | Задаване на нова парола |

### Matches — `/api/match`
| Метод | Endpoint | Описание |
|---|---|---|
| GET | `/upcoming` | Предстоящи мачове |
| GET | `/live` | Мачове в момента |
| GET | `/finished` | Завършени мачове |
| GET | `/{id}` | Детайли за мач |

### Predictions — `/api/prediction`
| Метод | Endpoint | Описание |
|---|---|---|
| POST | `/` | Създаване на прогноза (+ AI анализ) |
| GET | `/me` | Всички мои прогнози |

### Betting — `/api/bet`
| Метод | Endpoint | Описание |
|---|---|---|
| POST | `/` | Залагане (Winner / ExactScore / BTTS / OverUnder) |
| GET | `/me` | Всички мои залози |

### Odds — `/api/odds`
| Метод | Endpoint | Описание |
|---|---|---|
| GET | `/{matchId}/{betType}` | Динамични коефициенти за залог |

### Wallet — `/api/wallet`
| Метод | Endpoint | Описание |
|---|---|---|
| GET | `/` | Баланс и история |
| POST | `/deposit` | Добавяне на монети |
| POST | `/reset` | Нулиране до начален баланс |

### Fantasy — `/api/fantasy`
| Метод | Endpoint | Описание |
|---|---|---|
| GET | `/team` | Моят фентъзи отбор |
| POST | `/team/select` | Избор на играчи |
| GET | `/players` | Всички налични играчи |
| GET | `/gameweek/current` | Текущ гейм уийк |
| GET | `/leaderboard/{gameweekId}` | Класиране |
| GET | `/leaderboard/current` | Класиране за текущия гейм уийк |

### Leaderboard — `/api/leaderboard`
| Метод | Endpoint | Описание |
|---|---|---|
| GET | `/` | Глобално класиране по точки |

### Leagues — `/api/league`
| Метод | Endpoint | Описание |
|---|---|---|
| POST | `/create` | Създаване на лига |
| POST | `/join` | Присъединяване с invite код |
| GET | `/{leagueId}` | Детайли и класиране |
| GET | `/my` | Моите лиги |

### Profile — `/api/profile`
| Метод | Endpoint | Описание |
|---|---|---|
| GET | `/` | Профил и статистики |
| PUT | `/` | Обновяване на профил |

### Admin — `/api/admin/sync`
| Метод | Endpoint | Описание |
|---|---|---|
| POST | `/matches` | Ръчна синхронизация на мачове |
| POST | `/teams` | Ръчна синхронизация на отбори |
| POST | `/score` | Ръчно изчисляване на точки |

---

## Системи

### Прогнозиране
- Потребителят прогнозира точен резултат или победител
- AI анализ (OpenRouter / Claude Haiku) генерира вероятности и обяснение
- **Exact Score** = 5 точки | **Winner/Draw** = 3 точки
- Точките се изчисляват автоматично при приключване на мача

### Залагане
Четири типа залози с динамични коефициенти базирани на Poisson вероятности:

| Тип | Описание | Точки |
|---|---|---|
| `Winner` | 1/X/2 | 1 |
| `ExactScore` | Точен резултат | 5 |
| `BTTS` | И двата отбора вкарват | 1 |
| `OverUnder` | Над/под 1.5 / 2.5 / 3.5 гола | 1 |

Коефициентите са динамични — изчисляват се от очакваните голове (xG) по Poisson модел.

### Фентъзи
- Всеки потребител избира 11 играча + 4 резерви
- Играчите получават точки на база реални статистики (голове, асистенции, чисти листове и т.н.)
- Класиране по гейм уийкове

### Фонови задачи
| Job | Интервал | Описание |
|---|---|---|
| `MatchSyncJob` | 15 мин | Синхронизация на мачове и отбори от football-data.org; изчислява odds за предстоящи мачове; auto-sync на фентъзи играчи |
| `PredictionScoringJob` | 2 мин | Оценяване на прогнози и залози за завършени мачове |

---

## Конфигурация

Копирай `appsettings.json` и попълни:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=...;Username=...;Password=..."
  },
  "Jwt": {
    "Key": "<минимум 32 символа>",
    "Issuer": "BPFL",
    "Audience": "BPFL",
    "ExpirationMinutes": 15
  },
  "FootballData": {
    "BaseUrl": "https://api.football-data.org/v4/",
    "Token": "<football-data.org API ключ>"
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
    "ApiKey": "<OpenRouter API ключ>",
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

## Стартиране локално

```bash
# 1. Клониране
git clone https://github.com/Koceto231/prediction-game-backend.git
cd prediction-game-backend

# 2. Попълни appsettings.json с горните стойности

# 3. Приложи миграциите
dotnet ef database update

# 4. Стартирай
dotnet run
```

API ще е достъпно на `https://localhost:7xxx` — Swagger UI на `/swagger`.

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

## Автентикация

Всички защитени endpoints изискват:
```
Authorization: Bearer <access_token>
```

Access token-ът изтича след 15 минути. Използвай `POST /api/auth/refresh` с refresh token-а (в HTTP-only cookie) за подновяване.
