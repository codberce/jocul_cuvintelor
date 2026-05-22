# Jocul Cuvintelor

Aplicație web .NET 10 pentru un joc educațional de întrebări pe materii, cu mod singleplayer, mod multiplayer în timp real, întrebări filtrate pe materii, scoruri, coduri de cameră, QR de conectare și deploy prin Docker, PostgreSQL, Redis și Cloudflare Tunnel.

## Tehnologii

- .NET 10, ASP.NET Core, Razor Components/Blazor Server.
- Entity Framework Core 10, PostgreSQL și providerul Npgsql.
- SignalR pentru multiplayer în timp real și Redis backplane opțional.
- StackExchange.Redis pentru camere active, mapări de conexiuni și lock-uri distribuite.
- FluentValidation pentru validarea cererilor.
- QRCoder pentru coduri QR.
- Serilog pentru logare.
- xUnit, EF Core InMemory și Microsoft.AspNetCore.Mvc.Testing pentru teste.
- Docker, Docker Compose, PostgreSQL, Redis și `cloudflared` pentru publicare la `gravital.space`.

## Rădăcină

- `README.md` - descrierea structurii proiectului.
- `prezentare-proiect.html` - prezentare HTML a proiectului.
- `WordGame.sln` - soluția .NET pentru proiectele din `src` și `tests`.
- `Directory.Build.props` - setări comune de compilare.
- `Directory.Packages.props` - versiuni centralizate pentru pachetele NuGet.
- `Dockerfile` - build multi-stage pentru aplicația web.
- `docker-compose.yml` - pornește PostgreSQL, Redis, aplicația web și Cloudflare Tunnel.
- `.env` - variabile locale reale pentru Docker Compose.
- `.env.example` - șablon pentru variabilele Docker Compose.
- `.dockerignore` - exclude fișiere inutile din build-ul Docker.
- `.gitignore` - exclude fișiere locale, generate sau sensibile.

## `src/WordGame.Domain`

- `WordGame.Domain.csproj` - proiectul de domeniu.
- `Entities/Category.cs` - categorie pentru întrebări.
- `Entities/GameEvent.cs` - eveniment produs într-o cameră.
- `Entities/GameRoom.cs` - cameră multiplayer.
- `Entities/GameSession.cs` - sesiune de joc persistată.
- `Entities/LeaderboardEntry.cs` - rezultat pentru clasament.
- `Entities/Lesson.cs` - lecție informativă.
- `Entities/Player.cs` - jucător multiplayer.
- `Entities/PlayerAnswer.cs` - răspuns trimis de jucător.
- `Entities/Question.cs` - întrebare cu răspuns, dificultate, categorie și materie.
- `Entities/Subject.cs` - materie selectabilă.
- `Enums/Difficulty.cs` - dificultatea întrebării.
- `Enums/GameEventType.cs` - tipuri de evenimente.
- `Enums/GameStatus.cs` - stări ale camerei/jocului.
- `ValueObjects/GameSettings.cs` - setări ale unei camere.

## `src/WordGame.Application`

- `WordGame.Application.csproj` - proiectul stratului de aplicație.
- `ApplicationDependencyInjection.cs` - servicii de aplicație pentru DI.
- `DTOs/ActiveRoomState.cs` - stare serializată pentru camere active.
- `DTOs/GameDtos.cs` - DTO-uri pentru multiplayer.
- `DTOs/Requests.cs` - cereri pentru cameră, join, răspuns și singleplayer.
- `DTOs/SinglePlayerDtos.cs` - DTO-uri pentru antrenament solo.
- `Interfaces/*` - contracte pentru camere, sesiuni de invitați, QR, scor, clasamente, Redis și selecția întrebărilor.
- `Options/GameOptions.cs` - reguli de joc.
- `Options/GuestSessionOptions.cs` - durata tokenurilor temporare pentru jucători invitați.
- `Options/QrOptions.cs` - URL public și dimensiuni QR.
- `Options/RateLimitOptions.cs` - limite pentru create room, join și submit answer.
- `Options/RedisOptions.cs` - conexiunea și setările Redis.
- `Options/SignalROptions.cs` - backplane Redis/Azure SignalR.
- `Services/AnswerNormalizer.cs` - normalizează răspunsurile.
- `Services/GameStateMachine.cs` - validează tranzițiile de stare.
- `Services/LeaderboardService.cs` - construiește clasamente.
- `Services/RoomCodeGenerator.cs` - generează coduri de cameră.
- `Services/ScoreCalculator.cs` - calculează punctajul.
- `Services/SinglePlayerTrainingService.cs` - rulează singleplayer.
- `Subjects/SubjectCatalog.cs` - lista materiilor și ținta de 100 întrebări active pe materie.
- `Validators/GameValidators.cs` - validatori FluentValidation.

