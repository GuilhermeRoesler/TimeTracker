const state = {
  availableDates: [],
  selectedDate: null,
  records: [],
  summary: null,
  hasData: false,
  isLoading: true,
  categories: [],
  apps: [],
  activeTab: "overview",
  settingsSearch: "",
  enterPlayed: false,
};

const els = {
  bootLoader: document.getElementById("boot-loader"),
  emptyState: document.getElementById("empty-state"),
  appLayout: document.getElementById("app-layout"),
  dateShortcuts: document.getElementById("date-shortcuts"),
  datePicker: document.getElementById("date-picker"),
  btnPrev: document.getElementById("btn-prev-day"),
  btnNext: document.getElementById("btn-next-day"),
  dateWarning: document.getElementById("date-warning"),
  btnRefresh: document.getElementById("btn-refresh"),
  btnAppUpdate: document.getElementById("btn-app-update"),
  btnOpenBrowser: document.getElementById("btn-open-browser"),
  btnOpenBrowserEmpty: document.getElementById("btn-open-browser-empty"),
  headerDate: document.getElementById("header-date"),
  panelOverview: document.getElementById("panel-overview"),
  panelDetails: document.getElementById("panel-details"),
  panelSettings: document.getElementById("panel-settings"),
  tabs: document.querySelectorAll(".tab"),
};

let updatePollTimer = null;

document.addEventListener("DOMContentLoaded", init);

async function init() {
  setupDemoBanner();
  setupAppShellChrome();
  bindEvents();
  await reloadAll();
  startUpdateStatusPolling();
}

function setupDemoBanner() {
  if (typeof isDemoMode !== "function" || !isDemoMode()) {
    return;
  }

  document.body.classList.add("demo-mode");
  document.getElementById("demo-banner")?.classList.remove("hidden");
}

function isAppShell() {
  return new URLSearchParams(window.location.search).get("shell") === "app";
}

function setupAppShellChrome() {
  if (!isAppShell()) {
    return;
  }

  document.body.classList.add("shell-app");
  els.btnOpenBrowser?.classList.remove("hidden");
  els.btnOpenBrowserEmpty?.classList.remove("hidden");
}

function openInSystemBrowser() {
  if (window.chrome?.webview?.postMessage) {
    window.chrome.webview.postMessage("openInBrowser");
    return;
  }

  window.open(`${window.location.origin}/`, "_blank", "noopener,noreferrer");
}

function bindEvents() {
  els.btnRefresh.addEventListener("click", () => reloadAll());
  els.btnAppUpdate?.addEventListener("click", onAppUpdateClick);
  els.btnOpenBrowser?.addEventListener("click", openInSystemBrowser);
  els.btnOpenBrowserEmpty?.addEventListener("click", openInSystemBrowser);
  els.btnPrev.addEventListener("click", () => navigateDate(1));
  els.btnNext.addEventListener("click", () => navigateDate(-1));
  els.datePicker.addEventListener("change", () => {
    if (state.isLoading) return;
    state.selectedDate = els.datePicker.value;
    loadActivityForSelectedDate();
  });

  els.tabs.forEach((tab) => {
    tab.addEventListener("click", () => switchTab(tab.dataset.tab));
  });
}

function startUpdateStatusPolling() {
  refreshUpdateButton();
  if (updatePollTimer) {
    clearInterval(updatePollTimer);
  }
  // O check no Tracker roda ~3s após abrir; poll curto no início, depois a cada 60s.
  updatePollTimer = setInterval(refreshUpdateButton, 15_000);
  setTimeout(refreshUpdateButton, 4_000);
  setTimeout(refreshUpdateButton, 8_000);
}

