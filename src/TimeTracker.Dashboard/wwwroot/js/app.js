const state = {
  availableDates: [],
  selectedDate: null,
  records: [],
  summary: null,
  hasData: false,
  limitApps: 5,
  categories: [],
  apps: [],
  activeTab: "overview",
  settingsSearch: "",
};

const els = {
  emptyState: document.getElementById("empty-state"),
  appLayout: document.getElementById("app-layout"),
  dateShortcuts: document.getElementById("date-shortcuts"),
  datePicker: document.getElementById("date-picker"),
  btnPrev: document.getElementById("btn-prev-day"),
  btnNext: document.getElementById("btn-next-day"),
  dateWarning: document.getElementById("date-warning"),
  btnRefresh: document.getElementById("btn-refresh"),
  panelOverview: document.getElementById("panel-overview"),
  panelDetails: document.getElementById("panel-details"),
  panelSettings: document.getElementById("panel-settings"),
  tabs: document.querySelectorAll(".tab"),
};

document.addEventListener("DOMContentLoaded", init);

async function init() {
  bindEvents();
  await reloadAll();
}

function bindEvents() {
  els.btnRefresh.addEventListener("click", () => reloadAll());
  els.btnPrev.addEventListener("click", () => navigateDate(1));
  els.btnNext.addEventListener("click", () => navigateDate(-1));
  els.datePicker.addEventListener("change", () => {
    state.selectedDate = els.datePicker.value;
    loadActivityForSelectedDate();
  });

  els.tabs.forEach((tab) => {
    tab.addEventListener("click", () => switchTab(tab.dataset.tab));
  });
}

async function reloadAll() {
  try {
    const [dates, meta] = await Promise.all([fetchDates(), fetchMeta()]);
    state.availableDates = dates;
    state.categories = meta.categories || [];

    if (!dates.length) {
      showEmptyState();
      return;
    }

    if (!state.selectedDate || !dates.includes(state.selectedDate)) {
      state.selectedDate = dates[0];
    }

    showAppLayout();
    updateDateControls();
    await loadActivityForSelectedDate();

    if (state.activeTab === "settings") {
      await loadSettingsPanel();
    }
  } catch (error) {
    console.error(error);
    showEmptyState("Erro ao carregar dados. Verifique se o dashboard está rodando.");
  }
}

async function loadActivityForSelectedDate() {
  if (!state.selectedDate) return;

  try {
    const payload = await fetchActivity(state.selectedDate);
    state.records = payload.records || [];
    state.summary = payload.summary || null;
    state.hasData = payload.hasData;
    updateDateControls();
    renderActiveTab();
  } catch (error) {
    console.error(error);
  }
}

function showEmptyState(message) {
  els.emptyState.classList.remove("hidden");
  els.appLayout.classList.add("hidden");
  if (message) {
    els.emptyState.querySelector("p").textContent = message;
  }
}

function showAppLayout() {
  els.emptyState.classList.add("hidden");
  els.appLayout.classList.remove("hidden");
}

function updateDateControls() {
  const dates = state.availableDates;
  if (!dates.length) return;

  const minDate = dates[dates.length - 1];
  const maxDate = dates[0];
  els.datePicker.min = minDate;
  els.datePicker.max = maxDate;
  els.datePicker.value = state.selectedDate || maxDate;

  const currentIndex = dates.indexOf(state.selectedDate);
  const inList = currentIndex >= 0;
  els.btnPrev.disabled = !inList || currentIndex >= dates.length - 1;
  els.btnNext.disabled = !inList || currentIndex <= 0;

  els.dateWarning.classList.toggle("hidden", state.hasData);

  els.dateShortcuts.innerHTML = "";
  const shortcuts = [];
  if (dates.includes(todayIso())) shortcuts.push(["Hoje", todayIso()]);
  if (dates.includes(yesterdayIso())) shortcuts.push(["Ontem", yesterdayIso()]);

  for (const [label, value] of shortcuts) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "btn-secondary";
    button.textContent = label;
    button.addEventListener("click", () => {
      state.selectedDate = value;
      loadActivityForSelectedDate();
    });
    els.dateShortcuts.appendChild(button);
  }
}

function navigateDate(direction) {
  const index = state.availableDates.indexOf(state.selectedDate);
  if (index < 0) return;
  const nextIndex = index + direction;
  if (nextIndex < 0 || nextIndex >= state.availableDates.length) return;
  state.selectedDate = state.availableDates[nextIndex];
  loadActivityForSelectedDate();
}

function switchTab(tabName) {
  state.activeTab = tabName;
  els.tabs.forEach((tab) => tab.classList.toggle("active", tab.dataset.tab === tabName));
  els.panelOverview.classList.toggle("hidden", tabName !== "overview");
  els.panelDetails.classList.toggle("hidden", tabName !== "details");
  els.panelSettings.classList.toggle("hidden", tabName !== "settings");
  renderActiveTab();
}

function renderActiveTab() {
  if (state.activeTab === "overview") renderOverview();
  if (state.activeTab === "details") renderDetails();
  if (state.activeTab === "settings") loadSettingsPanel();
}

