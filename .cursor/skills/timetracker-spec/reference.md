# TimeTracker — Referência Técnica

## Constantes globais

### C# (`TimeTracker.Core`)

| Constante | Classe | Valor |
|-----------|--------|-------|
| `DbFileName` | `AppConstants` | `"productivity.db"` |
| `SettingsFileName` | `AppConstants` | `"app_settings.json"` |
| `DefaultPollIntervalSeconds` | `AppConstants` | `5.0` |
| `DashboardPort` | `AppConstants` | `8501` |
| `DashboardHost` | `AppConstants` | `"localhost"` |
| `AppDisplayName` | `AppConstants` | `"TimeTracker Pro"` |

| Classe | Responsabilidade |
|--------|------------------|
| `ActivityRepository` | Init WAL, `SaveActivity`, `GetAllApps`, `GetAllActivities` |
| `SettingsStore` | Load/save JSON, `UpdateAppSetting`, batch |
| `TrackingEngine` | Loop de polling via `IActiveWindowProvider` |
| `ActivityQueryService` | Load enriquecido, dates, apps com settings |
| `ActivityTextHelper` | `FormatDurationClean`, `CleanWindowTitle` |
| `Win32ActiveWindowProvider` | Captura janela ativa (projeto Tracker) |
| `DashboardProcessService` | Sobe dashboard ASP.NET como subprocesso |
| `StartupShortcutService` | Atalho `.lnk` na pasta Startup |
| `AppCategories` | Categorias válidas para personalização |

## API — Dashboard .NET

| Endpoint | Descrição |
|----------|-----------|
| `GET /api/health` | Status do serviço |
| `GET /api/meta` | Categorias válidas, porta |
| `GET /api/dates` | Datas com registros (desc) |
| `GET /api/activity?date=yyyy-MM-dd` | Registros do dia + summary |
| `GET /api/apps` | Apps do log com settings |
| `GET /api/settings` | Mapa de settings (JSON legado) |
| `PUT /api/settings/{appName}` | Atualiza um app |
| `POST /api/settings/batch` | Salva alterações em lote |

## Ciclo de vida — Tracker C#

1. `StartupShortcutService.EnsureStartupShortcut()` — cria/atualiza `.lnk` em Startup
2. `Host` inicia `TrackingBackgroundService` → `TrackingEngine.RunAsync()`
3. `DashboardProcessService.Start()` — subprocess dashboard (dev ou exe publicado)
4. `TrayApplicationContext` — bandeja bloqueia thread principal
5. Shutdown via "Sair" ou `SystemEvents.SessionEnding` — flush sessão, para dashboard e host

## Startup Windows (atalho)

- Path: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\TimeTracker Pro.lnk`
- Destino: `TimeTracker.Tracker.exe` (publicado) ou exe do `dotnet run` (dev).
- Diretório de trabalho: pasta do app (raiz do repo em dev; pasta do exe em prod).
- Remove legados `.vbs` e `.bat` com mesmo prefixo.

## Arquivos não versionados

- `productivity.db` — dados de atividade
- `app_settings.json` — personalizações do usuário
- `bin/`, `obj/`, `publish/`, `venv/`

## Roadmap

### Migração de stack

Documento completo: [MIGRATION.md](MIGRATION.md) — **Fase 3 concluída.**

| Fase | Objetivo | Status |
|------|----------|--------|
| 0 | Solution .NET, Core, tracker scaffold, dashboard esqueleto | ✅ |
| 1 | Tracker C# no uso diário | ✅ |
| 2 | Dashboard ASP.NET + Chart.js com paridade Streamlit | ✅ |
| 3 | Integração única, CI .NET, remoção Python, WebView2, testes | ✅ |

### Features de produto (pós-migração)

| Prioridade | Feature | Notas |
|------------|---------|-------|
| Alta | Filtro por categoria na sidebar | Reutiliza coluna `category` já existente |
| Alta | Exportação CSV/JSON | Dados do dia filtrado ou intervalo customizado |
| Média | Metas diárias / alertas | Requer novo schema ou tabela de metas |
| Média | Detecção de idle (AFK) | Pausar tracking quando sem input por N minutos |
| Baixa | Suporte Linux/macOS | Fora do escopo Windows-only atual |
| Baixa | WebView2 para dashboard | ✅ Janela nativa com fallback para browser |

### Implementado

- Migração completa Python → C# (.NET 8)
- Dashboard ASP.NET + Chart.js (3 abas, paridade Streamlit)
- CI release: `dotnet publish` self-contained win-x64
- WebView2 para dashboard + testes xUnit + CI build/test

## Testes

**Estado atual:** suite xUnit em `tests/TimeTracker.Core.Tests` (Core). CI em `.github/workflows/ci.yml`.

### Executar localmente

```bash
dotnet test TimeTracker.sln
```

### Cobertura atual

- `ActivityTextHelper` — formatação e limpeza de títulos
- `ActivityRepository` — filtro ≥1s, partição horária, queries
- `SettingsStore` — persistência JSON snake_case, batch
- `ActivityQueryService` — enriquecimento e filtros por data
- `TrackingEngine` — flush em cancelamento e troca de janela

### Checklist manual — app completa

- [ ] `run.bat` grava em `productivity.db` ao trocar de janela
- [ ] Dashboard carrega em `http://localhost:8501`
- [ ] Sessões < 1s não aparecem no banco
- [ ] Sessão que cruza hora cheia gera múltiplos registros
- [ ] "Sair" na bandeja faz flush da sessão ativa e encerra dashboard
- [ ] Atalho `.lnk` criado em Startup apontando para exe C#
- [ ] Logoff/reboot encerra gracefully

