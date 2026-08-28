async function loadHealth() {
  const statusEl = document.getElementById("health-status");
  try {
    const response = await fetch("/api/health");
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const data = await response.json();
    statusEl.textContent = `API online — ${data.app}`;
  } catch (error) {
    statusEl.textContent = "API indisponível.";
    console.error(error);
  }
}

document.addEventListener("DOMContentLoaded", loadHealth);
