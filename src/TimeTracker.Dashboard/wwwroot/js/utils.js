const CHART_PALETTE = [
  "#0e7490", "#1e3a5f", "#c2410c", "#0f766e", "#475569",
  "#b45309", "#0369a1", "#3f6212", "#9f1239", "#4338ca",
];

function formatDurationClean(seconds) {
  if (!Number.isFinite(seconds) || seconds < 0) return "0m";
  const total = Math.floor(seconds);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function formatDurationDetailed(seconds) {
  const total = Math.max(0, Math.floor(seconds));
  const m = Math.floor(total / 60);
  const s = total % 60;
  return `${m}m ${s}s`;
}

function cleanWindowTitle(title) {
  if (!title) return "Sem Título";
  const suffixes = [
    " - Opera",
    " - Google Chrome",
    " - Microsoft Edge",
    " - Mozilla Firefox",
    " - Brave",
    " - Vivaldi",
    " - YouTube",
  ];
  let clean = title;
  for (const suffix of suffixes) {
    clean = clean.split(suffix).join("");
  }
  return clean.trim() || "Sem Título";
}

function buildColorMap(records) {
  const map = {};
  for (const record of records) {
    if (record.hexColor && record.displayName) {
      map[record.displayName] = record.hexColor;
    }
  }
  return map;
}

function colorForLabel(label, colorMap, index) {
  return colorMap[label] || CHART_PALETTE[index % CHART_PALETTE.length];
}

function sumBy(records, keyFn, valueFn = (r) => r.durationSeconds) {
  const totals = new Map();
  for (const record of records) {
    const key = keyFn(record);
    totals.set(key, (totals.get(key) || 0) + valueFn(record));
  }
  return totals;
}

function toSortedEntries(map, ascending = false) {
  return [...map.entries()].sort((a, b) => (ascending ? a[1] - b[1] : b[1] - a[1]));
}

function formatDateInput(dateStr) {
  return dateStr;
}

function todayIso() {
  return new Date().toISOString().slice(0, 10);
}

function yesterdayIso() {
  const d = new Date();
  d.setDate(d.getDate() - 1);
  return d.toISOString().slice(0, 10);
}

function formatDateTime(value) {
  if (!value) return "";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString("pt-BR");
}

function formatTime(value) {
  if (!value) return "";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}
