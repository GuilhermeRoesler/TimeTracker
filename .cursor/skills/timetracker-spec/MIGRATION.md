# TimeTracker Pro — Migração de Stack

Documento vivo do processo de migração **Python/Streamlit → C#/ASP.NET Core + Chart.js**.

**Última atualização:** 2026-08-28  
**Fase atual:** 0 concluída · Fase 1 em andamento  
**Commit de referência:** `6f149a9` — scaffold .NET

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
| Tracker | C# .NET 8 + WinForms (tray) | ✅ scaffold |
| Core | C# class library | ✅ scaffold |
| Dashboard API | ASP.NET Core Minimal API | 🔶 esqueleto |
| Dashboard UI | HTML + Chart.js (vanilla) | 🔶 placeholder |
| Dados | SQLite WAL + JSON | ✅ contrato mantido |

**Não adotado:** Node.js (Chart.js funciona com qualquer backend estático); C++ (C# é mais produtivo no Windows para este caso).

---

## Estratégia de repositório

**Mesmo repo, monorepo.** Não criar repositório separado.

| Motivo | Detalhe |
|--------|---------|
| Histórico | Issues, tags, CI existentes |
| Spec | `.cursor/skills/timetracker-spec/` continua válido |
| Convivência | Python e C# paralelos por fases |
| Contrato | Mesmo `productivity.db` e `app_settings.json` |

**Estrutura atual:**

```
TimeTracker/
├── TimeTracker.sln
├── run-tracker.bat              # tracker C#
├── run-dashboard.bat            # dashboard .NET (dev)
├── run.bat                      # app Python legada (completa)
├── main.py, tracker.py          # legado — remover na Fase 3
├── dashboard/                   # legado Streamlit — remover na Fase 3
└── src/
    ├── TimeTracker.Core/
    ├── TimeTracker.Tracker/
    └── TimeTracker.Dashboard/
```

Python **não** foi movido para `legacy/` ainda — continua na raiz e funcional.

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

### Fase 1 — Tracker C# operacional 🔶

**Objetivo:** tracker C# substitui `tracker.py` no uso diário; dashboard Streamlit continua.

| Item | Status |
|------|--------|
| Paridade de gravação com `tracker.py` | 🔶 implementado, falta validação manual sistemática |
| Resolução de `AppDir` (dev: raiz do repo; prod: pasta do exe) | ✅ |
| Bandeja abre Streamlit (`localhost:8501`) | ✅ |
| Shutdown graceful (flush sessão ativa) | ✅ via `CancellationToken` |
| Handler shutdown Windows (logoff/reboot) | ⬜ |
| Testes manuais documentados | ⬜ checklist abaixo |
| CI: build .NET no GitHub Actions | ⬜ |

**Critério de done:** rodar só `run-tracker.bat` + `streamlit run dashboard/app.py` no dia a dia; dados idênticos ao Python.

### Fase 2 — Dashboard ASP.NET + Chart.js ⬜

**Objetivo:** paridade funcional com as 3 abas Streamlit.

| Item | Status |
|------|--------|
| `ActivityQueryService` — consultas agregadas | ⬜ |
| `GET /api/dates` — dias com registros | ⬜ |
| `GET /api/activity?date=` — registros enriquecidos | ⬜ |
| `PUT /api/settings/{appName}` | ⬜ |
| Portar `clean_window_title` | ⬜ |
| Portar `format_duration_clean` | ⬜ |
| UI — Visão Geral (métricas, donut, timeline, ranking, pizza, tabela) | ⬜ |
| UI — Detalhes por App | ⬜ |
| UI — Personalizar Apps | ⬜ |
| Filtros de data (hoje/ontem, ◀/▶, calendário) | ⬜ |
| Paginação ranking (+5) | ⬜ |

**Referência de comportamento:** módulos Python em `dashboard/` (fonte da verdade até remoção).

**Critério de done:** feature parity; usuário não precisa mais do Streamlit.

### Fase 3 — Integração e limpeza ⬜

**Objetivo:** um único entry point C#; remover Python.

| Item | Status |
|------|--------|
| Tracker inicia dashboard ASP.NET como subprocesso | ⬜ |
| Porta única `8501` (ou configurável) | ⬜ |
| WebView2 (opcional) — janela nativa sem browser externo | ⬜ |
| CI release: `dotnet publish` substitui PyInstaller | ⬜ |
| Remover `main.py`, `tracker.py`, `dashboard/`, `requirements.txt`, hooks Streamlit | ⬜ |
| Mover/atualizar README para stack final | ⬜ |
| Atualizar startup `.lnk` para exe C# publicado | ✅ parcial (já aponta para exe atual) |

---

## Mapa de portabilidade (Python → C#)

| Python | C# | Fase | Status |
|--------|-----|------|--------|
| `tracker.py` → `ProductivityTracker` | `TrackingEngine` + `ActivityRepository` | 0–1 | ✅ |
| `tracker.py` → Win32 capture | `Win32ActiveWindowProvider` | 0 | ✅ |
| `tracker.py` → settings JSON | `SettingsStore` | 0 | ✅ |
| `app_paths.py` | `AppPaths` (walk up até `TimeTracker.sln`) | 0 | ✅ |
| `main.py` → tray | `TrayApplicationContext` | 0 | ✅ |
| `main.py` → startup shortcut | `StartupShortcutService` | 0 | ✅ |
| `main.py` → spawn Streamlit | — (manual na Fase 1; integrado na Fase 3) | 3 | ⬜ |
| `main.py` → shutdown handler | — | 1 | ⬜ |
| `dashboard/data.py` | `ActivityQueryService` (a criar) | 2 | ⬜ |
| `dashboard/charts.py` | Chart.js em `wwwroot/js/` | 2 | ⬜ |
| `dashboard/utils.py` | JS ou helpers C# na API | 2 | ⬜ |
| `dashboard/filters.py` | JS + `/api/dates` | 2 | ⬜ |
| `dashboard/overview.py` | `wwwroot` — Visão Geral | 2 | ⬜ |
| `dashboard/details.py` | `wwwroot` — Detalhes | 2 | ⬜ |
| `dashboard/settings.py` | `wwwroot` — Personalizar | 2 | ⬜ |

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

## Como trabalhar durante a migração

### Regra de ouro

> **Nunca rode dois trackers simultaneamente** (`run-tracker.bat` + `python main.py` / `python tracker.py`).

Ambos gravam no mesmo `productivity.db` — concorrência WAL tolera leitura, mas dois writers de tracking corrompem a lógica de sessão.

### Fluxos de dev

| Objetivo | Comando |
|----------|---------|
| Tracker C# + dashboard Streamlit (Fase 1) | `run-tracker.bat` + `streamlit run dashboard/app.py` |
| App Python completa (legado) | `run.bat` ou `python main.py` |
| Dashboard .NET (esqueleto) | `run-dashboard.bat` |
| Build .NET | `dotnet build TimeTracker.sln` |
| Publish tracker | `dotnet publish src/TimeTracker.Tracker -c Release` |

### Onde ficam os dados

Em desenvolvimento, `AppPaths` sobe diretórios até encontrar `TimeTracker.sln` ou `main.py` → grava na **raiz do repo**.

Em produção (exe publicado), grava na **pasta do executável**.

---

## Checklist de validação — Fase 1

Marcar conforme for testando.

### Tracker C#

- [ ] `run-tracker.bat` — ícone aparece na bandeja
- [ ] Trocar janela gera registros em `productivity.db`
- [ ] Sessões < 1s não são gravadas
- [ ] Sessão cruzando hora cheia gera múltiplos registros
- [ ] Janela protegida registra `System/Protected`
- [ ] "Abrir Dashboard" abre `http://localhost:8501`
- [ ] "Sair" encerra tracker e faz flush da sessão
- [ ] Atalho Startup aponta para exe C# correto
- [ ] Streamlit lê dados gravados pelo tracker C# sem erro

### Regressão Python (legado)

- [ ] `run.bat` continua funcional até Fase 3
- [ ] Dashboard Streamlit inalterado para o usuário

---

## Decisões registradas

| Data | Decisão | Motivo |
|------|---------|--------|
| 2026-08-28 | C# em vez de C++ para tracker | Produtividade, integração Windows nativa, SQLite first-class |
| 2026-08-28 | ASP.NET Core em vez de FastAPI para dashboard | Consolidar em um runtime; mesmo ecossistema do tracker |
| 2026-08-28 | Chart.js vanilla (sem React/Vue) | Dashboard local simples; evita toolchain frontend pesada |
| 2026-08-28 | Monorepo (não repo novo) | Histórico, spec, convivência incremental |
| 2026-08-28 | Manter Python na raiz até Fase 3 | Não quebrar fluxo existente durante transição |
| 2026-08-28 | Porta 8501 reservada ao Streamlit na Fase 1 | Dashboard .NET usa porta dev (`5205`) até Fase 3 |

---

## Próximos passos (ordem sugerida)

1. **Validar Fase 1** — checklist manual tracker C# vs Python
2. **Handler shutdown Windows** no Tracker (`SetConsoleCtrlHandler` equivalente)
3. **`ActivityQueryService`** — portar lógica de `dashboard/data.py`
4. **API `/api/activity` e `/api/dates`** completas
5. **UI Visão Geral** — primeiro gráfico Chart.js (donut top 5)
6. Detalhes + Personalizar → integração tracker/dashboard → limpeza Python

---

## Manutenção deste documento

**Atualizar sempre que:**

- Concluir um item de fase (marcar ✅)
- Tomar decisão arquitetural nova (tabela de decisões)
- Mudar contrato de dados, portas ou entry points
- Remover código Python (Fase 3)

**Também atualizar:** `SKILL.md` (changelog), `reference.md` (API/checklists), `README.md` (instruções de uso).
