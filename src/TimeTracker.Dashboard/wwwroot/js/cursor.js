(() => {
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const coarsePointer = window.matchMedia("(hover: none), (pointer: coarse)");

  function canUseCustomCursor() {
    return !reduceMotion.matches && !coarsePointer.matches;
  }

  function isTextTarget(target) {
    if (!(target instanceof Element)) {
      return false;
    }

    return Boolean(
      target.closest(
        "input:not([type='button']):not([type='submit']):not([type='checkbox']):not([type='radio']):not([type='color']), textarea, select, [contenteditable='true']",
      ),
    );
  }

  function isInteractive(target) {
    if (!(target instanceof Element)) {
      return false;
    }

    return Boolean(
      target.closest(
        "a, button, label, summary, .tab, .link-quiet, .btn-icon, .btn-secondary, .btn-primary, .btn-more, [role='button'], [role='tab']",
      ),
    );
  }

  function lerp(a, b, t) {
    return a + (b - a) * t;
  }

  function initCustomCursor() {
    if (!canUseCustomCursor() || document.documentElement.classList.contains("has-custom-cursor")) {
      return;
    }

    const root = document.createElement("div");
    root.className = "cursor";
    root.setAttribute("aria-hidden", "true");
    root.innerHTML = '<div class="cursor-tip"></div><div class="cursor-halo"></div>';
    document.body.appendChild(root);
    document.documentElement.classList.add("has-custom-cursor");

    const tip = root.querySelector(".cursor-tip");
    const halo = root.querySelector(".cursor-halo");

    let pointerX = window.innerWidth / 2;
    let pointerY = window.innerHeight / 2;
    let haloX = pointerX;
    let haloY = pointerY;
    let visible = false;
    let rafId = 0;

    const show = () => {
      if (visible) {
        return;
      }
      visible = true;
      root.classList.add("is-visible");
    };

    const hide = () => {
      visible = false;
      root.classList.remove("is-visible", "is-hover", "is-down", "is-text");
    };

    const onPointerMove = (event) => {
      pointerX = event.clientX;
      pointerY = event.clientY;
      show();

      const textMode = isTextTarget(event.target);
      const hoverMode = !textMode && isInteractive(event.target);
      root.classList.toggle("is-text", textMode);
      root.classList.toggle("is-hover", hoverMode);
    };

    const tick = () => {
      haloX = lerp(haloX, pointerX, 0.16);
      haloY = lerp(haloY, pointerY, 0.16);
      tip.style.transform = `translate3d(${pointerX}px, ${pointerY}px, 0) translate(-50%, -50%)`;
      halo.style.transform = `translate3d(${haloX}px, ${haloY}px, 0) translate(-50%, -50%)`;
      rafId = requestAnimationFrame(tick);
    };

    window.addEventListener("pointermove", onPointerMove, { passive: true });
    window.addEventListener("pointerdown", () => root.classList.add("is-down"), { passive: true });
    window.addEventListener("pointerup", () => root.classList.remove("is-down"), { passive: true });
    document.addEventListener("mouseleave", hide);
    window.addEventListener("blur", hide);

    rafId = requestAnimationFrame(tick);

    const teardown = () => {
      cancelAnimationFrame(rafId);
      window.removeEventListener("pointermove", onPointerMove);
      document.removeEventListener("mouseleave", hide);
      window.removeEventListener("blur", hide);
      root.remove();
      document.documentElement.classList.remove("has-custom-cursor");
    };

    reduceMotion.addEventListener("change", (event) => {
      if (event.matches) {
        teardown();
      }
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initCustomCursor, { once: true });
  } else {
    initCustomCursor();
  }
})();
