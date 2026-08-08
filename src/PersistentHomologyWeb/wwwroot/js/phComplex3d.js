// Canvas glue for the boundary-operator explorer.
//
// Unlike phCanvas.js, this module owns its camera and caches the pushed
// geometry: drag-rotation has to redraw at pointer rate, and routing that
// through .NET every frame would be pointless when the geometry has not
// changed. C# pushes the complex and the highlight; rotation stays here.

const scenes = new Map();

// Camera distance in units of the model radius. Large enough that the
// perspective divide reads as depth without distorting a 5-vertex complex.
const CAMERA_DISTANCE = 4.2;

// Hit radii in CSS pixels. The backing store is sized to the element (see
// resize), so drawing and hit-testing share one coordinate system and these
// mean the same thing on every screen.
const VERTEX_HIT_RADIUS = 11;
const CENTRE_HIT_RADIUS = 11;
const EDGE_HIT_RADIUS = 8;

// Fingers are blunter than cursors and there is no hover to aim with first.
// Kept modest on purpose: every pixel given to vertices and edges is taken
// from the face interiors, which are the only way to select a triangle.
const COARSE_POINTER_BOOST = 1.35;

function cssVar(name, fallback) {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}

// Matches the backing store to the element's own size in device pixels, then
// scales the context so every drawing and hit-testing coordinate is a CSS
// pixel. A fixed backing store would be blurry on a high-DPI screen and, worse,
// would make canvas pixels and CSS pixels differ by a factor of two or more on
// a phone - which silently shrinks every touch target.
function resize(scene) {
  const canvas = scene.ctx.canvas;
  const rect = canvas.getBoundingClientRect();
  const dpr = window.devicePixelRatio || 1;

  const cssWidth = rect.width || scene.fallbackWidth;
  const cssHeight = rect.height || scene.fallbackHeight;

  scene.cssWidth = cssWidth;
  scene.cssHeight = cssHeight;
  canvas.width = Math.round(cssWidth * dpr);
  canvas.height = Math.round(cssHeight * dpr);
  scene.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
}

export function initCanvas(id, width, height) {
  const canvas = document.getElementById(id);
  if (!canvas) {
    return;
  }

  const existing = scenes.get(id);
  existing?.observer?.disconnect();

  const scene = {
    ctx: canvas.getContext('2d'),
    // Only used if the element has not been laid out yet; CSS owns the shape.
    fallbackWidth: width,
    fallbackHeight: height,
    cssWidth: width,
    cssHeight: height,
    observer: null,
    verts: new Float64Array(0),
    edges: new Int32Array(0),
    tris: new Int32Array(0),
    tets: new Int32Array(0),
    solid: false,
    selectedDim: -1,
    selectedIndex: -1,
    relatedDim: -1,
    related: new Int32Array(0),
    // Keep the camera across re-inits so switching preset does not snap the
    // view back to default mid-inspection.
    yaw: existing ? existing.yaw : 0.62,
    pitch: existing ? existing.pitch : 0.34,
    projected: [],
    dotNetRef: null,
    handlers: null
  };
  scenes.set(id, scene);
  resize(scene);

  // Rotation, orientation change, or the layout collapsing to one column all
  // change the element's size without any call from C#.
  if (typeof ResizeObserver !== 'undefined') {
    scene.observer = new ResizeObserver(() => {
      resize(scene);
      render(scene);
    });
    scene.observer.observe(canvas);
  }
}

export function setComplex(id, vertexXyzView, edgePairsView, triTriplesView, tetQuadsView, solid) {
  const scene = scenes.get(id);
  if (!scene) {
    return;
  }
  // MemoryViews are only valid synchronously inside this call; copy out first.
  scene.verts = vertexXyzView.slice();
  scene.edges = edgePairsView.slice();
  scene.tris = triTriplesView.slice();
  scene.tets = tetQuadsView.slice();
  scene.solid = solid;

  // The cached highlight is a (dimension, index) pair into the complex that has
  // just been replaced, so it is meaningless now - index 0 in dimension 3 may
  // denote a different tetrahedron, or none at all. Dropping it here mirrors
  // Recompute() clearing the selection on the C# side, and has to happen before
  // this render: C# pushes the new highlight only after setComplex returns, so
  // the frame drawn here would otherwise index arrays that no longer have the
  // entries it names.
  scene.selectedDim = -1;
  scene.selectedIndex = -1;
  scene.relatedDim = -1;
  scene.related = new Int32Array(0);

  render(scene);
}

