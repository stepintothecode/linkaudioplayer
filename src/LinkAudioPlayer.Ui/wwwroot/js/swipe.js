// Horizontal swipe between the top level tabs, with the page following the finger.
//
// Two rules keep this from breaking what already works:
//
//   1. It never calls preventDefault. The gesture is only observed, so vertical
//      scrolling and every other touch behaviour is untouched.
//   2. A gesture starting on something that owns horizontal dragging is ignored.
//
// That second list is deliberately short. Buttons and links are tap targets, not
// drag targets, and the browser cancels a tap once the finger moves, so a swipe is
// allowed to begin on one. Refusing them made most of a list row dead to the
// gesture, which is what made the playlists page feel stuck.
//
// Touch events are used rather than pointer events on purpose: reorder.js captures
// the pointer on the grip, and sharing that stream invites conflicts.

const MIN_DISTANCE = 48; // px for a deliberate drag
const FLICK_DISTANCE = 22; // px, when it is fast enough to be a flick
const FLICK_SPEED = 0.35; // px per ms
const MAX_FLICK_DURATION = 900; // ms, a flick has to be quick; a long drag may take its time
const HORIZONTAL_BIAS = 1.15; // how much more horizontal than vertical it must be
const DECIDE_AFTER = 8; // px before the direction is judged
const FOLLOW = 0.32; // how far the page moves with the finger
const RESIST = 0.08; // the same, at the ends of the strip where there is nowhere to go

// Things that own a horizontal drag of their own.
const IGNORE_WITHIN = "input, textarea, select, .grip, .scrubber, .player, .scrim, [data-no-swipe]";

let state = null;
let dotNet = null;
let target = null;
let tabAt = null;

/// Starts listening on an element. Safe to call more than once.
export function attach(element, dotNetRef, methodName, tabPosition) {
  if (!element || element.dataset.swipeBound === "true") {
    return;
  }

  element.dataset.swipeBound = "true";
  target = element;
  dotNet = { ref: dotNetRef, method: methodName };
  tabAt = tabPosition;

  // On the document rather than the element, so the whole screen takes the gesture:
  // the header, and the empty space below a short page, which main does not cover.
  // Passive, so the browser never waits on this handler before scrolling.
  document.addEventListener("touchstart", onStart, { passive: true });
  document.addEventListener("touchmove", onMove, { passive: true });
  document.addEventListener("touchend", onEnd, { passive: true });
  document.addEventListener("touchcancel", onCancel, { passive: true });
}

/// Stops listening and clears any gesture in progress.
export function detach(element) {
  const el = element || target;
  if (!el) {
    return;
  }

  delete el.dataset.swipeBound;
  document.removeEventListener("touchstart", onStart);
  document.removeEventListener("touchmove", onMove);
  document.removeEventListener("touchend", onEnd);
  document.removeEventListener("touchcancel", onCancel);

  release(true);
  target = null;
  dotNet = null;
  tabAt = null;
}

/// Tells the module where the current page sits, so the ends can resist.
export function setPosition(tabPosition) {
  tabAt = tabPosition;
}

function onStart(event) {
  release(true);

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
    page: target ? target.querySelector(".page") : null,
  };
}

function onMove(event) {
  if (!state || event.touches.length !== 1) {
    return;
  }

  const touch = event.touches[0];
  const dx = touch.clientX - state.x;
  const dy = touch.clientY - state.y;

  // The axis is decided once, on the first meaningful movement, and then held.
  // Without this a curved swipe could flip mid gesture.
  if (state.axis === null) {
    if (Math.abs(dx) < DECIDE_AFTER && Math.abs(dy) < DECIDE_AFTER) {
      return;
    }

    state.axis = Math.abs(dx) > Math.abs(dy) * HORIZONTAL_BIAS ? "x" : "y";

    if (state.axis === "x" && state.page) {
      // Removed for the duration so the page tracks the finger without lag.
      state.page.style.transition = "none";
    }
  }

  if (state.axis !== "x" || !state.page) {
    return;
  }

  state.page.style.transform = `translateX(${dx * damping(dx)}px)`;
}

function onEnd(event) {
  if (!state || state.axis !== "x") {
    release(true);
    return;
  }

  const touch = event.changedTouches[0];
  const dx = touch.clientX - state.x;
  const elapsed = Math.max(Date.now() - state.at, 1);
  const speed = Math.abs(dx) / elapsed;

  const far = Math.abs(dx) >= MIN_DISTANCE;
  const flicked = speed >= FLICK_SPEED && Math.abs(dx) >= FLICK_DISTANCE && elapsed <= MAX_FLICK_DURATION;

  // Dragging left moves forward through the tabs, the way pages do.
  const direction = dx < 0 ? 1 : -1;

  if ((far || flicked) && canMove(direction) && dotNet) {
    // The transform is left in place: the outgoing page keeps its offset until
    // the new one replaces it, which is what makes the change read as one motion.
    release(false);
    dotNet.ref.invokeMethodAsync(dotNet.method, direction).catch(() => release(true));
    return;
  }

  release(true);
}

function onCancel() {
  release(true);
}

/// How much of the finger's travel the page takes, given where the strip ends.
function damping(dx) {
  return canMove(dx < 0 ? 1 : -1) ? FOLLOW : RESIST;
}

/// False when there is no tab in that direction.
function canMove(direction) {
  if (tabAt === null || tabAt === undefined || tabAt.index < 0) {
    return false;
  }

  const next = tabAt.index + direction;
  return next >= 0 && next < tabAt.count;
}

/// Clears the gesture. Springs the page back unless the page is about to change.
function release(springBack) {
  if (state && state.page && springBack) {
    state.page.style.transition = "";
    state.page.style.transform = "";
  }

  state = null;
}
