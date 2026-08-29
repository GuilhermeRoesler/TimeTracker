import Lenis from "https://cdn.jsdelivr.net/npm/lenis@1.3.26/+esm";

let lenis = null;

function initSmoothScroll() {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    return;
  }

  if (lenis) {
    return;
  }

  lenis = new Lenis({
    autoRaf: true,
    duration: 1.15,
    easing: (t) => Math.min(1, 1.001 - Math.pow(2, -10 * t)),
    touchMultiplier: 1.4,
    wheelMultiplier: 0.95,
  });

  document.documentElement.classList.add("lenis", "lenis-smooth");
}

function refreshSmoothScroll() {
  if (!lenis) {
    return;
  }

  requestAnimationFrame(() => lenis.resize());
}

window.TimeTrackerSmoothScroll = {
  init: initSmoothScroll,
  refresh: refreshSmoothScroll,
};

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", initSmoothScroll, { once: true });
} else {
  initSmoothScroll();
}
