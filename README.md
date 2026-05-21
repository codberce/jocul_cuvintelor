# Jocul Cuvintelor

Aplicație web .NET 10 pentru joc de întrebări și răspunsuri pe materii, cu mod singleplayer, mod multiplayer în timp real, administrare conținut, scoruri, coduri de cameră, QR pentru conectare și rulare prin Docker, PostgreSQL, Redis și Cloudflare Tunnel.

## Tehnologii

- .NET 10, ASP.NET Core, Razor Components/Blazor Server.
- Entity Framework Core 10 cu PostgreSQL prin `Npgsql.EntityFrameworkCore.PostgreSQL`.
- ASP.NET Core Identity pentru utilizatori administrativi și gazde.
- SignalR pentru actualizări multiplayer în timp real; Redis backplane opțional.
- StackExchange.Redis pentru stare activă, lock-uri distribuite și cache de camere.
- FluentValidation pentru validarea cererilor.
- QRCoder pentru codurile QR de conectare.
- Serilog pentru logare în consolă.
- xUnit, Microsoft.NET.Test.Sdk, EF Core InMemory și Microsoft.AspNetCore.Mvc.Testing pentru teste.
- Docker, Docker Compose, PostgreSQL 18, Redis 8, Cloudflare `cloudflared`.

## Fișiere rădăcină

- `README.md` - descrierea proiectului.
- `WordGame.sln` - soluția .NET care grupează proiectele din `src` și `tests`.
- `Directory.Build.props` - setări comune de build pentru proiectele .NET.
- `Directory.Packages.props` - versiuni centralizate pentru pachetele NuGet.
- `.dockerignore` - exclude fișiere inutile din contextul de build Docker.
- `.env` - variabile locale reale pentru Docker Compose; nu se publică.
- `.env.example` - exemplu de variabile necesare pentru rularea cu Docker Compose.
- `.gitignore` - exclude fișiere generate, locale sau sensibile din Git.
- `Dockerfile` - build multi-stage pentru aplicația web publicată pe ASP.NET Runtime.
- `docker-compose.yml` - pornește PostgreSQL, Redis, aplicația web și containerul `cloudflared`.

## `src`

Conține codul aplicației, separat pe straturi.

### `src/WordGame.Domain`

Modelul de domeniu: entități, enum-uri și obiecte de valoare fără dependențe de infrastructură.

- `WordGame.Domain.csproj` - proiectul de domeniu.
- `Entities/Category.cs` - categorie pentru întrebări.
- `Entities/GameEvent.cs` - eveniment produs într-o cameră/joc.
- `Entities/GameRoom.cs` - cameră multiplayer activă sau persistată.
- `Entities/GameSession.cs` - sesiune finalizată de joc.
- `Entities/LeaderboardEntry.cs` - intrare în clasament.
- `Entities/Lesson.cs` - lecție/conținut administrabil.
- `Entities/Player.cs` - jucător din cameră.
- `Entities/PlayerAnswer.cs` - răspuns trimis de jucător.
- `Entities/Question.cs` - întrebare cu răspuns, dificultate, categorie și materie.
- `Entities/Subject.cs` - materie selectabilă pentru filtrarea întrebărilor.
- `Entities/User.cs` - utilizator de domeniu.
- `Enums/Difficulty.cs` - nivelul întrebării.
- `Enums/GameEventType.cs` - tipuri de evenimente de joc.
- `Enums/GameStatus.cs` - stări ale jocului/camerei.
- `ValueObjects/GameSettings.cs` - setări compacte pentru joc.

### `src/WordGame.Application`

Logica de aplicație, contractele, DTO-urile, validarea și serviciile independente de infrastructură.

