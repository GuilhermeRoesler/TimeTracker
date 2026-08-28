---
name: timetracker-spec
description: Especificações vivas do TimeTracker Pro — monitor de produtividade Windows com tracker em segundo plano, SQLite e dashboard Streamlit. Use ao implementar features, corrigir bugs, refatorar ou revisar qualquer código deste repositório.
---

# TimeTracker Pro — Especificações Vivas

Documento de referência para agentes e desenvolvedores. **Atualize este skill sempre que alterar arquitetura, contratos de dados ou comportamento público.**

## Visão geral

| Item | Valor |
|------|-------|
| Nome | TimeTracker Pro |
| Plataforma | **Windows apenas** (`pywin32`, system tray, startup `.lnk`) |
| Linguagem | Python 3.8+ |
| Entry point | `main.py` |
| Dashboard | Streamlit em `http://localhost:8501` |
| Dados locais | `productivity.db` (SQLite WAL) + `app_settings.json` |

**Propósito:** monitorar a janela ativa do Windows, registrar tempo por app/título, e exibir análises em dashboard web com personalização de apps.

## Arquitetura

```
main.py (AppOrchestrator)
├── tracker.py (thread daemon)     → captura janela, grava SQLite
├── dashboard/app.py (subprocess)  → Streamlit na porta 8501
└── pystray (thread principal)     → bandeja: Abrir Dashboard / Sair
```

### Responsabilidades por módulo

| Módulo | Responsabilidade |
|--------|------------------|
| `main.py` | Orquestração, shutdown graceful, startup Windows, ícone bandeja; modo frozen PyInstaller |
| `app_paths.py` | `get_app_dir` / `get_resource_path` (código-fonte ou exe) |
| `tracker.py` | Captura `app_name` + `window_title`, loop de polling, CRUD settings |
| `dashboard/app.py` | Entry Streamlit, tabs, wiring de filtros e dados |
| `dashboard/data.py` | Query SQLite → DataFrame com colunas derivadas |
| `dashboard/filters.py` | Filtro de data (atalhos, navegação, calendário) |
| `dashboard/overview.py` | Métricas, gráficos, tabela histórica |
| `dashboard/details.py` | Drill-down por app (abas/janelas) |
| `dashboard/charts.py` | Funções Plotly puras (sem Streamlit) |
| `dashboard/utils.py` | Formatação de tempo, limpeza de títulos, color map |
| `dashboard/settings.py` | UI de personalização de apps |

**Regra de separação:** lógica de gráfico em `dashboard/charts.py`; UI Streamlit nas abas; persistência em `tracker.py`.

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
- Categorias válidas: ver `CATEGORIES` em `dashboard/settings.py`.

### DataFrame do dashboard (pós-processamento)

Colunas esperadas após `load_activity_data()`:

`app_name`, `window_title`, `start_time`, `end_time`, `duration_seconds`, `display_name`, `hex_color`, `category`, `date`, `hour`

## Comportamento do tracker

- **Polling:** `poll_interval=5.0`s (configurável em `ProductivityTracker.run`).
- **Troca de foco:** salva sessão anterior e reinicia `start_time`.
- **Shutdown:** `stop_event` (threading.Event) ou KeyboardInterrupt → flush da sessão ativa.
- **Janelas protegidas:** fallback `app_name = "System/Protected"`.

## Comportamento do dashboard

### Abas

1. **Visão Geral** — métricas, donut top 5, timeline horária, ranking paginado (+5), pizza por categoria, histórico tabular.
2. **Detalhes por App** — selectbox de apps; top 15 títulos limpos; histórico cronológico.
3. **Personalizar Apps** — busca, edição em lote, salvar apenas alterações.

### Filtros (sidebar)

- Atalhos Hoje/Ontem quando disponíveis.
- Navegação ◀/▶ entre dias **com registros** (não dias vazios do calendário).
- Botão "Atualizar Dados" → `st.rerun()`.

### Session state relevante

- `limit_apps` (default 5) — paginação do ranking.
- `selected_date` — data filtrada.
- `cfg_*` keys — formulário de settings por app.

## Convenções de código

- **Idioma da UI:** português (mensagens, labels, captions).
- **Logging:** módulo `tracker.py` usa `logging` padrão.
- **Cores:** `build_color_map()` usa `hex_color` do settings; fallback Plotly `Alphabet`.
- **Títulos de navegador:** `clean_window_title()` remove sufixos conhecidos (` - Opera`, ` - Google Chrome`, etc.).
- **Gráficos:** Plotly Express; funções retornam `fig` ou `None` se sem dados.
- **Imports:** dashboard importa `ProductivityTracker` de `tracker.py` (acesso a DB e settings).

## Restrições e decisões

| Decisão | Motivo |
|---------|--------|
| Windows-only | `win32gui`, `win32process`, startup `.lnk`, `CREATE_NO_WINDOW` |
| SQLite local | Privacidade, zero infra, concorrência via WAL |
| Streamlit headless | Subprocess sem janela de terminal |
| `pythonw.exe` no startup | Execução oculta na bandeja |
| Partição por hora | Gráficos de timeline horária precisam de granularidade correta |
| Mínimo 1s por registro | Evita ruído de trocas rápidas de foco |

## Workflows de desenvolvimento

### Adicionar novo gráfico

1. Criar função em `dashboard/charts.py` (retorna `go.Figure | None`).
2. Consumir em aba correspondente (`overview` ou `details`).
3. Usar `color_map` e `format_duration_clean` existentes.

### Adicionar campo de personalização de app

1. Estender schema em `tracker.update_app_setting()` e `_load_settings_file`.
2. Mapear coluna em `dashboard/data.load_activity_data()`.
3. Adicionar controle em `dashboard/settings.render_settings_tab()`.
4. Atualizar `app_settings.example.json`.
5. **Atualizar este skill** (seção Contratos de dados).

### Adicionar categoria

1. Incluir em `CATEGORIES` (`dashboard/settings.py`).
2. Documentar aqui se houver regra de negócio associada.

### Testar localmente

```bash
pip install -r requirements.txt
python main.py          # app completa (tracker + dashboard + bandeja)
python tracker.py       # apenas tracker (dev isolado)
streamlit run dashboard/app.py  # apenas dashboard (requer DB com dados)
```

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
| 2026-08-28 | Dashboard reorganizado em pacote `dashboard/` (`app.py`, `charts.py`, etc.) |
| 2026-08-27 | Release CI: PyInstaller onedir + workflow `.github/workflows/release.yml`; `app_paths.py` e suporte `sys.frozen` |
| 2026-07-09 | Spec inicial criada a partir do estado atual do repositório |
| 2026-07-09 | Regra `.cursor/rules/timetracker-spec.mdc` + seções roadmap, testes e deploy em `reference.md` |
| 2026-07-09 | Startup Windows: atalho `.lnk` em vez de script `.vbs` (`create_startup_shortcut`) |

## Recursos adicionais

- Detalhes de API, **roadmap**, **testes** e **deploy**: [reference.md](reference.md)
- README do usuário: [README.md](../../README.md)
- Regra Cursor que aponta para este spec: [.cursor/rules/timetracker-spec.mdc](../../rules/timetracker-spec.mdc)