function renderOverview() {
  const records = state.records;
  const colorMap = buildColorMap(records);
  const summary = state.summary || { totalSeconds: 0, sessionCount: 0, topApp: null };
  const hours = Math.floor(summary.totalSeconds / 3600);
  const minutes = Math.floor((summary.totalSeconds % 3600) / 60);

  els.panelOverview.innerHTML = `
    <div class="metrics">
      <div class="metric-card"><div class="metric-label">Tempo Total</div><div class="metric-value">${hours}h ${minutes}m</div></div>
      <div class="metric-card"><div class="metric-label">Sessões (Focos)</div><div class="metric-value">${summary.sessionCount}</div></div>
      <div class="metric-card"><div class="metric-label">App Mais Usado</div><div class="metric-value">${summary.topApp || "—"}</div></div>
    </div>
    <div class="grid-2">
      <div class="card"><h3>Distribuição (Top 5)</h3><div class="chart-box"><canvas id="chart-donut"></canvas></div></div>
      <div class="card"><h3>Linha do Tempo</h3><div class="chart-box"><canvas id="chart-hourly"></canvas></div></div>
    </div>
    <div class="grid-2">
      <div class="card">
        <h3>Ranking Detalhado</h3>
        <div class="chart-box"><canvas id="chart-ranking"></canvas></div>
        <div id="ranking-more"></div>
      </div>
      <div class="card"><h3>Categorias</h3><div class="chart-box"><canvas id="chart-category"></canvas></div></div>
    </div>
    <div class="card">
      <h3>Linha do Tempo</h3>
      <div class="chart-box tall"><canvas id="chart-hourly-full"></canvas></div>
    </div>
    <h3 class="section-title">Histórico Detalhado</h3>
    <div class="card table-wrap">${renderHistoryTable(records)}</div>
  `;

  if (!records.length) {
    els.panelOverview.querySelectorAll(".chart-box").forEach((box) => {
      box.innerHTML = '<p class="info-box">Sem dados.</p>';
    });
    return;
  }

  const donutOk = renderDonut(document.getElementById("chart-donut"), records, colorMap);
  if (!donutOk) document.getElementById("chart-donut").parentElement.innerHTML = '<p class="info-box">Sem dados.</p>';

  const hourlyOk = renderHourlyTimeline(document.getElementById("chart-hourly"), records, colorMap);
  if (!hourlyOk) document.getElementById("chart-hourly").parentElement.innerHTML = '<p class="info-box">Sem atividades.</p>';

  const ranking = renderRanking(document.getElementById("chart-ranking"), records, colorMap, state.limitApps);
  const moreContainer = document.getElementById("ranking-more");
  if (!ranking.rendered) {
    document.getElementById("chart-ranking").parentElement.innerHTML = '<p class="info-box">Sem dados.</p>';
  } else if (ranking.totalApps > state.limitApps) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "btn-more";
    btn.textContent = "➕ Mostrar Mais 5";
    btn.addEventListener("click", () => {
      state.limitApps += 5;
      renderOverview();
    });
    moreContainer.appendChild(btn);
  }

  const catOk = renderCategoryPie(document.getElementById("chart-category"), records);
  if (!catOk) document.getElementById("chart-category").parentElement.innerHTML = '<p class="info-box">Sem dados de categoria.</p>';

  renderHourlyTimeline(document.getElementById("chart-hourly-full"), records, colorMap, true);
}

function renderHistoryTable(records) {
  if (!records.length) return '<p class="info-box">Sem registros.</p>';
  const rows = [...records]
    .sort((a, b) => new Date(b.startTime) - new Date(a.startTime))
    .map((record) => `
      <tr>
        <td>${formatDateTime(record.startTime)}</td>
        <td>${escapeHtml(record.displayName)}</td>
        <td>${escapeHtml(record.category)}</td>
        <td>${escapeHtml(record.windowTitle || "")}</td>
        <td>${formatDurationDetailed(record.durationSeconds)}</td>
      </tr>`)
    .join("");

  return `
    <table>
      <thead>
        <tr>
          <th>Início</th>
          <th>App</th>
          <th>Categoria</th>
          <th>Janela</th>
          <th>Duração</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>`;
}