async function refreshUpdateButton() {
  const button = els.btnAppUpdate;
  if (!button || typeof fetchUpdateStatus !== "function") {
    return;
  }

  try {
    const status = await fetchUpdateStatus();
    const show = Boolean(status?.enabled && status?.available);
    button.classList.toggle("hidden", !show);
    if (!show) {
      return;
    }

    const tag = status.tagName || status.latestVersion || "";
    if (status.installing) {
      button.disabled = true;
      button.textContent = "Baixando…";
      button.title = "Download da atualização em andamento";
      return;
    }

    button.disabled = false;
    button.textContent = tag ? `Atualizar ${tag}` : "Atualizar app";
    button.title = tag
      ? `Baixar e instalar ${tag} (versão atual: ${status.currentVersion || "?"})`
      : "Baixar e instalar a nova versão";
  } catch (error) {
    console.error(error);
  }
}

async function onAppUpdateClick() {
  const button = els.btnAppUpdate;
  if (!button || button.disabled) {
    return;
  }

  button.disabled = true;
  button.textContent = "Baixando…";

  try {
    await applyAppUpdate();
    // O app encerra ao iniciar o Setup; se ainda estiver aberto, manter feedback.
    button.textContent = "Instalando…";
  } catch (error) {
    console.error(error);
    button.disabled = false;
    button.textContent = "Atualizar app";
    window.alert(error?.message
      ? `Não foi possível atualizar:\n${error.message}`
      : "Não foi possível iniciar a atualização.");
    await refreshUpdateButton();
  }
}

async function reloadAll() {
  const layoutVisible = !els.appLayout.classList.contains("hidden");
  setLoading(true, { boot: !layoutVisible });

  try {
    const [dates, meta] = await Promise.all([fetchDates(), fetchMeta()]);
    state.availableDates = dates;
    state.categories = meta.categories || [];

    if (!dates.length) {
      setLoading(false);
      showEmptyState();
      return;
    }

    if (!state.selectedDate || !dates.includes(state.selectedDate)) {
      state.selectedDate = dates[0];
    }

    await fetchAndApplyActivity(state.selectedDate);
    setLoading(false);
    showAppLayout();
    updateDateControls();
    renderActiveTab();
    refreshUpdateButton();

    if (state.activeTab === "settings") {
      await loadSettingsPanel();
    }
  } catch (error) {
    console.error(error);
    setLoading(false);
    showEmptyState("Erro ao carregar dados. Verifique se o dashboard está rodando.");
  }
}

async function loadActivityForSelectedDate() {
  if (!state.selectedDate) return;

  setLoading(true, { boot: false });
  try {
    await fetchAndApplyActivity(state.selectedDate);
    setLoading(false);
    updateDateControls();
    renderActiveTab();
  } catch (error) {
    console.error(error);
    setLoading(false);
    updateDateControls();
    renderActiveTab();
  }
}

async function fetchAndApplyActivity(date) {
  const payload = await fetchActivity(date);
  state.records = payload.records || [];
  state.summary = payload.summary || null;
  state.hasData = Boolean(payload.hasData);
}

function setLoading(loading, { boot = false } = {}) {
  state.isLoading = loading;

  if (boot || (loading && els.appLayout.classList.contains("hidden"))) {
    els.bootLoader?.classList.toggle("hidden", !loading);
  } else if (!loading) {
    els.bootLoader?.classList.add("hidden");
  }

  if (els.btnRefresh) {
    els.btnRefresh.disabled = loading;
  }
  if (els.datePicker) {
    els.datePicker.disabled = loading;
  }
  if (loading) {
    els.btnPrev.disabled = true;
    els.btnNext.disabled = true;
  }

  // Nunca mostrar "sem registros" enquanto ainda carrega.
  if (els.dateWarning) {
    els.dateWarning.classList.toggle("hidden", loading || state.hasData);
  }

  if (loading && !boot && !els.appLayout.classList.contains("hidden")) {
    showPanelLoader();
  }
}

