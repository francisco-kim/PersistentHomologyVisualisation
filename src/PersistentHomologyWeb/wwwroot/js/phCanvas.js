// Canvas glue for the persistent homology simulator.
// Loaded both via JSHost.ImportAsync (for [JSImport] fast-path rendering) and
// via IJSRuntime module import (for the requestAnimationFrame loop); ES modules
// are singletons per URL, so both share this state.

const canvases = new Map();

function cssVar(name, fallback) {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}

export function initCanvas(id, width, height) {
  const canvas = document.getElementById(id);
  if (!canvas) {
    return;
  }
  canvas.width = width;
  canvas.height = height;
  canvases.set(id, { ctx: canvas.getContext('2d') });
}

export function clearCanvas(id) {
  const state = canvases.get(id);
  if (state) {
    state.ctx.clearRect(0, 0, state.ctx.canvas.width, state.ctx.canvas.height);
  }
}

export function getClientSize(id) {
  const canvas = document.getElementById(id);
  return canvas ? canvas.clientWidth : 0;
}

// --- complex layer: balls, filled triangles, edges (bottom to top) ---

export function drawComplex(id, pointXyView, edgePairsView, edgeCount, triTriplesView, triCount, epsilon, showBalls) {
  const state = canvases.get(id);
  if (!state) {
    return;
  }
  // MemoryViews are only valid synchronously inside this call; copy out first.
  const pointXy = pointXyView.slice();
  const edgePairs = edgePairsView.slice();
  const triTriples = triTriplesView.slice();

  const ctx = state.ctx;
  const w = ctx.canvas.width;
  const h = ctx.canvas.height;
  ctx.clearRect(0, 0, w, h);

  if (showBalls && epsilon > 0) {
    ctx.fillStyle = cssVar('--series-1', '#2a78d6');
    ctx.globalAlpha = 0.12;
    const r = epsilon / 2;
    for (let i = 0; i < pointXy.length; i += 2) {
      ctx.beginPath();
      ctx.arc(pointXy[i], pointXy[i + 1], r, 0, 2 * Math.PI);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }

  ctx.fillStyle = cssVar('--series-2', '#1baf7a');
  ctx.globalAlpha = 0.25;
  for (let i = 0; i < triCount * 3; i += 3) {
    const a = triTriples[i], b = triTriples[i + 1], c = triTriples[i + 2];
    ctx.beginPath();
    ctx.moveTo(pointXy[2 * a], pointXy[2 * a + 1]);
    ctx.lineTo(pointXy[2 * b], pointXy[2 * b + 1]);
    ctx.lineTo(pointXy[2 * c], pointXy[2 * c + 1]);
    ctx.closePath();
    ctx.fill();
  }
  ctx.globalAlpha = 1;

  ctx.strokeStyle = cssVar('--text-secondary', '#52514e');
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  for (let i = 0; i < edgeCount * 2; i += 2) {
    const a = edgePairs[i], b = edgePairs[i + 1];
    ctx.moveTo(pointXy[2 * a], pointXy[2 * a + 1]);
    ctx.lineTo(pointXy[2 * b], pointXy[2 * b + 1]);
  }
  ctx.stroke();
}

// --- points layer ---

export function drawPoints(id, pointXyView) {
  const state = canvases.get(id);
  if (!state) {
    return;
  }
  const pointXy = pointXyView.slice();
  const ctx = state.ctx;
  ctx.clearRect(0, 0, ctx.canvas.width, ctx.canvas.height);
  ctx.fillStyle = cssVar('--text-primary', '#0b0b0b');
  for (let i = 0; i < pointXy.length; i += 2) {
    ctx.beginPath();
    ctx.arc(pointXy[i], pointXy[i + 1], 4, 0, 2 * Math.PI);
    ctx.fill();
  }
}

// --- highlight overlay: hovered bar's representative cycle or cluster ---

export function drawHighlight(id, pointXyView, verticesView, edgePairsView) {
  const state = canvases.get(id);
  if (!state) {
    return;
  }
  const pointXy = pointXyView.slice();
  const vertices = verticesView.slice();
  const edgePairs = edgePairsView.slice();

  const ctx = state.ctx;
  ctx.clearRect(0, 0, ctx.canvas.width, ctx.canvas.height);
  const highlight = cssVar('--series-8', '#eb6834');
  const witnessColour = cssVar('--series-2', '#1baf7a');

  ctx.strokeStyle = highlight;
  ctx.lineWidth = 3;
  ctx.beginPath();
  for (let i = 0; i < edgePairs.length; i += 2) {
    const a = edgePairs[i], b = edgePairs[i + 1];
    ctx.moveTo(pointXy[2 * a], pointXy[2 * a + 1]);
    ctx.lineTo(pointXy[2 * b], pointXy[2 * b + 1]);
  }
  ctx.stroke();

  // H0 pairs ring every point of the dying component in the highlight
  // colour; the point the merge edge reaches into - in the other,
  // surviving component - rings in a different colour, so it reads as
  // "why it died" rather than "what died". H1 pairs carry no vertices
  // (the loop's edges are the whole representative), so this is skipped.
  ctx.lineWidth = 2.5;
  const members = new Set(vertices);
  ctx.strokeStyle = highlight;
  for (const v of members) {
    ctx.beginPath();
    ctx.arc(pointXy[2 * v], pointXy[2 * v + 1], 7, 0, 2 * Math.PI);
    ctx.stroke();
  }
  if (members.size > 0) {
    ctx.strokeStyle = witnessColour;
    for (let i = 0; i < edgePairs.length; i++) {
      const v = edgePairs[i];
      if (members.has(v)) continue;
      ctx.beginPath();
      ctx.arc(pointXy[2 * v], pointXy[2 * v + 1], 7, 0, 2 * Math.PI);
      ctx.stroke();
    }
  }
}

// --- requestAnimationFrame driver (play button, Phase 5) ---

let running = false;

export function startLoop(dotNetRef) {
  if (running) {
    return;
  }
  running = true;
  const frame = async () => {
    if (!running) {
      return;
    }
    try {
      await dotNetRef.invokeMethodAsync('OnAnimationFrame');
    } catch {
      running = false;
      return;
    }
    if (running) {
      requestAnimationFrame(frame);
    }
  };
  requestAnimationFrame(frame);
}

export function stopLoop() {
  running = false;
}