export function setHighlight(id, selectedDimension, selectedIndex, relatedDimension, relatedIndicesView) {
  const scene = scenes.get(id);
  if (!scene) {
    return;
  }
  scene.selectedDim = selectedDimension;
  scene.selectedIndex = selectedIndex;
  scene.relatedDim = relatedDimension;
  scene.related = relatedIndicesView.slice();
  render(scene);
}

// --- projection ---

function project(scene) {
  const verts = scene.verts;
  const count = verts.length / 3;
  const width = scene.cssWidth;
  const height = scene.cssHeight;

  // Centre on the model's own centroid rather than the origin: a preset with
  // a limb sticking out (the open triangle's apex) is not symmetric about the
  // origin, and centring on it would park the shape off to one side and
  // shrink it to fit the longer radius.
  let cx = 0, cy = 0, cz = 0;
  for (let i = 0; i < count; i++) {
    cx += verts[3 * i];
    cy += verts[3 * i + 1];
    cz += verts[3 * i + 2];
  }
  cx /= count || 1;
  cy /= count || 1;
  cz /= count || 1;

  let radius = 1e-6;
  for (let i = 0; i < count; i++) {
    radius = Math.max(radius, Math.hypot(verts[3 * i] - cx, verts[3 * i + 1] - cy, verts[3 * i + 2] - cz));
  }

  const cosYaw = Math.cos(scene.yaw), sinYaw = Math.sin(scene.yaw);
  const cosPitch = Math.cos(scene.pitch), sinPitch = Math.sin(scene.pitch);
  const scale = (Math.min(width, height) * 0.38) / radius;

  const projected = new Array(count);
  for (let i = 0; i < count; i++) {
    const x = verts[3 * i] - cx, y = verts[3 * i + 1] - cy, z = verts[3 * i + 2] - cz;

    const rx = (x * cosYaw) + (z * sinYaw);
    const rz = (z * cosYaw) - (x * sinYaw);
    const ry = (y * cosPitch) - (rz * sinPitch);
    const depth = (y * sinPitch) + (rz * cosPitch);

    // Weak perspective: nearer vertices spread out, so the shape reads as 3D.
    const perspective = CAMERA_DISTANCE / (CAMERA_DISTANCE - (depth / radius));
    projected[i] = {
      x: (width / 2) + (rx * scale * perspective),
      y: (height / 2) - (ry * scale * perspective),
      depth
    };
  }
  scene.projected = projected;
  return projected;
}

// --- rendering ---

// Larger is nearer the camera: project() divides by (CAMERA_DISTANCE - depth),
// so a bigger depth spreads a vertex further from the centre.
function triangleDepth(points, a, b, c) {
  return (points[a].depth + points[b].depth + points[c].depth) / 3;
}

function fillTriangle(ctx, points, a, b, c) {
  ctx.beginPath();
  ctx.moveTo(points[a].x, points[a].y);
  ctx.lineTo(points[b].x, points[b].y);
  ctx.lineTo(points[c].x, points[c].y);
  ctx.closePath();
  ctx.fill();
}

function strokeEdge(ctx, points, a, b) {
  ctx.beginPath();
  ctx.moveTo(points[a].x, points[a].y);
  ctx.lineTo(points[b].x, points[b].y);
  ctx.stroke();
}

function tetrahedronCentre(points, quad, index) {
  let x = 0, y = 0;
  for (let v = 0; v < 4; v++) {
    const p = points[quad[4 * index + v]];
    x += p.x;
    y += p.y;
  }
  return { x: x / 4, y: y / 4 };
}

