// Owns the single <audio> element and mirrors its state into the Media Session API.
// Media Session is what puts titles, artwork and transport controls on the Android
// lock screen, and it is why playback survives the screen turning off.

let audio = null;
let dotNet = null;

/// Creates the audio element and wires playback events back to .NET.
export function initialize(dotNetRef) {
  dotNet = dotNetRef;

  if (audio) {
    return;
  }

  audio = new Audio();
  audio.preload = "metadata";

  audio.addEventListener("play", () => notify("OnPlaybackStateChanged", true));
  audio.addEventListener("pause", () => notify("OnPlaybackStateChanged", false));
  audio.addEventListener("ended", () => notify("OnTrackEnded"));
  audio.addEventListener("timeupdate", () => {
    notify("OnPositionChanged", audio.currentTime, audio.duration || 0);
    updatePositionState();
  });
  audio.addEventListener("error", () => {
    notify("OnPlaybackError", describeError(audio.error));
  });

  registerActionHandlers();
}

/// Points the player at a track and sets its lock screen metadata.
export async function load(streamUrl, metadata) {
  if (!audio) {
    return;
  }

  audio.src = streamUrl;
  setMetadata(metadata);

  try {
    await audio.play();
  } catch (error) {
    // Autoplay is blocked until the user interacts with the page. The UI shows a play button.
    notify("OnPlaybackError", error.message);
  }
}

/// Resumes playback.
export async function play() {
  if (audio) {
    await audio.play();
  }
}

/// Pauses playback without clearing the source.
export function pause() {
  if (audio) {
    audio.pause();
  }
}

/// Seeks to a position in seconds.
export function seek(seconds) {
  if (audio && Number.isFinite(seconds)) {
    audio.currentTime = seconds;
  }
}

/// Sets output volume between 0 and 1.
export function setVolume(level) {
  if (audio) {
    audio.volume = Math.min(1, Math.max(0, level));
  }
}

/// Stops playback and releases the current source.
export function stop() {
  if (audio) {
    audio.pause();
    audio.removeAttribute("src");
    audio.load();
  }

  if ("mediaSession" in navigator) {
    navigator.mediaSession.metadata = null;
    navigator.mediaSession.playbackState = "none";
  }
}

/// Publishes track details to the lock screen and notification shade.
function setMetadata(metadata) {
  if (!("mediaSession" in navigator) || !metadata) {
    return;
  }

  const artwork = metadata.thumbnailUrl
    ? [{ src: metadata.thumbnailUrl, sizes: "512x512", type: "image/jpeg" }]
    : [];

  navigator.mediaSession.metadata = new MediaMetadata({
    title: metadata.title ?? "Unknown",
    artist: metadata.artist ?? "",
    album: metadata.album ?? "",
    artwork,
  });
}

/// Binds hardware and lock screen buttons to the player.
function registerActionHandlers() {
  if (!("mediaSession" in navigator)) {
    return;
  }

  const handlers = {
    play: () => audio.play(),
    pause: () => audio.pause(),
    previoustrack: () => notify("OnPreviousRequested"),
    nexttrack: () => notify("OnNextRequested"),
    seekbackward: (details) => seek(audio.currentTime - (details.seekOffset || 10)),
    seekforward: (details) => seek(audio.currentTime + (details.seekOffset || 10)),
    seekto: (details) => seek(details.seekTime),
    stop: () => stop(),
  };

  for (const [action, handler] of Object.entries(handlers)) {
    try {
      navigator.mediaSession.setActionHandler(action, handler);
    } catch {
      // Not every browser supports every action. Skipping one is harmless.
    }
  }
}

/// Keeps the lock screen scrubber in sync with the audio element.
function updatePositionState() {
  if (!("mediaSession" in navigator) || !navigator.mediaSession.setPositionState) {
    return;
  }

  if (!Number.isFinite(audio.duration) || audio.duration <= 0) {
    return;
  }

  try {
    navigator.mediaSession.setPositionState({
      duration: audio.duration,
      playbackRate: audio.playbackRate,
      position: Math.min(audio.currentTime, audio.duration),
    });
  } catch {
    // Ignore transient states where position briefly exceeds duration.
  }
}

/// Invokes a .NET callback, ignoring errors from a disposed component.
function notify(method, ...args) {
  if (!dotNet) {
    return;
  }

  dotNet.invokeMethodAsync(method, ...args).catch(() => {});
}

/// Converts a MediaError code into a readable message.
function describeError(error) {
  if (!error) {
    return "Unknown playback error.";
  }

  switch (error.code) {
    case MediaError.MEDIA_ERR_ABORTED:
      return "Playback was aborted.";
    case MediaError.MEDIA_ERR_NETWORK:
      return "Network error while loading audio.";
    case MediaError.MEDIA_ERR_DECODE:
      return "The audio could not be decoded.";
    case MediaError.MEDIA_ERR_SRC_NOT_SUPPORTED:
      return "This audio format is not supported by the browser.";
    default:
      return "Unknown playback error.";
  }
}
