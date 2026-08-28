# TimeTracker Pro — Migração de Stack

Documento vivo do processo de migração **Python/Streamlit → C#/ASP.NET Core + Chart.js**.

**Última atualização:** 2026-08-28  
**Fase atual:** Fase 3 concluída  
**Commit de referência:** Fase 3 — remoção Python, CI dotnet publish, entry point único C#

---

## Por que migrar

O stack Python foi excelente para **validar o produto** (~900 linhas, dashboard rico em pouco tempo). Para um app **Windows permanente na bandeja**, surgiram fricções:

| Problema (Python) | Solução (alvo) |
|-------------------|----------------|
| Runtime pesado 24/7 | Tracker C# leve (.NET 8 single-file) |
| Streamlit como subprocess | Dashboard web desacoplado (API + HTML) |
| PyInstaller empacota ecossistema inteiro | `dotnet publish` com binário menor |
| Integração Windows via camadas (`pywin32`, `pystray`) | APIs nativas (Win32, NotifyIcon, Startup `.lnk`) |

**Decisão:** não reescrever tudo de uma vez. Migração **incremental no mesmo repositório**, mantendo contratos de dados estáveis.

---

## Stack alvo (confirmada)

```
┌─────────────────────────────────────────┐
│  TimeTracker.Tracker (C# / .NET 8)      │
│  • Win32 — janela ativa                 │
│  • NotifyIcon — bandeja                 │
│  • Startup .lnk                         │
│  • BackgroundService — polling 5s       │
│  • DashboardProcessService — subprocess │
└──────────────────┬──────────────────────┘
                   │ productivity.db
                   │ app_settings.json
┌──────────────────▼──────────────────────┐
│  TimeTracker.Dashboard (ASP.NET Core)   │
│  • Minimal API — REST                   │
│  • wwwroot — HTML + CSS + Chart.js      │
└─────────────────────────────────────────┘

TimeTracker.Core — biblioteca compartilhada (SQLite, JSON, TrackingEngine)
```

| Camada | Tecnologia | Status |
|--------|------------|--------|
| Tracker | C# .NET 8 + WinForms (tray) | ✅ |
| Core | C# class library | ✅ |
| Dashboard API | ASP.NET Core Minimal API | ✅ |
| Dashboard UI | HTML + Chart.js (vanilla) | ✅ |
| Dados | SQLite WAL + JSON | ✅ contrato mantido |