function render(scene) {
  const ctx = scene.ctx;
  const points = project(scene);
  ctx.clearRect(0, 0, scene.cssWidth, scene.cssHeight);
  if (points.length === 0) {
    return;
  }

  const faceColour = cssVar('--series-2', '#1baf7a');
  const edgeColour = cssVar('--text-secondary', '#52514e');
  const vertexColour = cssVar('--text-primary', '#0b0b0b');
  const selectColour = cssVar('--series-8', '#eb6834');
  const relatedColour = cssVar('--series-1', '#2a78d6');
  const surfaceColour = cssVar('--surface-1', '#ffffff');

  // Drawn in index order, deliberately not depth-sorted. Every face is the same
  // colour at the same alpha, and source-over compositing of equal colours is
  // order-independent: a painter's-algorithm sort would cost a sort per frame
  // and produce the identical image. Depth still matters for picking, where the
  // frontmost face has to win - see pick().
  //
  // A filled tetrahedron adds no faces of its own, so nothing above would
  // distinguish it from its own hollow surface - the opacity bump and the
  // barycentre marker below are the only cues that the 3-simplex is present.
  ctx.fillStyle = faceColour;
  ctx.globalAlpha = scene.solid ? 0.3 : 0.16;
  for (let t = 0; t < scene.tris.length / 3; t++) {
    fillTriangle(ctx, points, scene.tris[3 * t], scene.tris[3 * t + 1], scene.tris[3 * t + 2]);
  }
  ctx.globalAlpha = 1;

  ctx.strokeStyle = edgeColour;
  ctx.lineWidth = 1.6;
  for (let e = 0; e < scene.edges.length / 2; e++) {
    strokeEdge(ctx, points, scene.edges[2 * e], scene.edges[2 * e + 1]);
  }

  for (let t = 0; t < scene.tets.length / 4; t++) {
    const centre = tetrahedronCentre(points, scene.tets, t);
    ctx.fillStyle = faceColour;
    ctx.beginPath();
    ctx.arc(centre.x, centre.y, 5, 0, 2 * Math.PI);
    ctx.fill();
    ctx.strokeStyle = surfaceColour;
    ctx.lineWidth = 1.5;
    ctx.stroke();
  }

  drawHighlight(scene, points, selectColour, relatedColour);

  ctx.fillStyle = vertexColour;
  ctx.font = '600 12px system-ui, sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  for (let i = 0; i < points.length; i++) {
    ctx.beginPath();
    ctx.arc(points[i].x, points[i].y, 5.5, 0, 2 * Math.PI);
    ctx.fill();
    ctx.fillStyle = surfaceColour;
    ctx.fillText(String(i), points[i].x, points[i].y + 0.5);
    ctx.fillStyle = vertexColour;
  }
}

// Whether the complex currently held by the scene actually has this simplex.
// setComplex drops the highlight precisely so this is always true, but drawing
// is one push behind C# by construction, and an index that is off the end reads
// as `undefined` rather than throwing - so it lands as `points[undefined].x`
// inside a canvas call, which tears down the whole Blazor render tree over a
// highlight that is merely stale. Skipping the draw degrades to no highlight.
function simplexExists(scene, points, dimension, index) {
  if (index < 0) {
    return false;
  }
  switch (dimension) {
    case 0: return index < points.length;
    case 1: return (2 * index) + 1 < scene.edges.length;
    case 2: return (3 * index) + 2 < scene.tris.length;
    case 3: return (4 * index) + 3 < scene.tets.length;
    default: return false;
  }
}

function drawSimplex(scene, points, dimension, index, colour, emphasis) {
  if (!simplexExists(scene, points, dimension, index)) {
    return;
  }

  const ctx = scene.ctx;
  ctx.strokeStyle = colour;
  ctx.fillStyle = colour;

  if (dimension === 0) {
    ctx.lineWidth = emphasis ? 3 : 2.5;
    ctx.beginPath();
    ctx.arc(points[index].x, points[index].y, emphasis ? 11 : 9, 0, 2 * Math.PI);
    ctx.stroke();
    return;
  }
  if (dimension === 1) {
    ctx.lineWidth = emphasis ? 4.5 : 3;
    strokeEdge(ctx, points, scene.edges[2 * index], scene.edges[2 * index + 1]);
    return;
  }
  if (dimension === 2) {
    // Fill only, no outline: a triangle's outline is its three boundary edges,
    // which are exactly what gets highlighted in the other colour when this
    // triangle is the selection. Stroking them here would hide that.
    const a = scene.tris[3 * index], b = scene.tris[3 * index + 1], c = scene.tris[3 * index + 2];
    ctx.globalAlpha = emphasis ? 0.55 : 0.32;
    fillTriangle(ctx, points, a, b, c);
    ctx.globalAlpha = 1;
    return;
  }
  if (dimension === 3) {
    // Wireframe and centre marker, deliberately not filled. A tetrahedron
    // occupies exactly the same pixels as its own four faces, so filling it
    // would paint over the face highlight - which is the thing a boundary
    // selection is trying to point at.
    const quad = [
      scene.tets[4 * index], scene.tets[4 * index + 1],
      scene.tets[4 * index + 2], scene.tets[4 * index + 3]
    ];
    ctx.lineWidth = emphasis ? 3.5 : 2;
    for (let i = 0; i < 4; i++) {
      for (let j = i + 1; j < 4; j++) {
        strokeEdge(ctx, points, quad[i], quad[j]);
      }
    }
    const centre = tetrahedronCentre(points, scene.tets, index);
    ctx.beginPath();
    ctx.arc(centre.x, centre.y, emphasis ? 9 : 7, 0, 2 * Math.PI);
    ctx.stroke();
  }
}