- `WordGame.Application.csproj` - proiectul stratului de aplicație.
- `ApplicationDependencyInjection.cs` - înregistrări DI pentru serviciile de aplicație.
- `DTOs/ActiveRoomState.cs` - stare serializabilă pentru camera activă.
- `DTOs/GameDtos.cs` - DTO-uri pentru fluxul multiplayer.
- `DTOs/Requests.cs` - cereri pentru creare cameră, join, răspuns, import și singleplayer.
- `DTOs/SinglePlayerDtos.cs` - DTO-uri pentru antrenamentul solo.
- `Interfaces/IActiveRoomStore.cs` - contract pentru stocarea stării active.
- `Interfaces/IAnswerNormalizer.cs` - contract pentru normalizarea răspunsurilor.
- `Interfaces/IDistributedLockProvider.cs` - contract pentru lock-uri distribuite.
- `Interfaces/IGameRoomService.cs` - contract pentru orchestration multiplayer.
- `Interfaces/IGuestSessionService.cs` - contract pentru sesiuni de invitați.
- `Interfaces/ILeaderboardService.cs` - contract pentru clasamente.
- `Interfaces/IQrCodeService.cs` - contract pentru generare QR.
- `Interfaces/IQuestionSelector.cs` - contract pentru selectarea întrebărilor filtrate.
- `Interfaces/IRoomCodeGenerator.cs` - contract pentru coduri de cameră.
- `Interfaces/IScoreCalculator.cs` - contract pentru calculul scorului.
- `Interfaces/ISinglePlayerTrainingService.cs` - contract pentru antrenament solo.
- `Options/GameOptions.cs` - opțiuni pentru durată, număr de întrebări și cameră.
- `Options/QrOptions.cs` - opțiuni pentru URL-ul public folosit în QR.
- `Options/RateLimitOptions.cs` - limite pentru endpoint-uri.
- `Options/RedisOptions.cs` - conexiune Redis.
- `Options/SecurityOptions.cs` - conturi inițiale admin/gazdă.
- `Options/SignalROptions.cs` - opțiuni SignalR, Redis backplane și Azure SignalR.
- `Services/AnswerNormalizer.cs` - curăță și normalizează răspunsurile.
- `Services/GameStateMachine.cs` - reguli pentru tranzițiile de stare.
- `Services/LeaderboardService.cs` - calculează și ordonează clasamente.
- `Services/RoomCodeGenerator.cs` - generează coduri numerice de cameră.
- `Services/ScoreCalculator.cs` - calculează punctajul pe timp și litere dezvăluite.
- `Services/SinglePlayerTrainingService.cs` - rulează jocul singleplayer în memorie.
- `Subjects/SubjectCatalog.cs` - lista materiilor, slug-uri și validare comună.
- `Validators/GameValidators.cs` - validatori FluentValidation pentru cererile de joc.

### `src/WordGame.Infrastructure`

Persistență, Redis, Identity, QR, health checks și implementări concrete pentru contractele din aplicație.

- `WordGame.Infrastructure.csproj` - proiectul de infrastructură.
- `InfrastructureDependencyInjection.cs` - înregistrări DI pentru DB, Redis, Identity, servicii și health checks.
- `Health/PostgresHealthCheck.cs` - verifică disponibilitatea PostgreSQL.
- `Health/RedisHealthCheck.cs` - verifică disponibilitatea Redis.
- `Identity/ApplicationUser.cs` - utilizator ASP.NET Identity.
- `Identity/GuestSessionService.cs` - gestionează tokenurile de invitați.
- `Migrations/20260517113812_InitialCreate.cs` - migrarea inițială a schemei.
- `Migrations/20260517113812_InitialCreate.Designer.cs` - metadate EF pentru migrarea inițială.
- `Migrations/20260520191631_AddSubjects.cs` - migrare pentru materii și filtrarea întrebărilor.
- `Migrations/20260520191631_AddSubjects.Designer.cs` - metadate EF pentru migrarea de materii.
- `Migrations/ApplicationDbContextModelSnapshot.cs` - snapshot EF al modelului curent.
- `Persistence/ApplicationDbContext.cs` - DbContext EF Core și configurarea entităților.
- `Persistence/ApplicationDbContextFactory.cs` - factory pentru comenzi EF și design-time.
- `Persistence/DatabaseSeeder.cs` - migrează, creează utilizatori, materii, categorii și întrebări inițiale.
- `Persistence/GameRoomService.cs` - implementarea principală pentru camere, jucători, întrebări și sesiuni.
- `Persistence/QuestionSelector.cs` - selectează aleator întrebări active filtrate pe materii.
- `Qr/QrCodeService.cs` - generează URL-uri și imagini QR.
- `Redis/RedisActiveRoomStore.cs` - salvează starea activă a camerelor în Redis.
- `Redis/RedisDistributedLockProvider.cs` - lock-uri distribuite prin Redis.

