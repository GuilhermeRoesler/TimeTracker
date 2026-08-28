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
  return apiGet("/api/dates");
}

function fetchActivity(date) {
  return apiGet(`/api/activity?date=${encodeURIComponent(date)}`);
}

function fetchApps() {
  return apiGet("/api/apps");
}

function fetchMeta() {
  return apiGet("/api/meta");
}

function saveSettingsBatch(changes) {
  return apiPost("/api/settings/batch", { changes });
}