## `src/WordGame.Infrastructure`

- `WordGame.Infrastructure.csproj` - proiectul de infrastructură.
- `InfrastructureDependencyInjection.cs` - conectează PostgreSQL, Redis, servicii, QR și health checks.
- `Health/PostgresHealthCheck.cs` - verifică PostgreSQL.
- `Health/RedisHealthCheck.cs` - verifică Redis.
- `Data/Seed/questions.*.json` - banca de 100 întrebări active pentru fiecare materie.
- `Sessions/GuestSessionService.cs` - creează și validează tokenuri temporare pentru jucători invitați.
- `Migrations/*` - istoricul schemei EF Core; migrarea curentă elimină tabelele vechi de Identity.
- `Persistence/ApplicationDbContext.cs` - modelul EF Core activ.
- `Persistence/ApplicationDbContextFactory.cs` - factory pentru design-time EF.
- `Persistence/DatabaseSeeder.cs` - rulează migrațiile, citește fișierele JSON de seed și sincronizează baza cu 100 întrebări active pe materie.
- `Persistence/GameRoomService.cs` - orchestration pentru camere, jucători, întrebări și sesiuni.
- `Persistence/QuestionSelector.cs` - alege aleator întrebări active filtrate pe materiile selectate.
- `Qr/QrCodeService.cs` - generează URL-uri și imagini QR.
- `Redis/RedisActiveRoomStore.cs` - salvează camere active în Redis.
- `Redis/RedisDistributedLockProvider.cs` - lock-uri distribuite prin Redis.

## `src/WordGame.Web`

- `WordGame.Web.csproj` - proiectul web.
- `Program.cs` - configurare ASP.NET Core, SignalR, rate limiting, health checks și migrare la startup.
- `appsettings.json` - configurare implicită.
- `appsettings.Development.json` - configurare locală.
- `appsettings.Production.json` - configurare pentru deploy.
- `Components/App.razor` - documentul HTML rădăcină.
- `Components/Routes.razor` - rutare Razor.
- `Components/_Imports.razor` - namespace-uri comune.
- `Components/SubjectSelection.razor` - selector reutilizabil de materii.
- `Components/Layout/MainLayout.razor` - layout și bară superioară.
- `Components/Pages/Home.razor` - meniul principal.
- `Components/Pages/HostDashboard.razor` - creare cameră multiplayer și intrare cu cod.
- `Components/Pages/HostRoom.razor` - ecranul gazdei.
- `Components/Pages/Join.razor` - formular pentru numele jucătorului.
- `Components/Pages/Play.razor` - ecranul jucătorului în multiplayer.
- `Components/Pages/Train.razor` - pagina singleplayer de la `/singleplayer`.
- `Hubs/GameHub.cs` - hub SignalR pentru evenimente live.
- `Middleware/ExceptionHandlingMiddleware.cs` - tratare uniformă a erorilor.
- `Services/GameTimerService.cs` - worker pentru expirarea întrebărilor și camerelor.
- `wwwroot/app.css` - stilurile aplicației.
- `wwwroot/wordgame.js` - focus, input mobil, sunete și volum.

## `tests/WordGame.UnitTests`

- `WordGame.UnitTests.csproj` - proiectul de teste unitare.
- `CoreLogicTests.cs` - teste pentru normalizare, scor și reguli de bază.
- `GameRoomServiceTests.cs` - teste pentru multiplayer și filtrarea pe materii.
- `SinglePlayerTrainingServiceTests.cs` - teste pentru singleplayer.

## `tests/WordGame.IntegrationTests`

- `WordGame.IntegrationTests.csproj` - proiectul de teste de integrare.
- `GameFlowIntegrationTests.cs` - teste pentru fluxul multiplayer.
- `TrainPageIntegrationTests.cs` - teste pentru pagina singleplayer și scripturile client.
