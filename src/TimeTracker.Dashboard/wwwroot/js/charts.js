const chartInstances = new Map();

const CHART_THEME = {
  text: "#1a2740",
  muted: "#5b6b82",
  grid: "rgba(15, 28, 48, 0.07)",
  tooltipBg: "rgba(255, 255, 255, 0.96)",
  tooltipTitle: "#0c1524",
  tooltipBody: "#5b6b82",
  tooltipBorder: "rgba(15, 28, 48, 0.12)",
  accent: "rgba(14, 116, 144, 0.78)",
};

function chartDefaults() {
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: "nearest", intersect: false },
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: CHART_THEME.tooltipBg,
        titleColor: CHART_THEME.tooltipTitle,
        bodyColor: CHART_THEME.tooltipBody,
        borderColor: CHART_THEME.tooltipBorder,
        borderWidth: 1,
        cornerRadius: 10,
        padding: { top: 10, right: 12, bottom: 10, left: 12 },
        caretSize: 5,
        caretPadding: 8,
        displayColors: true,
        boxWidth: 8,
        boxHeight: 8,
        boxPadding: 4,
        usePointStyle: true,
        multiKeyBackground: "#ffffff",
        titleMarginBottom: 6,
        titleFont: { family: "'Plus Jakarta Sans', sans-serif", weight: "600", size: 12 },
        bodyFont: { family: "'Plus Jakarta Sans', sans-serif", weight: "500", size: 12 },
        // Fade nativo do Chart.js (opacity) — duração alinhada ao --ease da UI
        animation: { duration: 180 },
      },
    },
  };
}

function legendOptions() {
  return {
    display: true,
    position: "bottom",
    labels: {
      color: CHART_THEME.muted,
      boxWidth: 10,
      boxHeight: 10,
      borderRadius: 2,
      useBorderRadius: true,
      padding: 14,
      font: { family: "'Plus Jakarta Sans', sans-serif", size: 11, weight: "500" },
    },
  };
}

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

  const defaults = chartDefaults();
  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "doughnut",
    data: {
      labels,
      datasets: [{
        data: values,
        backgroundColor: colors,
        borderWidth: 3,
        borderColor: "#ffffff",
        hoverOffset: 6,
      }],
    },
    options: {
      ...defaults,
      cutout: "68%",
      plugins: {
        ...defaults.plugins,
        legend: legendOptions(),
        tooltip: {
          ...defaults.plugins.tooltip,
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

function renderHourlyTimeline(canvas, records, colorMap) {
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
      borderRadius: 2,
      stack: "usage",
    };
  });

  const defaults = chartDefaults();
  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "bar",
    data: {
      labels: Array.from({ length: 24 }, (_, i) => `${i}h`),
      datasets,
    },
    options: {
      ...defaults,
      // Coluna da hora: lista só apps com uso (> 0), não o stack inteiro com zeros
      interaction: { mode: "index", intersect: false },
      scales: {
        x: {
          stacked: true,
          ticks: { color: CHART_THEME.muted, font: { size: 10, family: "'Plus Jakarta Sans', sans-serif" }, maxRotation: 0 },
          grid: { display: false },
          border: { color: CHART_THEME.grid },
        },
        y: {
          stacked: true,
          title: { display: true, text: "Min", color: CHART_THEME.muted, font: { size: 11, weight: "500" } },
          ticks: { color: CHART_THEME.muted, font: { size: 10 } },
          grid: { color: CHART_THEME.grid },
          border: { display: false },
        },
      },
      plugins: {
        ...defaults.plugins,
        tooltip: {
          ...defaults.plugins.tooltip,
          filter(item) {
            return item.parsed.y > 0;
          },
          itemSort(a, b) {
            return b.parsed.y - a.parsed.y;
          },
          callbacks: {
            label(context) {
              const minutes = context.raw;
              return `${context.dataset.label}: ${formatDurationClean(minutes * 60)}`;
            },
            footer(items) {
              const totalMin = items.reduce((sum, item) => sum + item.parsed.y, 0);
              return totalMin > 0 ? `Total: ${formatDurationClean(totalMin * 60)}` : "";
            },
          },
        },
      },
    },
  }));
  return true;
}

function renderRanking(canvas, records, colorMap) {
  destroyChart(canvas.id);
  const totals = toSortedEntries(sumBy(records, (r) => r.displayName));
  if (!totals.length) return false;

  // Ordem decrescente: maior no topo (primeiro no eixo Y do Chart.js)
  const labels = totals.map(([label]) => label);
  const values = totals.map(([, value]) => value);
  const colors = labels.map((label, index) => colorForLabel(label, colorMap, index));

  const rowHeight = 36;
  const chartHeight = Math.max(160, totals.length * rowHeight + 16);
  const box = canvas.parentElement;
  if (box) {
    box.style.height = `${chartHeight}px`;
    box.style.minHeight = `${chartHeight}px`;
  }

  const defaults = chartDefaults();
  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "bar",
    data: {
      labels,
      datasets: [{
        data: values,
        backgroundColor: colors,
        borderRadius: 5,
        borderSkipped: false,
        barThickness: 18,
      }],
    },
    options: {
      ...defaults,
      indexAxis: "y",
      scales: {
        x: { display: false, grid: { display: false } },
        y: {
          ticks: { color: CHART_THEME.text, font: { size: 11, family: "'Plus Jakarta Sans', sans-serif", weight: "500" } },
          grid: { display: false },
          border: { display: false },
        },
      },
      plugins: {
        ...defaults.plugins,
        tooltip: {
          ...defaults.plugins.tooltip,
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

function renderCategoryPie(canvas, records) {
  destroyChart(canvas.id);
  const totals = toSortedEntries(sumBy(records, (r) => r.category || "Sem Categoria"));
  if (!totals.length) return false;

  const labels = totals.map(([label]) => label);
  const values = totals.map(([, value]) => value);
  const colors = labels.map((_, index) => CHART_PALETTE[index % CHART_PALETTE.length]);

  const defaults = chartDefaults();
  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "doughnut",
    data: {
      labels,
      datasets: [{
        data: values,
        backgroundColor: colors,
        borderWidth: 3,
        borderColor: "#ffffff",
        hoverOffset: 6,
      }],
    },
    options: {
      ...defaults,
      cutout: "58%",
      plugins: {
        ...defaults.plugins,
        legend: legendOptions(),
        tooltip: {
          ...defaults.plugins.tooltip,
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

  const defaults = chartDefaults();
  chartInstances.set(canvas.id, new Chart(canvas, {
    type: "bar",
    data: {
      labels,
      datasets: [{
        data: values,
        backgroundColor: CHART_THEME.accent,
        borderRadius: 5,
        borderSkipped: false,
        barThickness: 14,
      }],
    },
    options: {
      ...defaults,
      indexAxis: "y",
      scales: {
        x: {
          title: { display: true, text: "Tempo", color: CHART_THEME.muted, font: { size: 11, weight: "500" } },
          ticks: {
            color: CHART_THEME.muted,
            font: { size: 10 },
            callback: (v) => formatDurationClean(v),
          },
          grid: { color: CHART_THEME.grid },
          border: { display: false },
        },
        y: {
          ticks: { color: CHART_THEME.text, font: { size: 11, family: "'Plus Jakarta Sans', sans-serif", weight: "500" } },
          grid: { display: false },
          border: { display: false },
        },
      },
      plugins: {
        ...defaults.plugins,
        tooltip: {
          ...defaults.plugins.tooltip,
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
