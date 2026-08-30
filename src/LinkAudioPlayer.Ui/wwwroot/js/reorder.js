// Touch friendly list reordering.
// HTML5 drag and drop does not fire for touch in an Android web view, so this is
// built on pointer events. It never reorders the DOM itself, because Blazor owns
// that; it only shifts rows visually and reports the final move to .NET.

let state = null;

/// Starts listening on a list. Returns a handle used to stop again.
export function attach(list, dotNet, methodName) {
  if (!list || list.dataset.reorderBound === "true") {
    return;
  }

  list.dataset.reorderBound = "true";
  list.addEventListener("pointerdown", (event) => onPointerDown(event, list, dotNet, methodName));
}

/// Detaches listeners and clears any transforms left mid-drag.
export function detach(list) {
  if (list) {
    delete list.dataset.reorderBound;
  }

  reset();
}

function onPointerDown(event, list, dotNet, methodName) {
  const grip = event.target.closest(".grip");
  if (!grip) {
    return;
  }

  const row = grip.closest("[data-index]");
  if (!row) {
    return;
  }

  const rows = [...list.querySelectorAll("[data-index]")];
  if (rows.length < 2) {
    return;
  }

  event.preventDefault();
  grip.setPointerCapture(event.pointerId);

  state = {
    list,
    rows,
    grip,
    row,
    dotNet,
    methodName,
    pointerId: event.pointerId,
    startY: event.clientY,
    from: rows.indexOf(row),
    to: rows.indexOf(row),
    height: row.getBoundingClientRect().height + gapOf(list),
  };

  row.classList.add("dragging");
  list.classList.add("reordering");

  grip.addEventListener("pointermove", onPointerMove);
  grip.addEventListener("pointerup", onPointerUp);
  grip.addEventListener("pointercancel", onPointerCancel);
}

function onPointerMove(event) {
  if (!state || event.pointerId !== state.pointerId) {
    return;
  }

  event.preventDefault();

  const delta = event.clientY - state.startY;
  const limit = state.rows.length - 1;
  const target = clamp(state.from + Math.round(delta / state.height), 0, limit);

  state.row.style.transform = `translateY(${delta}px)`;

  if (target !== state.to) {
    state.to = target;
    shiftOthers();
  }
}

/// Moves the rows between the origin and the target to open a gap.
function shiftOthers() {
  const { rows, from, to, height, row } = state;

  rows.forEach((other, index) => {
    if (other === row) {
      return;
    }

    let offset = 0;
    if (from < to && index > from && index <= to) {
      offset = -height;
    } else if (from > to && index >= to && index < from) {
      offset = height;
    }

    other.style.transform = offset === 0 ? "" : `translateY(${offset}px)`;
  });
}

function onPointerUp(event) {
  if (!state || event.pointerId !== state.pointerId) {
    return;
  }

  const { dotNet, methodName, from, to } = state;
  finish();

  if (from !== to) {
    dotNet.invokeMethodAsync(methodName, from, to).catch(() => {});
  }
}

function onPointerCancel(event) {
  if (state && event.pointerId === state.pointerId) {
    finish();
  }
}

function finish() {
  const { grip } = state;

  grip.removeEventListener("pointermove", onPointerMove);
  grip.removeEventListener("pointerup", onPointerUp);
  grip.removeEventListener("pointercancel", onPointerCancel);

  reset();
}

/// Clears every transform so Blazor re-renders onto a clean list.
function reset() {
  if (!state) {
    return;
  }

  state.rows.forEach((row) => {
    row.style.transform = "";
    row.classList.remove("dragging");
  });

  state.list.classList.remove("reordering");
  state = null;
}

/// Reads the flex gap so the drag step matches the visual row pitch.
function gapOf(list) {
  const gap = parseFloat(getComputedStyle(list).rowGap);
  return Number.isFinite(gap) ? gap : 0;
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}