function showPanelLoader() {
  const mark = `
    <div class="panel-loader-copy">
      <div class="loader-mark" aria-hidden="true">
        <span class="loader-mark-ring"></span>
        <span class="loader-mark-core"></span>
      </div>
      <p>Atualizando dados do dia…</p>
    </div>`;

  let body = `
    <div class="skel-metrics" aria-hidden="true">
      <div class="skel skel-metric"></div>
      <div class="skel skel-metric"></div>
      <div class="skel skel-metric"></div>
    </div>
    <div class="skel-grid" aria-hidden="true">
      <div class="skel skel-card"></div>
      <div class="skel skel-card"></div>
    </div>`;

  if (state.activeTab === "details") {
    body = `
      <div class="skel skel-toolbar" aria-hidden="true"></div>
      <div class="skel-grid" aria-hidden="true">
        <div class="skel skel-card"></div>
        <div class="skel skel-card"></div>
      </div>`;
  }

  const html = `<div class="panel-loader" role="status" aria-live="polite">${mark}${body}</div>`;

  if (state.activeTab === "overview") {
    els.panelOverview.innerHTML = html;
  } else if (state.activeTab === "details") {
    els.panelDetails.innerHTML = html;
  }
}

function showEmptyState(message) {
  els.bootLoader?.classList.add("hidden");
  els.emptyState.classList.remove("hidden");
  els.appLayout.classList.add("hidden");
  els.appLayout.classList.remove("is-entering");
  if (message) {
    els.emptyState.querySelector("p").textContent = message;
  }
  playEmptyEnter();
}

function showAppLayout() {
  els.bootLoader?.classList.add("hidden");
  els.emptyState.classList.add("hidden");
  els.emptyState.classList.remove("is-entering");
  els.appLayout.classList.remove("hidden");
  refreshSmoothScroll();
}

function playEmptyEnter() {
  if (state.enterPlayed) return;
  state.enterPlayed = true;
  const empty = els.emptyState;
  empty.classList.remove("is-entering");
  void empty.offsetWidth;
  empty.classList.add("is-entering");
  window.setTimeout(() => empty.classList.remove("is-entering"), 650);
}

function playDashboardEnter() {
  if (state.enterPlayed) return;
  state.enterPlayed = true;
  const layout = els.appLayout;
  layout.classList.remove("is-entering");
  void layout.offsetWidth;
  layout.classList.add("is-entering");
  // Maior delay (~550ms) + duração (550ms); remove a classe para não reanimar em refresh/data.
  window.setTimeout(() => layout.classList.remove("is-entering"), 1200);
}

function formatHeaderDate(iso) {
  if (!iso) return "";
  const [y, m, d] = iso.split("-").map(Number);
  const date = new Date(y, m - 1, d);
  const formatted = date.toLocaleDateString("pt-BR", {
    weekday: "long",
    day: "numeric",
    month: "long",
    year: "numeric",
  });
  return formatted.charAt(0).toUpperCase() + formatted.slice(1);
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
  const navLocked = state.isLoading;
  els.btnPrev.disabled = navLocked || !inList || currentIndex >= dates.length - 1;
  els.btnNext.disabled = navLocked || !inList || currentIndex <= 0;
  if (els.datePicker) {
    els.datePicker.disabled = navLocked;
  }

  els.dateWarning.classList.toggle("hidden", state.isLoading || state.hasData);

  if (els.headerDate) {
    els.headerDate.textContent = formatHeaderDate(state.selectedDate);
  }

  els.dateShortcuts.innerHTML = "";
  const shortcuts = [];
  if (dates.includes(todayIso())) shortcuts.push(["Hoje", todayIso()]);

  for (const [label, value] of shortcuts) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = `btn-secondary${value === state.selectedDate ? " is-active" : ""}`;
    button.textContent = label;
    button.addEventListener("click", () => {
      if (state.isLoading) return;
      state.selectedDate = value;
      loadActivityForSelectedDate();
    });
    els.dateShortcuts.appendChild(button);
  }
}

function navigateDate(direction) {
  if (state.isLoading) return;
  const index = state.availableDates.indexOf(state.selectedDate);
  if (index < 0) return;
  const nextIndex = index + direction;
  if (nextIndex < 0 || nextIndex >= state.availableDates.length) return;
  state.selectedDate = state.availableDates[nextIndex];
  loadActivityForSelectedDate();
}

