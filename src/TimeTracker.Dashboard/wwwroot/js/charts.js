const chartInstances = new Map();

function destroyChart(key) {
  const existing = chartInstances.get(key);
  if (existing) {
    existing.destroy();
    chartInstances.delete(key);
  }
}

function renderDonut(canvas, records, colorMap) {
  destroyChart(canvas.id);
  const totals = toSortedEntries(sumBy(records, (r) => r.displayName)).slice(0, 5);
  if (!totals.length) return false;

  const labels = totals.map(([label]) => label);
  const values = totals.map(([, value]) => value);
  const colors = labels.map((label, index) => colorForLabel(label, colorMap, index));

  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "doughnut",
    data: {
      labels,
      datasets: [{
        data: values,
        backgroundColor: colors,
        borderWidth: 0,
      }],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: "bottom", labels: { color: "#e8eef7" } },
        tooltip: {
          callbacks: {
            label(context) {
              const seconds = context.raw;
              const total = values.reduce((a, b) => a + b, 0);
              const pct = total ? ((seconds / total) * 100).toFixed(1) : 0;
              return `${context.label}: ${formatDurationClean(seconds)} (${pct}%)`;
            },
          },
        },
      },
    },
  }));
  return true;
}

function renderHourlyTimeline(canvas, records, colorMap, tall = false) {
  destroyChart(canvas.id);
  const apps = [...new Set(records.map((r) => r.displayName))];
  if (!apps.length) return false;

  const datasets = apps.map((app, index) => {
    const hours = Array.from({ length: 24 }, (_, hour) => {
      return records
        .filter((r) => r.displayName === app && r.hour === hour)
        .reduce((sum, r) => sum + r.durationSeconds, 0) / 60;
    });
    return {
      label: app,
      data: hours,
      backgroundColor: colorForLabel(app, colorMap, index),
      stack: "usage",
    };
  });

  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "bar",
    data: {
      labels: Array.from({ length: 24 }, (_, i) => `${i}h`),
      datasets,
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: {
          stacked: true,
          ticks: { color: "#94a3b8" },
          grid: { color: "#2a3a55" },
        },
        y: {
          stacked: true,
          title: { display: true, text: "Min", color: "#94a3b8" },
          ticks: { color: "#94a3b8" },
          grid: { color: "#2a3a55" },
        },
      },
      plugins: {
        legend: { labels: { color: "#e8eef7" } },
        tooltip: {
          callbacks: {
            label(context) {
              const minutes = context.raw;
              return `${context.dataset.label}: ${formatDurationClean(minutes * 60)}`;
            },
          },
        },
      },
    },
  }));

  if (tall) {
    canvas.parentElement.style.minHeight = "420px";
  }
  return true;
}

function renderRanking(canvas, records, colorMap, limit) {
  destroyChart(canvas.id);
  const totals = toSortedEntries(sumBy(records, (r) => r.displayName)).slice(0, limit);
  if (!totals.length) return { rendered: false, totalApps: 0 };

  const labels = totals.map(([label]) => label).reverse();
  const values = totals.map(([, value]) => value).reverse();
  const colors = labels.map((label, index) => colorForLabel(label, colorMap, index));

  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "bar",
    data: {
      labels,
      datasets: [{
        data: values,
        backgroundColor: colors,
        borderRadius: 6,
      }],
    },
    options: {
      indexAxis: "y",
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: { display: false },
        y: { ticks: { color: "#e8eef7" }, grid: { display: false } },
      },
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label(context) {
              return formatDurationClean(context.raw);
            },
          },
        },
      },
    },
  }));

  const allCount = sumBy(records, (r) => r.displayName).size;
  return { rendered: true, totalApps: allCount };
}

function renderCategoryPie(canvas, records) {
  destroyChart(canvas.id);
  const totals = toSortedEntries(sumBy(records, (r) => r.category || "Sem Categoria"));
  if (!totals.length) return false;

  const labels = totals.map(([label]) => label);
  const values = totals.map(([, value]) => value);
  const colors = labels.map((_, index) => CHART_PALETTE[index % CHART_PALETTE.length]);

  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "pie",
    data: {
      labels,
      datasets: [{ data: values, backgroundColor: colors, borderWidth: 0 }],
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: "bottom", labels: { color: "#e8eef7" } },
        tooltip: {
          callbacks: {
            label(context) {
              const seconds = context.raw;
              const total = values.reduce((a, b) => a + b, 0);
              const pct = total ? ((seconds / total) * 100).toFixed(1) : 0;
              return `${context.label}: ${formatDurationClean(seconds)} (${pct}%)`;
            },
          },
        },
      },
    },
  }));
  return true;
}

function renderWindowTitles(canvas, records, displayName) {
  destroyChart(canvas.id);
  const appRecords = records.filter((r) => r.displayName === displayName);
  const totals = toSortedEntries(
    sumBy(appRecords, (r) => cleanWindowTitle(r.windowTitle)),
    true,
  ).slice(-15);

  if (!totals.length) return false;

  const labels = totals.map(([label]) => label);
  const values = totals.map(([, value]) => value);

  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "bar",
    data: {
      labels,
      datasets: [{
        data: values,
        backgroundColor: "rgba(59, 130, 246, 0.75)",
        borderRadius: 6,
      }],
    },
    options: {
      indexAxis: "y",
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: {
          title: { display: true, text: "Tempo Gasto", color: "#94a3b8" },
          ticks: { color: "#94a3b8", callback: (v) => formatDurationClean(v) },
          grid: { color: "#2a3a55" },
        },
        y: { ticks: { color: "#e8eef7" }, grid: { display: false } },
      },
      plugins: {
        legend: { display: false },
        tooltip: {
          callbacks: {
            label(context) {
              return formatDurationClean(context.raw);
            },
          },
        },
      },
    },
  }));
  return true;
}
