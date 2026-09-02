// Keeps --player-height equal to the real height of the now playing bar.
//
// The bar is fixed to the bottom and its height moves: a queue name appears, an
// error block pushes it taller, and it vanishes entirely when nothing is playing.
// A guessed constant left the last row of every list hidden behind it, so the value
// is measured instead.

let observer = null;
let watched = null;

/// Watches an element and mirrors its height into the CSS variable.
export function observe(element) {
  if (!element || element === watched) {
    return;
  }

  clear();
  watched = element;

  observer = new ResizeObserver(() => write(element.offsetHeight));
  observer.observe(element);

  write(element.offsetHeight);
}

/// Stops watching and reports no bar, so pages reclaim the space.
export function clear() {
  if (observer) {
    observer.disconnect();
    observer = null;
  }

  watched = null;
  write(0);
}

function write(height) {
  document.documentElement.style.setProperty("--player-height", `${Math.round(height)}px`);
}
