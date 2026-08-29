const DEMO_DATASET_PATH = "demo/dataset.json";

let demoDatasetPromise = null;

function isDemoMode() {
  if (window.__TIMETRACKER_DEMO__ === true) {
    return true;
  }

  const params = new URLSearchParams(window.location.search);
  if (params.get("demo") === "1") {
    return true;
  }

  return window.location.hostname.endsWith("github.io");
}

function getAssetBase() {
  const baseHref = document.querySelector("base")?.getAttribute("href");
  if (baseHref) {
    try {
      const resolved = new URL(baseHref, window.location.href);
      let path = resolved.pathname;
      if (!path.endsWith("/")) {
        path += "/";
      }
      return path;
    } catch {
      // fallback abaixo
    }
  }

  if (window.location.hostname.endsWith("github.io")) {
    const segments = window.location.pathname.split("/").filter(Boolean);
    if (segments.length > 0) {
      return `/${segments[0]}/`;
    }
  }

  return "/";
}

function resolveUrl(path) {
  const normalized = String(path || "").replace(/^\//, "");
  return new URL(normalized, window.location.origin + getAssetBase()).toString();
}

function pad2(value) {
  return String(value).padStart(2, "0");
}

function toIsoDate(date) {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;
}

function dateFromOffset(dayOffset) {
  const date = new Date();
  date.setHours(12, 0, 0, 0);
  date.setDate(date.getDate() + dayOffset);
  return date;
}

function expandDemoRecord(raw, isoDate) {
  const hour = Number.parseInt(String(raw.startTime).slice(0, 2), 10) || 0;
  return {
    id: raw.id,
    appName: raw.appName,
    windowTitle: raw.windowTitle,
    startTime: `${isoDate}T${raw.startTime}`,
    endTime: raw.endTime ? `${isoDate}T${raw.endTime}` : null,
    durationSeconds: raw.durationSeconds,
    displayName: raw.displayName,
    hexColor: raw.hexColor,
    category: raw.category,
    date: isoDate,
    hour,
  };
}

function buildDemoSummary(records) {
  const totalSeconds = records.reduce((sum, record) => sum + (record.durationSeconds || 0), 0);
  const topApp = Object.entries(
    records.reduce((acc, record) => {
      const key = record.displayName || record.appName;
      acc[key] = (acc[key] || 0) + (record.durationSeconds || 0);
      return acc;
    }, {}),
  ).sort((a, b) => b[1] - a[1])[0]?.[0] ?? null;

  return {
    totalSeconds,
    sessionCount: records.length,
    topApp,
  };
}

async function loadDemoDataset() {
  if (!demoDatasetPromise) {
    demoDatasetPromise = (async () => {
      const response = await fetch(resolveUrl(DEMO_DATASET_PATH));
      if (!response.ok) {
        throw new Error(`HTTP ${response.status} ao carregar dataset demo`);
      }
      return response.json();
    })();
  }
  return demoDatasetPromise;
}

async function demoGetDates() {
  const dataset = await loadDemoDataset();
  return [...dataset.days]
    .map((day) => toIsoDate(dateFromOffset(day.dayOffset)))
    .sort((a, b) => b.localeCompare(a));
}

async function demoGetActivity(date) {
  const dataset = await loadDemoDataset();
  const day = dataset.days.find((entry) => toIsoDate(dateFromOffset(entry.dayOffset)) === date);
  const records = day ? day.records.map((record) => expandDemoRecord(record, date)) : [];
  return {
    date,
    hasData: records.length > 0,
    records,
    summary: buildDemoSummary(records),
  };
}

async function demoGetApps() {
  const dataset = await loadDemoDataset();
  return dataset.apps;
}

async function demoGetMeta() {
  const dataset = await loadDemoDataset();
  return dataset.meta;
}

async function apiGet(path) {
  const response = await fetch(path);
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} em ${path}`);
  }
  return response.json();
}

async function apiPost(path, body) {
  const response = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} em ${path}`);
  }
  return response.json();
}

function fetchDates() {
  if (isDemoMode()) {
    return demoGetDates();
  }
  return apiGet("/api/dates");
}

function fetchActivity(date) {
  if (isDemoMode()) {
    return demoGetActivity(date);
  }
  return apiGet(`/api/activity?date=${encodeURIComponent(date)}`);
}

function fetchApps() {
  if (isDemoMode()) {
    return demoGetApps();
  }
  return apiGet("/api/apps");
}

function fetchMeta() {
  if (isDemoMode()) {
    return demoGetMeta();
  }
  return apiGet("/api/meta");
}

function saveSettingsBatch(changes) {
  if (isDemoMode()) {
    return Promise.resolve({ saved: 0, demo: true });
  }
  return apiPost("/api/settings/batch", { changes });
}
