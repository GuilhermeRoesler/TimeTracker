# TimeTracker — Referência Técnica

## Constantes globais

| Constante | Arquivo | Valor |
|-----------|---------|-------|
| `DB_NAME` | `tracker.py` | `"productivity.db"` |
| `SETTINGS_FILE` | `tracker.py` | `"app_settings.json"` |
| `DASHBOARD_PORT` | `main.py` | `8501` |
| `DASHBOARD_HOST` | `main.py` | `"localhost"` |
| `APP_NAME` | `main.py` | `"TimeTracker Pro"` |
| `poll_interval` | `tracker.run()` / `TrackingEngine` | `5.0` (segundos) |

### C# (`TimeTracker.Core`)

| Constante | Classe | Valor |
|-----------|--------|-------|
| `DbFileName` | `AppConstants` | `"productivity.db"` |
| `SettingsFileName` | `AppConstants` | `"app_settings.json"` |
| `DefaultPollIntervalSeconds` | `AppConstants` | `5.0` |
| `DashboardPort` | `AppConstants` | `8501` |

| Classe | Responsabilidade |
|--------|------------------|
| `ActivityRepository` | Init WAL, `SaveActivity`, `GetAllApps`, `GetAllActivities` |
| `SettingsStore` | Load/save JSON, `UpdateAppSetting`, batch |
| `TrackingEngine` | Loop de polling via `IActiveWindowProvider` |
| `ActivityQueryService` | Load enriquecido, dates, apps com settings |
| `ActivityTextHelper` | `FormatDurationClean`, `CleanWindowTitle` |
| `Win32ActiveWindowProvider` | Captura janela ativa (projeto Tracker) |
| `DashboardProcessService` | Sobe dashboard ASP.NET como subprocesso |

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

## API — `ProductivityTracker` *(Python legado)*

| Método | Descrição |
|--------|-----------|
| `get_active_window_info()` | Retorna `(app_name, window_title)` ou `(None, None)` |
| `save_activity(app, title, start, end)` | Persiste com partição horária e filtro ≥1s |
| `get_all_apps()` | `DISTINCT app_name` ordenado |
| `get_app_settings()` | Dict `{app_name: {display_name, hex_color, category}}` |
| `update_app_setting(app, display, color, category)` | Upsert no JSON; retorna `bool` |
| `run(stop_event, poll_interval)` | Loop principal |

## API — Dashboard Streamlit *(legado)*

### `dashboard.data.load_activity_data(tracker) → DataFrame`

- Lê `SELECT * FROM activity_log`.
- Enriquece com settings; default `category = "Sem Categoria"`.
- Deriva `date` e `hour` de `start_time`.
- Em erro: `st.error()` + DataFrame vazio.

### `dashboard.filters.render_date_filter(available_dates) → (date, has_data)`

- `available_dates`: lista ordenada decrescente.
- Retorna data selecionada e flag se há registros nessa data.

### `dashboard.charts` — assinaturas

| Função | Retorno |
|--------|---------|
| `create_top_apps_donut(df, color_map)` | Pie chart top 5 apps |
| `create_hourly_timeline(df, color_map, height=None)` | Bar chart empilhado por hora |
| `create_app_ranking(df, color_map, limit)` | `(fig, app_usage_all)` horizontal bar |
| `create_category_pie(df)` | Pie por categoria |
| `create_window_titles_chart(title_usage_df)` | Bar horizontal de títulos |

### `dashboard.utils`

| Função | Comportamento |
|--------|---------------|
| `format_duration_clean(seconds)` | `"Xh Ym"` ou `"Ym"`; NaN → `"0m"` |
| `clean_window_title(title)` | Remove sufixos de navegador; vazio → `"Sem Título"` |
| `build_color_map(df)` | `{display_name: hex_color}` de settings |

## `AppOrchestrator` — ciclo de vida

1. `create_startup_shortcut()` — cria `.lnk` em `%APPDATA%/.../Startup/`
2. Inicia thread daemon do tracker
3. `Popen` Streamlit headless
4. `pystray.Icon.run()` bloqueia thread principal
5. `cleanup()` — set stop_event, terminate/kill Streamlit, stop icon

**Shutdown Windows:** handler `CTRL_SHUTDOWN/LOGOFF/CLOSE` chama `cleanup()`.

## Startup Windows (atalho)