function drawHighlight(scene, points, selectColour, relatedColour) {
  const drawSelected = () => {
    if (scene.selectedDim >= 0 && scene.selectedIndex >= 0) {
      drawSimplex(scene, points, scene.selectedDim, scene.selectedIndex, selectColour, true);
    }
  };
  const drawRelated = () => {
    if (scene.relatedDim >= 0) {
      for (let i = 0; i < scene.related.length; i++) {
        drawSimplex(scene, points, scene.relatedDim, scene.related[i], relatedColour, false);
      }
    }
  };

  // Related first, so the selection always sits on top of whatever it is being
  // related to. Safe in both directions now that a tetrahedron draws as a
  // wireframe rather than a fill.
  drawRelated();
  drawSelected();
}

// --- pointer: drag to rotate, click to pick ---

function distanceToSegment(px, py, ax, ay, bx, by) {
  const dx = bx - ax;
  const dy = by - ay;
  const lengthSquared = (dx * dx) + (dy * dy);
  if (lengthSquared === 0) {
    return Math.hypot(px - ax, py - ay);
  }
  let t = (((px - ax) * dx) + ((py - ay) * dy)) / lengthSquared;
  t = Math.max(0, Math.min(1, t));
  return Math.hypot(px - (ax + (t * dx)), py - (ay + (t * dy)));
}

function pointInTriangle(px, py, a, b, c) {
  const area = ((b.x - a.x) * (c.y - a.y)) - ((c.x - a.x) * (b.y - a.y));
  if (Math.abs(area) < 1e-9) {
    return false;
  }
  const s = (((b.x - a.x) * (py - a.y)) - ((px - a.x) * (b.y - a.y))) / area;
  const t = (((px - a.x) * (c.y - a.y)) - ((c.x - a.x) * (py - a.y))) / area;
  return s >= 0 && t >= 0 && s + t <= 1;
}

// Highest dimension the pointer is plausibly on, cheapest test first. Vertices
// and tetrahedron markers win over edges, which win over faces, because the
// smaller target is the harder one to hit deliberately.
function pick(scene, px, py, pointerBoost) {
  const points = scene.projected;
  if (!points || points.length === 0) {
    return null;
  }

  const vertexRadius = VERTEX_HIT_RADIUS * pointerBoost;
  const centreRadius = CENTRE_HIT_RADIUS * pointerBoost;
  const edgeRadius = EDGE_HIT_RADIUS * pointerBoost;

  let best = null;
  let bestDistance = vertexRadius;
  for (let i = 0; i < points.length; i++) {
    const d = Math.hypot(px - points[i].x, py - points[i].y);
    if (d <= bestDistance) {
      bestDistance = d;
      best = { dimension: 0, index: i };
    }
  }
  if (best) {
    return best;
  }

  bestDistance = centreRadius;
  for (let t = 0; t < scene.tets.length / 4; t++) {
    const centre = tetrahedronCentre(points, scene.tets, t);
    const d = Math.hypot(px - centre.x, py - centre.y);
    if (d <= bestDistance) {
      bestDistance = d;
      best = { dimension: 3, index: t };
    }
  }
  if (best) {
    return best;
  }

  bestDistance = edgeRadius;
  for (let e = 0; e < scene.edges.length / 2; e++) {
    const a = points[scene.edges[2 * e]];
    const b = points[scene.edges[2 * e + 1]];
    const d = distanceToSegment(px, py, a.x, a.y, b.x, b.y);
    if (d <= bestDistance) {
      bestDistance = d;
      best = { dimension: 1, index: e };
    }
  }
  if (best) {
    return best;
  }

  let frontDepth = -Infinity;
  for (let t = 0; t < scene.tris.length / 3; t++) {
    const a = points[scene.tris[3 * t]];
    const b = points[scene.tris[3 * t + 1]];
    const c = points[scene.tris[3 * t + 2]];
    if (!pointInTriangle(px, py, a, b, c)) {
      continue;
    }
    const depth = triangleDepth(points, scene.tris[3 * t], scene.tris[3 * t + 1], scene.tris[3 * t + 2]);
    if (depth > frontDepth) {
      frontDepth = depth;
      best = { dimension: 2, index: t };
    }
  }
  return best;
}

