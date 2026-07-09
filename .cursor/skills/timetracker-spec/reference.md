# TimeTracker — Referência Técnica

## Constantes globais

| Constante | Arquivo | Valor |
|-----------|---------|-------|
| `DB_NAME` | `tracker.py` | `"productivity.db"` |
| `SETTINGS_FILE` | `tracker.py` | `"app_settings.json"` |
| `DASHBOARD_PORT` | `main.py` | `8501` |
| `DASHBOARD_HOST` | `main.py` | `"localhost"` |
| `APP_NAME` | `main.py` | `"TimeTracker Pro"` |
| `poll_interval` | `tracker.run()` | `5.0` (segundos) |

## API — `ProductivityTracker`

| Método | Descrição |
|--------|-----------|
| `get_active_window_info()` | Retorna `(app_name, window_title)` ou `(None, None)` |
| `save_activity(app, title, start, end)` | Persiste com partição horária e filtro ≥1s |
| `get_all_apps()` | `DISTINCT app_name` ordenado |
| `get_app_settings()` | Dict `{app_name: {display_name, hex_color, category}}` |
| `update_app_setting(app, display, color, category)` | Upsert no JSON; retorna `bool` |
| `run(stop_event, poll_interval)` | Loop principal |

## API — Dashboard

### `dashboard_data.load_activity_data(tracker) → DataFrame`

- Lê `SELECT * FROM activity_log`.
- Enriquece com settings; default `category = "Sem Categoria"`.
- Deriva `date` e `hour` de `start_time`.
- Em erro: `st.error()` + DataFrame vazio.

### `dashboard_filters.render_date_filter(available_dates) → (date, has_data)`

- `available_dates`: lista ordenada decrescente.
- Retorna data selecionada e flag se há registros nessa data.

### `dashboard_charts` — assinaturas

| Função | Retorno |
|--------|---------|
| `create_top_apps_donut(df, color_map)` | Pie chart top 5 apps |
| `create_hourly_timeline(df, color_map, height=None)` | Bar chart empilhado por hora |
| `create_app_ranking(df, color_map, limit)` | `(fig, app_usage_all)` horizontal bar |
| `create_category_pie(df)` | Pie por categoria |
| `create_window_titles_chart(title_usage_df)` | Bar horizontal de títulos |

### `dashboard_utils`

| Função | Comportamento |
|--------|---------------|
| `format_duration_clean(seconds)` | `"Xh Ym"` ou `"Ym"`; NaN → `"0m"` |
| `clean_window_title(title)` | Remove sufixos de navegador; vazio → `"Sem Título"` |
| `build_color_map(df)` | `{display_name: hex_color}` de settings |

## `AppOrchestrator` — ciclo de vida

1. `create_startup_script()` — escreve `.vbs` em `%APPDATA%/.../Startup/`
2. Inicia thread daemon do tracker
3. `Popen` Streamlit headless
4. `pystray.Icon.run()` bloqueia thread principal
5. `cleanup()` — set stop_event, terminate/kill Streamlit, stop icon

**Shutdown Windows:** handler `CTRL_SHUTDOWN/LOGOFF/CLOSE` chama `cleanup()`.

## Startup Windows (VBS)

- Path: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\TimeTracker Pro.vbs`
- Usa `pythonw.exe` quando disponível (execução sem console).
- Remove legados `.bat` e `.lnk` com mesmo prefixo.

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

## Pontos de extensão futuros (não implementados)

Registrar aqui quando planejado ou implementado:

- Filtro por categoria na sidebar
- Exportação CSV/JSON
- Metas diárias / alertas
- Suporte multi-monitor ou Linux/macOS
- API REST para dados externos
- Testes automatizados
