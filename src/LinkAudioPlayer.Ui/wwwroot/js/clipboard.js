// Copies text, falling back when the async clipboard is unavailable.
// navigator.clipboard needs a secure context, which an Android web view serving
// from a custom scheme does not always report, so the textarea path is not dead code.

export async function copy(text) {
  if (!text) {
    return false;
  }

  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    // Fall through and try the older path.
  }

  return legacyCopy(text);
}

/// Selects a hidden textarea and asks the document to copy it.
function legacyCopy(text) {
  const area = document.createElement("textarea");
  area.value = text;
  area.setAttribute("readonly", "");
  area.style.position = "fixed";
  area.style.top = "-1000px";
  area.style.opacity = "0";

  document.body.appendChild(area);
  area.select();

  let copied = false;
  try {
    copied = document.execCommand("copy");
  } catch {
    copied = false;
  }

  document.body.removeChild(area);
  return copied;
}
