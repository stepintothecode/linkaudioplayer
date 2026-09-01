// Detects a horizontal swipe between the top level tabs.
//
// Two rules keep this from breaking what already works:
//
//   1. It never calls preventDefault. The gesture is only observed, so vertical
//      scrolling and every other touch behaviour is untouched.
//   2. A gesture starting on anything interactive is ignored outright, which is
//      what stops it fighting the reorder grip, the scrubber and text fields.
//
// Touch events are used rather than pointer events on purpose: reorder.js captures
// the pointer on the grip, and sharing that stream invites conflicts.

const MIN_DISTANCE = 64; // px before a swipe counts
const MAX_DURATION = 700; // ms, so a slow drag is not a swipe
const HORIZONTAL_BIAS = 1.4; // how much more horizontal than vertical it must be
const DECIDE_AFTER = 10; // px before the direction is judged

const IGNORE_WITHIN = "input, textarea, select, button, a, .grip, .scrubber, .player, .scrim";

let state = null;
let dotNet = null;
let target = null;

/// Starts listening on an element. Safe to call more than once.
export function attach(element, dotNetRef, methodName) {
  if (!element || element.dataset.swipeBound === "true") {
    return;
  }

  element.dataset.swipeBound = "true";
  target = element;
  dotNet = { ref: dotNetRef, method: methodName };

  // Passive, so the browser never waits on this handler before scrolling.
  element.addEventListener("touchstart", onStart, { passive: true });
  element.addEventListener("touchmove", onMove, { passive: true });
  element.addEventListener("touchend", onEnd, { passive: true });
  element.addEventListener("touchcancel", reset, { passive: true });
}

/// Stops listening and clears any gesture in progress.
export function detach(element) {
  const el = element || target;
  if (!el) {
    return;
  }

  delete el.dataset.swipeBound;
  el.removeEventListener("touchstart", onStart);
  el.removeEventListener("touchmove", onMove);
  el.removeEventListener("touchend", onEnd);
  el.removeEventListener("touchcancel", reset);

  reset();
  target = null;
  dotNet = null;
}

function onStart(event) {
  reset();

  // Multi touch is a pinch or a stray palm, never a tab swipe.
  if (event.touches.length !== 1) {
    return;
  }

  if (event.target.closest && event.target.closest(IGNORE_WITHIN)) {
    return;
  }

  const touch = event.touches[0];
  state = {
    x: touch.clientX,
    y: touch.clientY,
    at: Date.now(),
    axis: null,
  };
}

function onMove(event) {
  if (!state || event.touches.length !== 1) {
    return;
  }

  const touch = event.touches[0];
  const dx = touch.clientX - state.x;
  const dy = touch.clientY - state.y;

  if (state.axis !== null) {
    return;
  }

  // The axis is decided once, on the first meaningful movement, and then held.
  // Without this a curved swipe could flip mid gesture.
  if (Math.abs(dx) < DECIDE_AFTER && Math.abs(dy) < DECIDE_AFTER) {
    return;
  }

  state.axis = Math.abs(dx) > Math.abs(dy) * HORIZONTAL_BIAS ? "x" : "y";
}

function onEnd(event) {
  if (!state || state.axis !== "x") {
    reset();
    return;
  }

  const touch = event.changedTouches[0];
  const dx = touch.clientX - state.x;
  const elapsed = Date.now() - state.at;

  reset();

  if (Math.abs(dx) < MIN_DISTANCE || elapsed > MAX_DURATION || !dotNet) {
    return;
  }

  // Dragging left moves forward through the tabs, the way pages do.
  dotNet.ref.invokeMethodAsync(dotNet.method, dx < 0 ? 1 : -1).catch(() => {});
}

function reset() {
  state = null;
}