### `src/WordGame.Web`

Aplicația web ASP.NET Core/Blazor, interfața, hub-ul SignalR, endpoint-urile HTTP și resursele statice.

- `WordGame.Web.csproj` - proiectul web.
- `Program.cs` - configurarea aplicației, middleware, endpoint-uri, SignalR, health checks și migrare la startup.
- `appsettings.json` - configurare implicită.
- `appsettings.Development.json` - configurare locală.
- `appsettings.Production.json` - configurare de producție.
- `Components/App.razor` - documentul HTML rădăcină.
- `Components/Routes.razor` - rutarea componentelor Razor.
- `Components/_Imports.razor` - namespace-uri comune pentru componente.
- `Components/SubjectSelection.razor` - selector reutilizabil de materii.
- `Components/Layout/MainLayout.razor` - layout cu bara superioară.
- `Components/Pages/Home.razor` - meniul principal.
- `Components/Pages/HostDashboard.razor` - creare joc multiplayer și intrare cu cod.
- `Components/Pages/HostRoom.razor` - ecranul gazdei pentru lobby, întrebare și rezultate.
- `Components/Pages/Join.razor` - formular pentru intrarea unui jucător într-o cameră.
- `Components/Pages/Login.razor` - autentificare pentru administrare/gazdă.
- `Components/Pages/Play.razor` - ecranul jucătorului în multiplayer.
- `Components/Pages/Train.razor` - pagina singleplayer disponibilă la `/singleplayer`.
- `Components/Pages/AdminCategories.razor` - administrarea categoriilor.
- `Components/Pages/AdminGames.razor` - vizualizarea jocurilor/sesiunilor.
- `Components/Pages/AdminLeaderboards.razor` - vizualizarea clasamentelor.
- `Components/Pages/AdminLessons.razor` - administrarea lecțiilor.
- `Components/Pages/AdminQuestions.razor` - administrarea și importul întrebărilor.
- `Hubs/GameHub.cs` - hub SignalR pentru conectare, răspunsuri și actualizări live.
- `Middleware/ExceptionHandlingMiddleware.cs` - răspunsuri uniforme pentru erori.
- `Services/GameTimerService.cs` - worker care avansează periodic jocurile active.
- `Services/QuestionImportParser.cs` - parser JSON/CSV pentru import întrebări.
- `wwwroot/app.css` - stilurile aplicației.
- `wwwroot/wordgame.js` - comportament client pentru focus și completarea literelor.

## `tests`

Teste automate pentru logica de aplicație și pentru integrarea web.

### `tests/WordGame.UnitTests`

- `WordGame.UnitTests.csproj` - proiectul de teste unitare.
- `CoreLogicTests.cs` - teste pentru normalizare, scor și reguli de bază.
- `GameRoomServiceTests.cs` - teste pentru fluxul multiplayer și filtrarea întrebărilor.
- `SinglePlayerTrainingServiceTests.cs` - teste pentru fluxul singleplayer.

### `tests/WordGame.IntegrationTests`

- `WordGame.IntegrationTests.csproj` - proiectul de teste de integrare.
- `GameFlowIntegrationTests.cs` - teste pentru fluxuri web și servicii integrate.
- `TrainPageIntegrationTests.cs` - teste pentru pagina singleplayer și scripturile Blazor.