**Não adotado:** Node.js (Chart.js funciona com qualquer backend estático); C++ (C# é mais produtivo no Windows para este caso).

---

## Estratégia de repositório

**Mesmo repo, monorepo.** Python legado removido na Fase 3.

**Estrutura atual:**

```
TimeTracker/
├── TimeTracker.sln
├── run.bat / run-tracker.bat    # entry point C#
├── run-dashboard.bat            # dashboard .NET (dev)
└── src/
    ├── TimeTracker.Core/
    ├── TimeTracker.Tracker/
    └── TimeTracker.Dashboard/
```

---

## Fases e progresso

### Legenda

- ✅ Concluído
- 🔶 Parcial / em andamento
- ⬜ Pendente

### Fase 0 — Preparação ✅

**Objetivo:** solution .NET, Core com contratos, tracker compilando, dashboard esqueleto.

| Item | Status |
|------|--------|
| `TimeTracker.sln` na raiz | ✅ |
| `TimeTracker.Core` — SQLite WAL, schema `activity_log` | ✅ |
| `TimeTracker.Core` — `SettingsStore` (JSON snake_case) | ✅ |
| `TimeTracker.Core` — `TrackingEngine` (poll 5s, partição horária, ≥1s) | ✅ |
| `TimeTracker.Tracker` — Win32 capture | ✅ |
| `TimeTracker.Tracker` — NotifyIcon + menu bandeja | ✅ |
| `TimeTracker.Tracker` — startup `.lnk` | ✅ |
| `TimeTracker.Dashboard` — API `/api/health`, `/api/settings` (stub) | ✅ |
| `TimeTracker.Dashboard` — `wwwroot` placeholder | ✅ |
| `run-tracker.bat`, `run-dashboard.bat` | ✅ |
| README + spec atualizados | ✅ |
| Build `dotnet build TimeTracker.sln` | ✅ |

### Fase 1 — Tracker C# operacional ✅

**Objetivo:** tracker C# substitui `tracker.py` no uso diário.

| Item | Status |
|------|--------|
| Paridade de gravação com `tracker.py` | ✅ |
| Resolução de `AppDir` (dev: raiz do repo; prod: pasta do exe) | ✅ |
| Bandeja abre dashboard (`localhost:8501`) | ✅ |
| Shutdown graceful (flush sessão ativa) | ✅ via `CancellationToken` |
| Handler shutdown Windows (logoff/reboot) | ✅ via `SystemEvents.SessionEnding` |
| Testes manuais documentados | 🔶 checklist abaixo |
| CI: build .NET no GitHub Actions | ✅ (via release workflow) |

**Critério de done:** rodar `run-tracker.bat` no dia a dia; dados no mesmo `productivity.db`. ✅

### Fase 2 — Dashboard ASP.NET + Chart.js ✅

**Objetivo:** paridade funcional com as 3 abas Streamlit.

| Item | Status |
|------|--------|
| `ActivityQueryService` — consultas agregadas | ✅ |
| `ActivityTextHelper` — títulos e duração | ✅ |
| `GET /api/dates`, `GET /api/activity`, `GET /api/apps` | ✅ |
| `PUT /api/settings/{appName}`, `POST /api/settings/batch` | ✅ |
| UI — Visão Geral (métricas, donut, timeline, ranking, pizza, tabela) | ✅ |
| UI — Detalhes por App | ✅ |
| UI — Personalizar Apps | ✅ |
| Filtros de data (hoje/ontem, ◀/▶, calendário) | ✅ |
| Paginação ranking (+5) | ✅ |

**Critério de done:** feature parity; usuário não precisa mais do Streamlit. ✅

### Fase 3 — Integração e limpeza ✅

**Objetivo:** um único entry point C#; remover Python.

| Item | Status |
|------|--------|
| Tracker inicia dashboard ASP.NET como subprocesso | ✅ `DashboardProcessService` |
| Porta única `8501` | ✅ |
| WebView2 — janela nativa sem browser externo | ✅ |
| CI release: `dotnet publish` substitui PyInstaller | ✅ |
| Remover `main.py`, `tracker.py`, `dashboard/`, `requirements.txt`, hooks Streamlit | ✅ |
| Mover/atualizar README para stack final | ✅ |
| Atualizar startup `.lnk` para exe C# publicado | ✅ |
| Handler shutdown Windows (logoff/reboot) | ✅ |
| Testes automatizados xUnit (`TimeTracker.Core.Tests`) | ✅ |
| CI build + test (`.github/workflows/ci.yml`) | ✅ |

---

## Mapa de portabilidade (Python → C#)

| Python (removido) | C# | Fase | Status |
|-------------------|-----|------|--------|
| `tracker.py` → `ProductivityTracker` | `TrackingEngine` + `ActivityRepository` | 0–1 | ✅ |
| `tracker.py` → Win32 capture | `Win32ActiveWindowProvider` | 0 | ✅ |
| `tracker.py` → settings JSON | `SettingsStore` | 0 | ✅ |
| `app_paths.py` | `AppPaths` (walk up até `TimeTracker.sln`) | 0 | ✅ |
| `main.py` → tray | `TrayApplicationContext` | 0 | ✅ |
| `main.py` → startup shortcut | `StartupShortcutService` | 0 | ✅ |
| `main.py` → spawn Streamlit | `DashboardProcessService` | 3 | ✅ |
| `main.py` → shutdown handler | `SystemEvents.SessionEnding` | 3 | ✅ |
| `dashboard/data.py` | `ActivityQueryService` | 2 | ✅ |
| `dashboard/utils.py` | `ActivityTextHelper` + `wwwroot/js/utils.js` | 2 | ✅ |
| `dashboard/charts.py` | `wwwroot/js/charts.js` (Chart.js) | 2 | ✅ |
| `dashboard/filters.py` | `wwwroot/js/app.js` (filtros) | 2 | ✅ |
| `dashboard/overview.py` | `wwwroot/js/app.js` (Visão Geral) | 2 | ✅ |
| `dashboard/details.py` | `wwwroot/js/app.js` (Detalhes) | 2 | ✅ |
| `dashboard/settings.py` | `wwwroot/js/app.js` (Personalizar) + `AppCategories` | 2 | ✅ |

---

## Contratos que NÃO mudam

Estes formatos são a **ponte** entre stacks. Alterar exige migração de dados + update do spec.

### SQLite — `activity_log`

Inalterado. Ver `SKILL.md` → Contratos de dados.

### JSON — `app_settings.json`

Inalterado. Chaves snake_case: `display_name`, `hex_color`, `category`.

### Regras de negócio do tracker

- `poll_interval = 5.0`s
- Descartar registros com duração < 1s
- Particionar sessões na hora cheia
- Janelas protegidas → `app_name = "System/Protected"`
- WAL habilitado na conexão

---

## Fluxos de dev

| Objetivo | Comando |
|----------|---------|
| **App completa (recomendado)** | `run.bat` ou `run-tracker.bat` — tracker + dashboard na porta 8501 |
| Dashboard isolado (dev) | `run-dashboard.bat` |
| Build .NET | `dotnet build TimeTracker.sln` |
| Publish local | ver `reference.md` → Deploy |

### Onde ficam os dados

Em desenvolvimento, `AppPaths` sobe diretórios até encontrar `TimeTracker.sln` → grava na **raiz do repo**.

Em produção (instalado / publish sem `.sln`), dados em **`%LocalAppData%\TimeTracker Pro\`**; executáveis em `GetInstallDir()`.

Release: publish **framework-dependent** + instalador **Inno Setup** (`installer/TimeTracker.iss`, `scripts/Publish-Release.ps1`).

---

## Checklist de validação

Marcar conforme for testando.

### App completa C#

- [ ] `run.bat` — ícone aparece na bandeja
- [ ] Trocar janela gera registros em `productivity.db`
- [ ] Sessões < 1s não são gravadas
- [ ] Sessão cruzando hora cheia gera múltiplos registros
- [ ] Janela protegida registra `System/Protected`
- [ ] "Abrir Dashboard" abre `http://localhost:8501`
- [ ] "Sair" encerra tracker, dashboard e faz flush da sessão
- [ ] Atalho Startup aponta para exe C# correto
- [ ] Logoff/reboot encerra gracefully

---

## Decisões registradas

| Data | Decisão | Motivo |
|------|---------|--------|
| 2026-08-28 | C# em vez de C++ para tracker | Produtividade, integração Windows nativa, SQLite first-class |
| 2026-08-28 | ASP.NET Core em vez de FastAPI para dashboard | Consolidar em um runtime; mesmo ecossistema do tracker |
| 2026-08-28 | Chart.js vanilla (sem React/Vue) | Dashboard local simples; evita toolchain frontend pesada |
| 2026-08-28 | Monorepo (não repo novo) | Histórico, spec, convivência incremental |
| 2026-08-28 | Remover Python na Fase 3 | Stack única C#; CI com `dotnet publish` |
| 2026-08-28 | Release framework-dependent + Inno Setup | Runtime .NET compartilhado do sistema; Setup pequeno |
| 2026-08-28 | Porta 8501 reservada ao dashboard .NET | Substitui Streamlit definitivamente |

---

## Próximos passos (pós-migração)

1. Features de produto (export CSV, idle detection, filtro por categoria)
2. Expandir cobertura de testes (API dashboard, integração end-to-end)

---

## Manutenção deste documento

**Atualizar sempre que:**

- Concluir um item de fase (marcar ✅)
- Tomar decisão arquitetural nova (tabela de decisões)
- Mudar contrato de dados, portas ou entry points

**Também atualizar:** `SKILL.md` (changelog), `reference.md` (API/checklists), `README.md` (instruções de uso).