- Path: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\TimeTracker Pro.lnk`
- Destino: `pythonw.exe` quando disponível (execução sem console).
- Argumentos: caminho absoluto de `main.py`; diretório de trabalho = pasta do projeto.
- Remove legados `.vbs` e `.bat` com mesmo prefixo.

## Dependências (`requirements.txt`)

```
streamlit>=1.56.0
pandas>=3.0.3
pywin32>=311
plotly>=6.7.0
pystray>=0.19.5
Pillow>=11.1.0
```

## Arquivos não versionados

- `productivity.db` — dados de atividade
- `app_settings.json` — personalizações do usuário
- `venv/`, `__pycache__/`

## Roadmap

### Migração de stack (prioridade atual)

Documento completo: [MIGRATION.md](MIGRATION.md)

| Fase | Objetivo | Status |
|------|----------|--------|
| 0 | Solution .NET, Core, tracker scaffold, dashboard esqueleto | ✅ |
| 1 | Tracker C# no uso diário | ✅ |
| 2 | Dashboard ASP.NET + Chart.js com paridade Streamlit | ✅ |
| 3 | Integração única, CI .NET, remoção Python | 🔶 |

### Features de produto (pós-migração ou paralelo)

Prioridade sugerida. Mover para "Implementado" ao concluir e atualizar o changelog do spec.

| Prioridade | Feature | Notas |
|------------|---------|-------|
| Alta | Filtro por categoria na sidebar | Reutiliza coluna `category` já existente no DataFrame |
| Alta | Exportação CSV/JSON | Dados do dia filtrado ou intervalo customizado |
| Média | Metas diárias / alertas | Requer novo schema ou tabela de metas |
| Média | Detecção de idle (AFK) | Pausar tracking quando sem input por N minutos |
| Baixa | Suporte Linux/macOS | Fora do escopo Windows-only atual |
| Baixa | WebView2 para dashboard | Janela nativa sem browser externo |

### Implementado

- Empacotamento Windows com PyInstaller (`timetracker.spec`) + release via GitHub Actions
- Scaffold migração .NET: `TimeTracker.sln`, Core, Tracker, Dashboard esqueleto (Fase 0)

## Testes

**Estado atual:** sem suite automatizada. Testes manuais são o fluxo principal. Checklists de migração em [MIGRATION.md](MIGRATION.md).

### Checklist manual — tracker C# (Fase 1)

Ver checklist completo em [MIGRATION.md](MIGRATION.md).

- [ ] `run-tracker.bat` grava em `productivity.db` ao trocar de janela
- [ ] Streamlit lê dados gravados pelo tracker C#
- [ ] Sessões < 1s não aparecem no banco
- [ ] Sessão que cruza hora cheia gera múltiplos registros
- [ ] "Sair" na bandeja faz flush da sessão ativa

### Checklist manual — tracker Python (legado)
- [ ] Sessões < 1s não aparecem no banco
- [ ] Sessão que cruza hora cheia gera múltiplos registros
- [ ] `stop_event` faz flush da sessão ativa ao encerrar

### Checklist manual — dashboard

- [ ] Dashboard carrega com DB populado; aviso quando vazio
- [ ] Filtro de data: atalhos, ◀/▶, calendário
- [ ] Gráficos renderizam sem erro com e sem `hex_color` configurado
- [ ] Aba Detalhes: títulos limpos (`clean_window_title`)
- [ ] Personalizar Apps: salvar persiste em `app_settings.json`

### Checklist manual — app completa

- [ ] `python main.py` inicia tracker + Streamlit + ícone na bandeja
- [ ] "Abrir Dashboard" abre `http://localhost:8501`
- [ ] "Sair" encerra tracker e subprocess Streamlit
- [ ] Atalho `.lnk` criado em Startup com `pythonw.exe`

### Diretrizes para testes automatizados (futuro)

- **Framework sugerido:** `pytest`
- **Priorizar:** funções puras em `dashboard/utils.py` e `dashboard/charts.py` (sem Streamlit/win32)
- **Tracker:** mockar `win32gui`/`win32process`; testar `save_activity` com timestamps controlados
- **Integração:** DB em memória (`:memory:`) para queries de `dashboard/data`
- **Evitar:** testes que dependam de janela ativa real ou bandeja do sistema

## Deploy e distribuição

**Estado atual:** execução via código-fonte + `pip install -r requirements.txt`.

### Uso pessoal (atual)

```bash
pip install -r requirements.txt
python main.py
```

O startup automático é gerenciado por `main.py` via atalho `.lnk` na pasta Startup.

### Deploy manual em outra máquina

1. Copiar pasta do projeto (sem `venv/`, `productivity.db`, `app_settings.json`)
2. Instalar Python 3.8+ no Windows (marcar “Add to PATH”)
3. Duplo clique em `run.bat` — cria `venv`, instala deps e inicia o app

Alternativa manual: `pip install -r requirements.txt` e `python main.py` (registra startup na primeira execução).

### Empacotamento Windows (PyInstaller)

Build local:

```bash
pip install -r requirements-build.txt
pyinstaller timetracker.spec --noconfirm --clean
```

Saída: pasta `dist/TimeTrackerPro/` com `TimeTrackerPro.exe` (modo onedir).

Contratos do pacote:

- `app_paths.get_app_dir()` — pasta do `.exe` (gravável); dados do usuário ficam ao lado do exe
- `productivity.db` e `app_settings.json` **fora** do bundle (criados em runtime)
- Dashboard: entry `dashboard/app.py`; exe reinicia a si mesmo com `--timetracker-streamlit` (subprocess Streamlit)
- Startup `.lnk` aponta para o `.exe` quando `sys.frozen`
- Spec: `timetracker.spec`; hooks em `hooks/`; config em `.streamlit/config.toml`

### CI — GitHub Actions Releases

Workflow: `.github/workflows/release.yml`

| Trigger | Comportamento |
|---------|---------------|
| Push de tag `v*` (ex.: `v1.0.0`) | Build Windows + cria/atualiza GitHub Release com o zip |
| `workflow_dispatch` | Só gera artifact (sem Release) |

Artefato: `TimeTrackerPro-<tag>-windows-amd64.zip` (conteúdo de `dist/TimeTrackerPro/`).

```bash
git tag v1.0.0
git push origin v1.0.0
```

### Atualização de versão

- Tag git semântica (`v1.0.0`) dispara o release automático
- Preferir changelog nas notes geradas pelo Actions / README
- Migrar schema SQLite com script de migração se necessário
