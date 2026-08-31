// Scrolls a title only when it is actually too wide for its box.
// CSS alone cannot tell, so the width is measured here and the animation duration
// is set from the overflow, keeping the speed constant regardless of length.

const PIXELS_PER_SECOND = 40;
const observed = new WeakMap();

/// Measures one element and turns scrolling on or off.
export function refresh(element) {
  if (!element) {
    return;
  }

  const inner = element.querySelector(".marquee-text");
  if (!inner) {
    return;
  }

  // Measure without the animation running, or the transform skews the result.
  element.classList.remove("scrolling");
  inner.style.removeProperty("--marquee-shift");
  inner.style.removeProperty("--marquee-duration");

  const overflow = inner.scrollWidth - element.clientWidth;
  if (overflow <= 4) {
    return;
  }

  const distance = overflow + 24;
  inner.style.setProperty("--marquee-shift", `-${distance}px`);
  inner.style.setProperty("--marquee-duration", `${distance / PIXELS_PER_SECOND + 2}s`);
  element.classList.add("scrolling");
}

/// Keeps an element in sync as its text or width changes.
export function observe(element) {
  if (!element || observed.has(element)) {
    return;
  }

  const update = () => refresh(element);
  const resize = new ResizeObserver(update);
  resize.observe(element);

  observed.set(element, resize);
  update();
}

/// Stops watching an element.
export function unobserve(element) {
  const resize = observed.get(element);
  if (resize) {
    resize.disconnect();
    observed.delete(element);
  }
}
