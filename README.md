# ⛩️ ToriiBackend

API de Torii, outil web de gestion de projet. « Torii » comme les portails
japonais : la porte d'entrée de vos projets.

Le frontend Angular vit dans le repo séparé **ToriiFrontend**.

## Stack

- **.NET 10** (ASP.NET Core Web API) — `Torii.Api`
- **EF Core** + **PostgreSQL** (Npgsql)
- **Auth** : JWT access token + refresh token (rotation à chaque usage), mots de passe BCrypt
- **Erreurs** : réponses normalisées ProblemDetails (RFC 9457)

## Prérequis

- **.NET SDK 10** (`dotnet --version` → `10.0.x`)
- **PostgreSQL** (local ou distant)
- Outil EF Core : versionné localement dans `dotnet-tools.json` → `dotnet tool restore`

## Configuration

Copier `Torii.Api/appsettings.example.json` en `Torii.Api/appsettings.json`
(ignoré par git) puis renseigner :

- `ConnectionStrings:ToriiDb` — chaîne de connexion PostgreSQL ;
- `Jwt:Key` — clé secrète de **≥ 32 caractères** (HMAC-SHA256).

> En dev, une clé vide laisse l'API démarrer mais les tokens ne seront pas
> fiables. Pour un secret hors dépôt, préférer `dotnet user-secrets` ou une
> variable d'environnement.

## Base de données

Une seule migration `InitialCreate` (tables `Users` + `RefreshTokens`) :

```bash
cd Torii.Api
dotnet ef database update
```

Pour faire évoluer le schéma : `dotnet ef migrations add <Nom>` puis
`dotnet ef database update`.

## Lancer en développement

```bash
cd Torii.Api
dotnet run --launch-profile http   # http://localhost:5080
```

- OpenAPI (dev) : `http://localhost:5080/openapi/v1.json`
- CORS autorisé pour le front sur `http://localhost:4200` (`Program.cs`).
- VS Code : **Run and Debug → « API (.NET debug) »**, tâches EF dans `.vscode/tasks.json`.
- Requêtes d'exemple : `Torii.Api/Torii.Api.http`.

## Authentification

Endpoints (`Controllers/AuthController.cs`, préfixe `/api/auth`) :

| Méthode | Route       | Description                                   | Auth       |
|---------|-------------|-----------------------------------------------|------------|
| POST    | `/register` | Crée un compte, renvoie les tokens            | non        |
| POST    | `/login`    | Connexion, renvoie les tokens                 | non        |
| POST    | `/refresh`  | Échange un refresh token contre de nouveaux   | non        |
| POST    | `/logout`   | Révoque un refresh token                      | non        |
| GET     | `/me`       | Utilisateur courant                           | **Bearer** |

```bash
curl -X POST http://localhost:5080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"a@b.co","password":"password123","displayName":"Alice"}'
```

## Structure

```
ToriiBackend/
├── Torii.slnx
└── Torii.Api/
    ├── Program.cs              # DI, EF, JWT, CORS, ProblemDetails, pipeline
    ├── Controllers/            # AuthController, ToriController (base)
    ├── Data/ToriiDbContext.cs
    ├── Dtos/AuthDtos.cs
    ├── Exceptions/             # ToriiException + Validation/NotFound/Conflict/Authentication
    ├── Handlers/               # GlobalExceptionHandler → ProblemDetails
    ├── Managers/ToriManager.cs # base des managers métier
    ├── Models/                 # Entity (base), User, RefreshToken, DTO/DtoHelper
    ├── Services/               # TokenService, AuthService
    └── Migrations/             # InitialCreate
```

## Ajouter le modèle métier

1. Créer les entités dans `Models/` (hériter de `Entity`). Les enums sont stockés
   automatiquement en chaîne MAJUSCULE, en base (`Data/UpperCaseEnumConverter.cs`) comme
   en JSON (`Json/UpperCaseEnumJson.cs`, entiers refusés en entrée, casse ignorée) ; nommer
   les valeurs en `UPPER_SNAKE` (ex. `ON_GOING`) pour garder la séparation des mots.
2. Les déclarer via `DbSet<>` dans `Data/ToriiDbContext.cs` (+ config dans `OnModelCreating`).
3. `dotnet ef migrations add <Nom>` puis `dotnet ef database update`.
4. Ajouter un manager (hérite de `ToriManager`), l'enregistrer dans `Program.cs`.
5. Exposer des contrôleurs `[Authorize]` sous `/api/...` ; lever des `ToriiException` pour les erreurs métier.

## Build de production

```bash
cd Torii.Api && dotnet publish -c Release
```

## À faire

- [ ] Modèle de données métier
- [ ] Rôles / autorisations fines si nécessaire
- [ ] Tests unitaires et CI
- [ ] Durcir le stockage des tokens (cookie httpOnly) avant prod