function switchTab(tabName) {
  state.activeTab = tabName;
  els.tabs.forEach((tab) => {
    const active = tab.dataset.tab === tabName;
    tab.classList.toggle("active", active);
    tab.setAttribute("aria-selected", active ? "true" : "false");
  });
  els.panelOverview.classList.toggle("hidden", tabName !== "overview");
  els.panelDetails.classList.toggle("hidden", tabName !== "details");
  els.panelSettings.classList.toggle("hidden", tabName !== "settings");
  renderActiveTab();

  const panel = tabName === "overview"
    ? els.panelOverview
    : tabName === "details"
      ? els.panelDetails
      : els.panelSettings;
  if (state.enterPlayed && panel) {
    panel.classList.remove("panel-enter");
    void panel.offsetWidth;
    panel.classList.add("panel-enter");
  }

  refreshSmoothScroll();
}

function refreshSmoothScroll() {
  window.TimeTrackerSmoothScroll?.refresh?.();
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
  const topApp = summary.topApp || "—";
  const topAppClass = topApp.length > 18 ? " metric-value is-long" : "metric-value";

  els.panelOverview.innerHTML = `
    <div class="metrics">
      <div class="metric-card">
        <div class="metric-label">Tempo total</div>
        <div class="metric-value">${hours}h ${minutes}m</div>
      </div>
      <div class="metric-card">
        <div class="metric-label">Sessões de foco</div>
        <div class="metric-value">${summary.sessionCount}</div>
      </div>
      <div class="metric-card">
        <div class="metric-label">App mais usado</div>
        <div class="${topAppClass}">${escapeHtml(topApp)}</div>
      </div>
    </div>
    <div class="grid-2">
      <div class="card">
        <div class="card-head">
          <h3>Distribuição</h3>
          <p class="card-sub">Top 5 aplicativos</p>
        </div>
        <div class="chart-box"><canvas id="chart-donut"></canvas></div>
      </div>
      <div class="card">
        <div class="card-head">
          <h3>Linha do tempo</h3>
          <p class="card-sub">Uso por hora</p>
        </div>
        <div class="chart-box"><canvas id="chart-hourly"></canvas></div>
      </div>
    </div>
    <div class="grid-2">
      <div class="card">
        <div class="card-head">
          <h3>Ranking</h3>
          <p class="card-sub">Detalhado por app</p>
        </div>
        <div class="chart-scroll" data-lenis-prevent>
          <div class="chart-box" id="ranking-chart-box"><canvas id="chart-ranking"></canvas></div>
        </div>
      </div>
      <div class="card">
        <div class="card-head">
          <h3>Categorias</h3>
          <p class="card-sub">Composição do dia</p>
        </div>
        <div class="chart-box"><canvas id="chart-category"></canvas></div>
      </div>
    </div>
    <h3 class="section-title" style="margin-top: 1.35rem;">Histórico detalhado</h3>
    <p class="section-lead">Sessões do dia selecionado, da mais recente à mais antiga.</p>
    <div class="card table-wrap" data-lenis-prevent>${renderHistoryTable(records)}</div>
  `;

  if (!records.length) {
    els.panelOverview.querySelectorAll(".chart-box").forEach((box) => {
      box.innerHTML = '<p class="info-box">Sem dados.</p>';
    });
    playDashboardEnter();
    refreshSmoothScroll();
    return;
  }

  const donutOk = renderDonut(document.getElementById("chart-donut"), records, colorMap);
  if (!donutOk) document.getElementById("chart-donut").parentElement.innerHTML = '<p class="info-box">Sem dados.</p>';

  const hourlyOk = renderHourlyTimeline(document.getElementById("chart-hourly"), records, colorMap);
  if (!hourlyOk) document.getElementById("chart-hourly").parentElement.innerHTML = '<p class="info-box">Sem atividades.</p>';

  const rankingOk = renderRanking(document.getElementById("chart-ranking"), records, colorMap);
  if (!rankingOk) {
    const box = document.getElementById("ranking-chart-box");
    if (box) box.innerHTML = '<p class="info-box">Sem dados.</p>';
  }

  const catOk = renderCategoryPie(document.getElementById("chart-category"), records);
  if (!catOk) document.getElementById("chart-category").parentElement.innerHTML = '<p class="info-box">Sem dados de categoria.</p>';

  playDashboardEnter();
  refreshSmoothScroll();
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
    <h3 class="section-title">Detalhes por aplicativo</h3>
    <p class="section-lead">Selecione um app para ver em quais abas ou arquivos você passou mais tempo.</p>
    <div class="panel-toolbar">
      <label class="field-label" for="details-app-select">Aplicativo</label>
      <select id="details-app-select">${apps.map((app, i) => `<option value="${escapeHtml(app)}" ${i === defaultIndex ? "selected" : ""}>${escapeHtml(app)}</option>`).join("")}</select>
    </div>
    <div class="details-layout">
      <div class="card">
        <div class="card-head">
          <h3 id="details-chart-title">Top abas e janelas</h3>
          <p class="card-sub">Até 15 títulos</p>
        </div>
        <div class="chart-box tall"><canvas id="chart-windows"></canvas></div>
      </div>
      <div class="card">
        <div class="card-head">
          <h3>Histórico cronológico</h3>
          <p class="card-sub">Sessões do app</p>
        </div>
        <div id="details-history" class="table-wrap" data-lenis-prevent></div>
      </div>
    </div>
  `;

  const select = document.getElementById("details-app-select");
  const renderForApp = () => {
    const appName = select.value;
    document.getElementById("details-chart-title").textContent = `Top abas e janelas · ${appName}`;
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
  refreshSmoothScroll();
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

  const demo = typeof isDemoMode === "function" && isDemoMode();
  els.panelSettings.innerHTML = `
    <h3 class="section-title">Personalizar aplicativos</h3>
    <p class="section-lead">${demo
      ? "Na demonstração, as alterações não são persistidas."
      : "Defina nomes amigáveis, cores e categorias. Somente alterações são salvas."}</p>
    <input type="search" id="settings-search" class="settings-search" placeholder="Filtrar por executável ou nome de exibição…" value="${escapeHtml(state.settingsSearch)}" />
    <p class="settings-meta">${filtered.length} app(s) exibido(s)</p>
    <div class="card settings-table table-wrap" data-lenis-prevent>${filtered.length ? renderSettingsTable(filtered) : '<p class="info-box">Nenhum app corresponde à busca.</p>'}</div>
    <div class="settings-actions">
      <button type="button" id="btn-save-settings" class="btn-primary" ${demo ? "disabled" : ""}>Salvar alterações</button>
      <span id="settings-status" class="status-message">${demo ? "Modo demo: salvamento desativado." : ""}</span>
    </div>
  `;

  document.getElementById("settings-search")?.addEventListener("input", (event) => {
    state.settingsSearch = event.target.value;
    renderSettings();
  });

  if (!demo) {
    document.getElementById("btn-save-settings")?.addEventListener("click", saveSettings);
  }
  refreshSmoothScroll();
}

function renderSettingsTable(apps) {
  const categoryOptions = (selected) => state.categories.map((cat) =>
    `<option value="${escapeHtml(cat)}" ${cat === selected ? "selected" : ""}>${escapeHtml(cat)}</option>`,
  ).join("");

  const rows = apps.map((app) => {
    const color = app.hexColor || "#64748b";
    const category = app.category || "Sem Categoria";
    return `
      <tr data-app="${escapeHtml(app.appName)}">
        <td><code style="font-family:var(--mono);font-size:0.78rem;">${escapeHtml(app.appName)}</code></td>
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
      ? `${result.saved} app(s) atualizado(s).`
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