### Checklist manual — dashboard

- [ ] Dashboard carrega com DB populado; aviso quando vazio
- [ ] Filtro de data: atalhos, ◀/▶, calendário
- [ ] Gráficos renderizam sem erro com e sem `hex_color` configurado
- [ ] Aba Detalhes: títulos limpos (`CleanWindowTitle`)
- [ ] Personalizar Apps: salvar persiste em `app_settings.json`

### Expandir cobertura (futuro)

- API dashboard (Minimal API end-to-end)
- Evitar testes que dependam de janela ativa real, bandeja ou WebView2

## Deploy e distribuição

### Desenvolvimento

```bash
dotnet build TimeTracker.sln
run.bat   # ou run-tracker.bat
```

Startup automático via `StartupShortcutService` na primeira execução.

### Publish local (framework-dependent + instalador)

```powershell
# Requer Inno Setup 6 para gerar Setup.exe (opcional: -SkipInstaller)
.\scripts\Publish-Release.ps1 -Version "1.0.0"
```

Ícone do produto (`assets/app.ico`, fonte `assets/app-icon.png`):

- Embutido nos exes via `ApplicationIcon` (Tracker + Dashboard)
- Bandeja e janela WebView2 (`AppIconLoader`)
- Favicon do dashboard (`wwwroot/favicon.ico`)
- `SetupIconFile` do Inno Setup
- Regenerar: `powershell.exe -File .\scripts\Convert-AppIcon.ps1`

Saída:

- `artifacts/publish/` — apps framework-dependent (sem runtime embutido)
- `artifacts/installer/TimeTrackerPro-<ver>-setup-win-x64.exe` — instalador Inno Setup

Pré-requisitos de runtime (instalados pelo Setup se faltarem):

- .NET 8 **Desktop** Runtime (WinForms / Tracker)
- .NET 8 **ASP.NET Core** Runtime (Dashboard)

### Contratos do pacote instalado

| Conceito | Caminho |
|----------|---------|
| Executáveis | `{autopf}\TimeTracker Pro\` |
| Dados | `%LocalAppData%\TimeTracker Pro\` (`productivity.db`, `app_settings.json`) |
| Dev (com `TimeTracker.sln`) | raiz do repositório |

- `AppPaths.GetInstallDir()` — pasta dos exes
- `AppPaths.GetDataDir()` — pasta de dados do usuário
- Dashboard recebe `TIMETRACKER_DATA_DIR` do tracker
- Startup `.lnk` aponta para `TimeTracker.Tracker.exe`

### CI — GitHub Actions Releases

Workflow: `.github/workflows/release.yml`

| Trigger | Comportamento |
|---------|---------------|
| Push de tag `v*` | Publish framework-dependent + Inno Setup + Release |
| `workflow_dispatch` | Artifact (Setup.exe + zip portable slim) |

Artefatos:

- `TimeTrackerPro-<tag>-setup-win-x64.exe` (recomendado)
- `TimeTrackerPro-<tag>-portable-win-x64.zip` (exige runtimes no sistema)

```bash
git tag v1.0.0
git push origin v1.0.0
```

### Atualização de versão

- Tag git semântica (`v1.0.0`) dispara o release automático
- Preferir changelog nas notes geradas pelo Actions / README
- Migrar schema SQLite com script de migração se necessário
