// Remembers how far down each page was scrolled and puts you back there on return,
// whether by the back button, a swipe or a tab.
//
// The target is read the moment the route changes, before the new page renders.
// Reading it later is too late: swapping the page shrinks the document, the browser
// clamps the scroll, and that clamped value would overwrite the one being restored.
// Saving is paused from the route change until the restore lands, for the same reason.

const positions = new Map();
const GIVE_UP_AFTER = 1500; // ms to wait for a list to load before settling for less

let pending = null; // { path, y } waiting for the page to render
let restoring = 0; // token of the restore loop running, 0 when none
let paused = false;

/// Starts remembering. Safe to call more than once.
export function attach() {
  if (window.__scrollMemoryBound) {
    return;
  }

  window.__scrollMemoryBound = true;

  // The browser's own restoration runs before Blazor has drawn anything.
  history.scrollRestoration = "manual";

  window.addEventListener("scroll", () => {
    if (!paused) {
      positions.set(location.pathname, window.scrollY);
    }
  }, { passive: true });

  for (const name of ["pushState", "replaceState"]) {
    const original = history[name].bind(history);
    history[name] = (state, title, url) => {
      const before = location.pathname;
      if (!paused) {
        positions.set(before, window.scrollY);
      }

      original(state, title, url);

      if (location.pathname !== before) {
        expect();
      }
    };
  }

  window.addEventListener("popstate", expect);

  // A finger on the screen means the user has taken over.
  window.addEventListener("touchstart", stop, { passive: true });
}

/// Called by the layout once the page for a route has rendered.
export function rendered(path) {
  if (!pending || pending.path !== normalise(path)) {
    return;
  }

  const { y } = pending;
  pending = null;

  const token = ++restoring;
  const giveUpAt = performance.now() + GIVE_UP_AFTER;

  const step = () => {
    if (token !== restoring) {
      return;
    }

    // Waits until the page is tall enough, since lists load after the first render.
    const room = document.documentElement.scrollHeight - window.innerHeight;
    if (room >= y || performance.now() >= giveUpAt) {
      window.scrollTo(0, Math.max(0, Math.min(y, room)));
      restoring = 0;
      paused = false;
      return;
    }

    requestAnimationFrame(step);
  };

  requestAnimationFrame(step);
}

function expect() {
  restoring = 0;
  paused = true;
  const expected = { path: normalise(location.pathname), y: positions.get(location.pathname) ?? 0 };
  pending = expected;

  // If the render is never reported, saving must not stay paused for good.
  setTimeout(() => {
    if (pending === expected) {
      pending = null;
      paused = false;
    }
  }, GIVE_UP_AFTER * 2);
}

function stop() {
  if (restoring) {
    restoring = 0;
    paused = false;
  }
}

/// "/playlists/" and "playlists" name the same page.
function normalise(path) {
  return (path || "").replace(/^\/+|\/+$/g, "");
}
