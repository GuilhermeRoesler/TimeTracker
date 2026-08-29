---
name: timetracker-spec
description: Especificações vivas do TimeTracker Pro — monitor de produtividade Windows. Stack C#/.NET 8 (tracker + ASP.NET Core + Chart.js). Use ao implementar features, corrigir bugs, refatorar ou revisar qualquer código deste repositório.
---

# TimeTracker Pro — Especificações Vivas

Documento de referência para agentes e desenvolvedores. **Atualize este skill sempre que alterar arquitetura, contratos de dados ou comportamento público.**

## Visão geral

| Item | Valor |
|------|-------|
| Nome | TimeTracker Pro |
| Plataforma | **Windows apenas** |
| Stack | C# (.NET 8) tracker + ASP.NET Core + HTML/Chart.js |
| Entry point | `run.bat` → `src/TimeTracker.Tracker` |
| Dashboard | ASP.NET + Chart.js em `http://localhost:8501` |
| Dados locais | Dev: raiz do repo · Instalado: `%LocalAppData%\TimeTracker Pro\` |
| Ícone | `assets/app.ico` (fonte `app-icon.png`) — exe, bandeja, favicon, Setup |

**Propósito:** monitorar a janela ativa do Windows, registrar tempo por app/título, e exibir análises em dashboard web com personalização de apps.

> Stack C#/.NET 8 estável. Spec vivo: este skill + [reference.md](reference.md).

## Arquitetura

```
TimeTracker.sln
├── src/TimeTracker.Core/       → SQLite, settings JSON, TrackingEngine
├── src/TimeTracker.Tracker/  → Win32, bandeja, startup, Kestrel in-process + WebView2
└── src/TimeTracker.Dashboard/ → API endpoints + wwwroot (Chart.js); embutido no Tracker
```

### Responsabilidades por módulo

| Módulo | Responsabilidade |
|--------|------------------|
| `src/TimeTracker.Core` | Contratos de dados, `ActivityRepository`, `SettingsStore`, `TrackingEngine`, `ActivityQueryService` |
| `src/TimeTracker.Tracker` | Win32, bandeja WinForms, startup `.lnk`, worker, Kestrel in-process, auto-update GitHub |
| `src/TimeTracker.Dashboard` | `DashboardWeb` (Minimal API) + `wwwroot` Chart.js; isolável via `dotnet run` em dev |

## Contratos de dados

### SQLite — `activity_log`

```sql
id INTEGER PK AUTOINCREMENT
app_name TEXT NOT NULL          -- nome do executável (ex: chrome.exe)
window_title TEXT               -- título da janela ativa
start_time TIMESTAMP NOT NULL
end_time TIMESTAMP
duration_seconds REAL
```

- Modo WAL habilitado na inicialização.
- Registros com duração < 1s são descartados.
- Sessões que cruzam hora cheia são **particionadas** em múltiplos registros.

### JSON — `app_settings.json`

```json
{
  "apps": {
    "<app_name>": {
      "display_name": "string",
      "hex_color": "#RRGGBB ou null",
      "category": "string ou null"
    }
  }
}
```

- Chave: `app_name` exato do executável (case-sensitive).
- Não versionado (`.gitignore`). Exemplo em `app_settings.example.json`.
- Categorias válidas: ver `AppCategories` em `TimeTracker.Core`.

### Modelo enriquecido (API/dashboard)

Campos expostos por `ActivityQueryService`:

`app_name`, `window_title`, `start_time`, `end_time`, `duration_seconds`, `display_name`, `hex_color`, `category`, `date`, `hour`

## Comportamento do tracker

- **Polling:** `poll_interval=5.0`s (configurável em `TrackingEngine`).
- **Troca de foco:** salva sessão anterior e reinicia `start_time`.
- **Shutdown:** `CancellationToken` ou `SystemEvents.SessionEnding` → flush da sessão ativa.
- **Janelas protegidas:** fallback `app_name = "System/Protected"`.
- **Dashboard:** Kestrel in-process no Tracker (`DashboardWeb`). UI WebView2 com User Data Folder em `AppPaths.GetDataDir()/WebView2` (fallback: browser se a inicialização falhar). Em dev, `dotnet run --project src/TimeTracker.Dashboard` sobe só a API.

## Comportamento do dashboard

### Visual (UI)

- Tema **claro** corporativo; tipografia Plus Jakarta Sans + JetBrains Mono (durações/tabelas).
- Layout centralizado sem sidebar (`max-width` ~960px, margens laterais amplas).
- Controles de data no cabeçalho do painel; botão Atualizar no canto superior direito.
- Abas underline (sem emojis); métricas com acento teal; painéis leves com blur.
- Gráficos Chart.js alinhados ao tema (tooltips escuros, grades suaves, paleta sóbria).

### Abas

1. **Visão Geral** — métricas, donut top 5, timeline horária, ranking paginado (+5), pizza por categoria, histórico tabular.
2. **Detalhes por App** — select de apps; top 15 títulos limpos; histórico cronológico.
3. **Personalizar Apps** — busca, edição em lote, salvar apenas alterações.

### Filtros (cabeçalho)

- Atalhos Hoje/Ontem quando disponíveis (estado ativo no dia selecionado).
- Navegação ◀/▶ entre dias **com registros** (não dias vazios do calendário).
- Botão "Atualizar" no topo do painel recarrega via API.
- Cabeçalho principal mostra a data selecionada por extenso (pt-BR).

## Convenções de código

- **Idioma da UI:** português (mensagens, labels, captions).
- **Logging:** `ILogger` via Microsoft.Extensions.Logging.
- **Cores:** `hex_color` do settings; fallback paleta sóbria em `utils.js` (`CHART_PALETTE`).
- **Títulos de navegador:** `ActivityTextHelper.CleanWindowTitle()` remove sufixos conhecidos.
- **Gráficos:** Chart.js em `wwwroot/js/charts.js`.

## Restrições e decisões

| Decisão | Motivo |
|---------|--------|
| Windows-only | Win32, NotifyIcon, startup `.lnk` |
| SQLite local | Privacidade, zero infra, concorrência via WAL |
| ASP.NET Core + Chart.js vanilla | Runtime único .NET; frontend leve |
| Partição por hora | Gráficos de timeline horária precisam de granularidade correta |
| Mínimo 1s por registro | Evita ruído de trocas rápidas de foco |
| Processo único (Kestrel in-process) | Um exe na bandeja; porta 8501 mantida por compatibilidade |

## Workflows de desenvolvimento

### Adicionar novo gráfico

1. Criar função em `wwwroot/js/charts.js`.
2. Consumir em aba correspondente em `wwwroot/js/app.js`.
3. Usar `buildColorMap` e `formatDurationClean` de `utils.js`.

### Adicionar campo de personalização de app

1. Estender schema em `SettingsStore.UpdateAppSetting()`.
2. Mapear em `ActivityQueryService`.
3. Adicionar controle em `wwwroot/js/app.js` (aba Personalizar).
4. Atualizar `app_settings.example.json`.
5. **Atualizar este skill** (seção Contratos de dados).

### Adicionar categoria

1. Incluir em `AppCategories` (`TimeTracker.Core`).
2. Documentar aqui se houver regra de negócio associada.

### Testar localmente

```bash
build.bat                                  # rebuild Debug (wwwroot fresco → bin/)
run.bat                                        # app completa (duplo clique)
dotnet run --project src/TimeTracker.Tracker   # equivalente
dotnet run --project src/TimeTracker.Dashboard # API isolada (dev)
dotnet build TimeTracker.sln
```

> Após editar `wwwroot`, use `build.bat` e reinicie o Tracker. O `run.bat` (`dotnet run` incremental) pode não recopiar CSS/JS.

## Manutenção deste documento vivo

**Obrigatório atualizar o skill quando:**

- [ ] Novo módulo ou responsabilidade alterada
- [ ] Schema SQLite ou formato JSON modificado
- [ ] Nova aba, filtro ou fluxo de UI
- [ ] Mudança de porta, paths ou dependências críticas
- [ ] Nova restrição de plataforma ou decisão arquitetural

**Ao atualizar:** editar `SKILL.md` (essencial) e `reference.md` (detalhes). Registrar mudança na tabela de changelog abaixo.

### Changelog do spec

| Data | Mudança |
|------|---------|
| 2026-08-28 | Shutdown: Kestrel para após `Application.Run` (não no Dispose da bandeja); `run.bat` usa `--no-hot-reload` |
| 2026-08-28 | `build.bat`: rebuild Debug com `--no-incremental` para forçar cópia fresca do `wwwroot` |
| 2026-08-28 | UI: removida sidebar/marca; layout centralizado com margens amplas; data e Atualizar no cabeçalho do painel |
| 2026-08-28 | UI do dashboard: redesign executivo (tema claro, Plus Jakarta Sans, tabs underline, gráficos refinados); removidos emojis da interface |
| 2026-08-28 | Removidos `run-tracker.bat` / `run-dashboard.bat` (duplicata e launcher legado); entry point só `run.bat` |
| 2026-08-28 | WebView2: User Data Folder em `%LocalAppData%` (evita falha/fallback ao instalar em Program Files) |
| 2026-08-28 | Auto-update: verifica GitHub Releases, notifica na bandeja e instala Setup |
| 2026-08-28 | Removido `MIGRATION.md` (migração Python→C# concluída; histórico no changelog) |
| 2026-08-28 | Ícone do produto (`assets/app.ico`): exe, bandeja, WebView2, favicon, Setup Inno; CI verifica presença |
| 2026-08-28 | Processo único: Tracker hospeda Kestrel/dashboard in-process; exe publicado `TimeTracker.exe` |
| 2026-08-28 | Instalador Inno Setup + publish framework-dependent; dados em `%LocalAppData%\TimeTracker Pro` |
| 2026-08-28 | WebView2: janela nativa do dashboard; testes xUnit (`TimeTracker.Core.Tests`); CI build+test |
| 2026-08-28 | Fase 3: remoção Python legado, CI `dotnet publish`, entry point único C#, handler shutdown Windows |
| 2026-08-28 | Fase 2: dashboard ASP.NET + Chart.js (paridade Streamlit), `ActivityQueryService`, API REST |
| 2026-08-28 | Migração Fase 0–3: solution .NET, Core, Tracker, Dashboard, limpeza Python |
| 2026-08-28 | `run.bat` Windows: launch por duplo clique (C#) |
| 2026-08-27 | Release CI: PyInstaller — *substituído por dotnet publish / Inno Setup* |
| 2026-07-09 | Spec inicial criada a partir do estado atual do repositório |
| 2026-07-09 | Regra `.cursor/rules/timetracker-spec.mdc` + seções roadmap, testes e deploy em `reference.md` |
| 2026-07-09 | Startup Windows: atalho `.lnk` em vez de script `.vbs` |

## Recursos adicionais

- Detalhes de API, **roadmap**, **testes** e **deploy**: [reference.md](reference.md)
- README do usuário: [README.md](../../README.md)
- Regra Cursor que aponta para este spec: [.cursor/rules/timetracker-spec.mdc](../../rules/timetracker-spec.mdc)