function renderDetails() {
  const records = state.records;
  if (!records.length) {
    els.panelDetails.innerHTML = '<p class="info-box">Sem dados para o dia selecionado.</p>';
    return;
  }

  const appTotals = toSortedEntries(sumBy(records, (r) => r.displayName));
  const apps = appTotals.map(([name]) => name);
  let defaultIndex = apps.findIndex((name) => name.toLowerCase().includes("opera"));
  if (defaultIndex < 0) defaultIndex = 0;

  els.panelDetails.innerHTML = `
    <header class="section-title">🔎 O que você fez dentro de cada App?</header>
    <p class="info-box">Selecione um aplicativo para ver em quais abas ou arquivos você passou mais tempo.</p>
    <label class="field-label" for="details-app-select">Selecione o Aplicativo:</label>
    <select id="details-app-select">${apps.map((app, i) => `<option value="${escapeHtml(app)}" ${i === defaultIndex ? "selected" : ""}>${escapeHtml(app)}</option>`).join("")}</select>
    <div class="details-layout" style="margin-top: 1rem;">
      <div class="card">
        <h3 id="details-chart-title">Top Abas/Janelas</h3>
        <div class="chart-box tall"><canvas id="chart-windows"></canvas></div>
      </div>
      <div class="card">
        <h3>Histórico Cronológico</h3>
        <div id="details-history"></div>
      </div>
    </div>
  `;

  const select = document.getElementById("details-app-select");
  const renderForApp = () => {
    const appName = select.value;
    document.getElementById("details-chart-title").textContent = `Top Abas/Janelas em: ${appName}`;
    const canvas = document.getElementById("chart-windows");
    const ok = renderWindowTitles(canvas, records, appName);
    if (!ok) canvas.parentElement.innerHTML = '<p class="info-box">Sem dados detalhados.</p>';

    const appRecords = records
      .filter((r) => r.displayName === appName)
      .sort((a, b) => new Date(b.startTime) - new Date(a.startTime));

    document.getElementById("details-history").innerHTML = appRecords.length
      ? `<table><thead><tr><th>Hora</th><th>Janela</th><th>Duração</th></tr></thead><tbody>${appRecords.map((r) => `
          <tr>
            <td>${formatTime(r.startTime)}</td>
            <td>${escapeHtml(cleanWindowTitle(r.windowTitle))}</td>
            <td>${formatDurationClean(r.durationSeconds)}</td>
          </tr>`).join("")}</tbody></table>`
      : '<p class="info-box">Sem histórico.</p>';
  };

  select.addEventListener("change", renderForApp);
  renderForApp();
}

async function loadSettingsPanel() {
  try {
    state.apps = await fetchApps();
  } catch (error) {
    console.error(error);
    els.panelSettings.innerHTML = '<p class="info-box">Erro ao carregar apps.</p>';
    return;
  }

  renderSettings();
}

function renderSettings() {
  const search = state.settingsSearch.trim().toLowerCase();
  const filtered = state.apps.filter((app) => {
    if (!search) return true;
    return app.appName.toLowerCase().includes(search) ||
      (app.displayName || "").toLowerCase().includes(search);
  });

  els.panelSettings.innerHTML = `
    <header class="section-title">⚙️ Personalizar Apps</header>
    <p class="info-box">Defina nomes amigáveis, cores e categorias para cada aplicativo.</p>
    <input type="search" id="settings-search" class="settings-search" placeholder="Filtrar por nome do executável ou nome de exibição..." value="${escapeHtml(state.settingsSearch)}" />
    <p class="info-box">${filtered.length} app(s) exibido(s)</p>
    <div class="card settings-table">${filtered.length ? renderSettingsTable(filtered) : '<p class="info-box">Nenhum app corresponde à busca.</p>'}</div>
    <div class="settings-actions">
      <button type="button" id="btn-save-settings" class="btn-primary">💾 Salvar alterações</button>
      <span id="settings-status" class="status-message"></span>
    </div>
  `;

  document.getElementById("settings-search")?.addEventListener("input", (event) => {
    state.settingsSearch = event.target.value;
    renderSettings();
  });

  document.getElementById("btn-save-settings")?.addEventListener("click", saveSettings);
}

function renderSettingsTable(apps) {
  const categoryOptions = (selected) => state.categories.map((cat) =>
    `<option value="${escapeHtml(cat)}" ${cat === selected ? "selected" : ""}>${escapeHtml(cat)}</option>`,
  ).join("");

  const rows = apps.map((app) => {
    const color = app.hexColor || "#808080";
    const category = app.category || "Sem Categoria";
    return `
      <tr data-app="${escapeHtml(app.appName)}">
        <td>${escapeHtml(app.appName)}</td>
        <td><input type="text" class="setting-display" value="${escapeHtml(app.displayName || app.appName)}" /></td>
        <td><select class="setting-category">${categoryOptions(category)}</select></td>
        <td><input type="color" class="setting-color" value="${escapeHtml(color)}" /></td>
      </tr>`;
  }).join("");

  return `
    <table>
      <thead>
        <tr>
          <th>App</th>
          <th>Nome de exibição</th>
          <th>Categoria</th>
          <th>Cor</th>
        </tr>
      </thead>
      <tbody>${rows}</tbody>
    </table>`;
}

async function saveSettings() {
  const rows = els.panelSettings.querySelectorAll("tbody tr[data-app]");
  const changes = [...rows].map((row) => ({
    appName: row.dataset.app,
    displayName: row.querySelector(".setting-display").value,
    hexColor: row.querySelector(".setting-color").value,
    category: row.querySelector(".setting-category").value,
  }));

  const status = document.getElementById("settings-status");
  try {
    const result = await saveSettingsBatch(changes);
    status.textContent = result.saved > 0
      ? `${result.saved} app(s) atualizado(s)!`
      : "Nenhuma alteração para salvar.";
    status.className = `status-message ${result.saved > 0 ? "success" : ""}`;
    await reloadAll();
    switchTab("settings");
  } catch (error) {
    console.error(error);
    status.textContent = "Erro ao salvar alterações.";
    status.className = "status-message";
  }
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}