export function attachPicker(id, dotNetRef) {
  const scene = scenes.get(id);
  const canvas = document.getElementById(id);
  if (!scene || !canvas || scene.handlers) {
    return;
  }
  scene.dotNetRef = dotNetRef;

  let dragging = false;
  let moved = false;
  let lastX = 0;
  let lastY = 0;

  // The context is scaled by the device pixel ratio, so drawn coordinates are
  // CSS pixels and pointer coordinates need no conversion beyond the offset.
  const toCanvas = (event) => {
    const rect = canvas.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  };

  const onPointerDown = (event) => {
    dragging = true;
    moved = false;
    lastX = event.clientX;
    lastY = event.clientY;
    canvas.setPointerCapture(event.pointerId);
  };

  const onPointerMove = (event) => {
    if (!dragging) {
      return;
    }
    const dx = event.clientX - lastX;
    const dy = event.clientY - lastY;
    // A finger wobbles on the way down far more than a mouse does; too tight a
    // threshold turns every tap into a rotation and nothing is ever selected.
    const dragThreshold = event.pointerType === 'mouse' ? 2 : 8;
    if (Math.abs(dx) + Math.abs(dy) > dragThreshold) {
      moved = true;
    }
    lastX = event.clientX;
    lastY = event.clientY;

    scene.yaw += dx * 0.01;
    // Clamped short of the poles: past vertical the model appears to flip,
    // which reads as a bug rather than as rotation.
    scene.pitch = Math.max(-1.4, Math.min(1.4, scene.pitch + (dy * 0.01)));
    render(scene);
  };

  const onPointerUp = (event) => {
    if (!dragging) {
      return;
    }
    dragging = false;
    if (canvas.hasPointerCapture(event.pointerId)) {
      canvas.releasePointerCapture(event.pointerId);
    }
    if (moved) {
      return;
    }
    const { x, y } = toCanvas(event);
    const coarse = event.pointerType !== 'mouse';
    const hit = pick(scene, x, y, coarse ? COARSE_POINTER_BOOST : 1);
    scene.dotNetRef.invokeMethodAsync('OnSimplexPicked', hit ? hit.dimension : -1, hit ? hit.index : -1);
  };

  scene.handlers = { onPointerDown, onPointerMove, onPointerUp };
  canvas.addEventListener('pointerdown', onPointerDown);
  canvas.addEventListener('pointermove', onPointerMove);
  canvas.addEventListener('pointerup', onPointerUp);
  canvas.addEventListener('pointercancel', onPointerUp);
}

export function detachPicker(id) {
  const scene = scenes.get(id);
  if (!scene) {
    return;
  }

  // The observer is torn down whether or not a picker was ever attached: it is
  // created by initCanvas, not by attachPicker, so bailing out early on a
  // missing handler set would leak it for the lifetime of the page.
  scene.observer?.disconnect();
  scene.observer = null;

  if (scene.handlers) {
    const canvas = document.getElementById(id);
    if (canvas) {
      canvas.removeEventListener('pointerdown', scene.handlers.onPointerDown);
      canvas.removeEventListener('pointermove', scene.handlers.onPointerMove);
      canvas.removeEventListener('pointerup', scene.handlers.onPointerUp);
      canvas.removeEventListener('pointercancel', scene.handlers.onPointerUp);
    }
    scene.handlers = null;
    scene.dotNetRef = null;
  }
}

// Re-renders every live scene. Every colour is read from a CSS custom property
// at draw time, so the last frame stays in the old palette until something
// redraws - and nothing otherwise would: the scene only redraws on rotation, on
// resize, or on a push from C#.
export function refresh() {
  for (const scene of scenes.values()) {
    render(scene);
  }
}

window.matchMedia?.('(prefers-color-scheme: dark)').addEventListener('change', refresh);
