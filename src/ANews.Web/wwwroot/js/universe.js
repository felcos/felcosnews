/* ============================================================
   universe.js  v4 — Solar System Universe for AgenteNews
   Solar-system layout: sections = stars, events = planets
   Camera: smooth lerp pan/zoom, drag, double-click, pinch
   ============================================================ */
(function () {
    'use strict';

    // ─────────────────────────────────────────────────────────────
    //  ENGINE CLASS
    // ─────────────────────────────────────────────────────────────
    class UniverseEngine {
        constructor(canvas, events, sections, dotnetRef) {
            this.canvas = canvas;
            this.ctx = this.canvas.getContext('2d');
            this.events = events || [];
            this.sections = sections || [];
            this._dotnet = dotnetRef || null;

            // Camera state
            this.cam = { x: 0, y: 0, scale: 1, tx: 0, ty: 0, ts: 1 };
            this._cameraSnapped = false;

            // Data
            this.systems = [];
            this.nodes = [];

            // Interaction
            this._dragging = false;
            this._dragStart = { x: 0, y: 0 };
            this._dragCamStart = { x: 0, y: 0 };
            this._didDrag = false;
            this._mouseWorld = { x: 0, y: 0 };
            this._mouseScreen = { x: 0, y: 0 };

            // Callout
            this._callout = null;
            this._calloutProgress = 0;
            this._calloutBox = null;

            // Moon state
            this._frozenMoons = {};   // nodeId -> frozenAngle map
            this._hoveredMoon = null;

            // Background
            this._stars = this._genStars(220);
            this._constellations = this._genConstellations();
            this._shootingStars = [];
            this._nextShoot = Date.now() + 5000;
            this._pendingShootCount = 0;

            // Keywords
            this._userKeywords = [];

            // Pinch
            this._pinchDist = null;

            // RAF
            this._raf = null;
            this._lastTs = 0;

            // Resize observer
            this._ro = new ResizeObserver(() => this._onResize());
            this._ro.observe(this.canvas.parentElement);

            this._onResize();
            this._buildSystems();
            this._buildNodes();
            this._resetCamera();
            this._bindEvents();
            this._createZoomControls();
            this._loop(0);
        }

        // ─────────────────────────────────────────────────────────
        //  RESIZE
        // ─────────────────────────────────────────────────────────
        _onResize() {
            const p = this.canvas.parentElement;
            this.W = this.canvas.width = p.clientWidth || 800;
            this.H = this.canvas.height = p.clientHeight || 600;
        }

        // ─────────────────────────────────────────────────────────
        //  CAMERA HELPERS
        // ─────────────────────────────────────────────────────────
        _worldToScreen(wx, wy) {
            const { x, y, scale } = this.cam;
            return {
                x: this.W / 2 + (wx - x) * scale,
                y: this.H / 2 + (wy - y) * scale
            };
        }

        _screenToWorld(sx, sy) {
            const { x, y, scale } = this.cam;
            return {
                x: x + (sx - this.W / 2) / scale,
                y: y + (sy - this.H / 2) / scale
            };
        }

        _applyTransform(ctx) {
            const { x, y, scale } = this.cam;
            ctx.setTransform(scale, 0, 0, scale, this.W / 2 - x * scale, this.H / 2 - y * scale);
        }

        _animateCam() {
            const c = this.cam;
            const f = 0.12;
            c.x += (c.tx - c.x) * f;
            c.y += (c.ty - c.y) * f;
            c.scale += (c.ts - c.scale) * f;
        }

        _resetCamera() {
            if (this.systems.length === 0) {
                this.cam.tx = this.cam.x = 0;
                this.cam.ty = this.cam.y = 0;
                this.cam.ts = this.cam.scale = 1;
                this._cameraSnapped = true;
                return;
            }
            let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
            for (const s of this.systems) {
                minX = Math.min(minX, s.x); minY = Math.min(minY, s.y);
                maxX = Math.max(maxX, s.x); maxY = Math.max(maxY, s.y);
            }
            // Add ring padding
            const pad = 180;
            minX -= pad; minY -= pad; maxX += pad; maxY += pad;
            const cx = (minX + maxX) / 2;
            const cy = (minY + maxY) / 2;
            const scaleX = (this.W * 0.85) / (maxX - minX || 1);
            const scaleY = (this.H * 0.85) / (maxY - minY || 1);
            const s = Math.min(scaleX, scaleY, 1.6);

            this.cam.tx = cx; this.cam.ty = cy; this.cam.ts = s;
            if (!this._cameraSnapped) {
                this.cam.x = cx; this.cam.y = cy; this.cam.scale = s;
                this._cameraSnapped = true;
            }
        }

        // ─────────────────────────────────────────────────────────
        //  DATA BUILD
        // ─────────────────────────────────────────────────────────
        _buildSystems() {
            // Gather unique section slugs from events
            const slugSet = new Set();
            for (const ev of this.events) {
                if (ev.sectionSlug) slugSet.add(ev.sectionSlug);
            }
            const slugs = [...slugSet];

            this.systems = slugs.map(slug => {
                const secDef = this.sections.find(s => s.slug === slug) || {};
                return {
                    slug,
                    name: secDef.name || slug,
                    color: secDef.color || '#4a90e2',
                    iconClass: secDef.iconClass || 'fa-circle',
                    x: 0, y: 0,
                    pulsePhase: Math.random() * Math.PI * 2
                };
            });

            // Arrange centers
            const n = this.systems.length;
            const positions = this._arrangeSystemCenters(n);
            for (let i = 0; i < this.systems.length; i++) {
                this.systems[i].x = positions[i].x;
                this.systems[i].y = positions[i].y;
            }
        }

        _arrangeSystemCenters(n) {
            if (n === 0) return [];
            if (n === 1) return [{ x: 0, y: 0 }];
            if (n === 2) return [{ x: -260, y: 0 }, { x: 260, y: 0 }];
            if (n === 3) {
                return [
                    { x: 0, y: -230 },
                    { x: -265, y: 180 },
                    { x: 265, y: 180 }
                ];
            }
            if (n <= 6) {
                const cols = Math.ceil(n / 2);
                const spacingX = 480;
                const spacingY = 420;
                const result = [];
                for (let i = 0; i < n; i++) {
                    const row = Math.floor(i / cols);
                    const col = i % cols;
                    const rowCount = (row === 0) ? cols : n - cols;
                    const rowOffset = -(rowCount - 1) * spacingX / 2;
                    result.push({
                        x: rowOffset + col * spacingX,
                        y: (row - 0.5) * spacingY
                    });
                }
                return result;
            }
            // 7+: ellipse — keep systems close enough to be visible at default zoom
            const rx = 300, ry = 210;
            const scale = 1 + (n - 7) * 0.08;
            return Array.from({ length: n }, (_, i) => {
                const angle = (i / n) * Math.PI * 2 - Math.PI / 2;
                return {
                    x: Math.cos(angle) * rx * scale,
                    y: Math.sin(angle) * ry * scale
                };
            });
        }

        _buildNodes() {
            this.nodes = [];

            // Group events by section slug
            const groups = {};
            for (const ev of this.events) {
                const slug = ev.sectionSlug || '__none__';
                if (!groups[slug]) groups[slug] = [];
                groups[slug].push(ev);
            }

            // Sort each group by priority then impact
            const priorityOrder = { Critical: 0, High: 1, Medium: 2, Low: 3 };
            for (const slug of Object.keys(groups)) {
                groups[slug].sort((a, b) => {
                    const pa = priorityOrder[a.priority] ?? 9;
                    const pb = priorityOrder[b.priority] ?? 9;
                    if (pa !== pb) return pa - pb;
                    return (b.impactScore || 0) - (a.impactScore || 0);
                });
            }

            const pColors = { Critical: '#ff0040', High: '#ff6600', Medium: '#e6b800', Low: '#4a90e2' };

            for (const sys of this.systems) {
                const evList = groups[sys.slug] || [];
                if (evList.length === 0) continue;

                // Dynamic rings based on planet count — ensure enough spacing
                const totalPlanets = evList.length;
                const rings = this._computeRings(totalPlanets);
                let evIdx = 0;

                for (let ri = 0; ri < rings.length && evIdx < evList.length; ri++) {
                    const ring = rings[ri];
                    const countInRing = Math.min(ring.count, evList.length - evIdx);
                    const stagger = ri * (Math.PI / (ring.count + 1));

                    for (let pos = 0; pos < countInRing; pos++, evIdx++) {
                        const ev = evList[evIdx];
                        const angle = (pos / countInRing) * Math.PI * 2 + stagger;
                        const pp = (ev.id * 0.6173 + pos * 0.314) % (Math.PI * 2);
                        this.nodes.push({
                            id: ev.id,
                            x: sys.x + Math.cos(angle) * ring.radius,
                            y: sys.y + Math.sin(angle) * ring.radius,
                            radius: this._planetRadius(ev),
                            event: ev,
                            system: sys,
                            pulsePhase: pp,
                            planetType: ev.id % 8,
                            color: pColors[ev.priority] || '#4a90e2',
                            isModuleMatch: false,
                            frozenMoonAngles: {}
                        });
                    }
                }
            }

            // Collision resolution — push overlapping planets apart
            this._resolveCollisions();

            if (this._userKeywords.length > 0) {
                this._applyKeywords();
            }
            console.log(`[universe] built: ${this.systems.length} systems, ${this.nodes.length} nodes`);
        }

        _computeRings(totalPlanets) {
            // Dynamically compute ring layout so planets don't overlap
            const rings = [];
            let remaining = totalPlanets;
            let ringIdx = 0;
            const baseRadius = 100;
            const ringGap = 80;

            while (remaining > 0) {
                const radius = baseRadius + ringIdx * ringGap;
                // How many planets fit at this radius with comfortable spacing
                const circumference = Math.PI * 2 * radius;
                const minSpacing = 55; // minimum px between planet centers
                const maxInRing = Math.max(3, Math.floor(circumference / minSpacing));
                const count = Math.min(maxInRing, remaining);
                rings.push({ count, radius });
                remaining -= count;
                ringIdx++;
            }
            return rings;
        }

        _resolveCollisions() {
            // Simple iterative collision resolution
            const minGap = 8; // minimum px gap between planet edges
            for (let iter = 0; iter < 5; iter++) {
                let moved = false;
                for (let i = 0; i < this.nodes.length; i++) {
                    for (let j = i + 1; j < this.nodes.length; j++) {
                        const a = this.nodes[i], b = this.nodes[j];
                        const dx = b.x - a.x, dy = b.y - a.y;
                        const dist = Math.hypot(dx, dy);
                        const minDist = a.radius + b.radius + minGap;
                        if (dist < minDist && dist > 0.1) {
                            const overlap = (minDist - dist) / 2;
                            const nx = dx / dist, ny = dy / dist;
                            a.x -= nx * overlap;
                            a.y -= ny * overlap;
                            b.x += nx * overlap;
                            b.y += ny * overlap;
                            moved = true;
                        }
                    }
                }
                if (!moved) break;
            }
        }

        _planetRadius(ev) {
            // Bigger planets so text fits inside
            const bonus = { Critical: 16, High: 11, Medium: 6, Low: 2 };
            const b = bonus[ev.priority] || 0;
            const impact = Math.min(1, Math.max(0, (ev.impactScore || 0) / 100));
            return Math.min(52, 24 + b + impact * 16);
        }

        _applyKeywords() {
            const kws = this._userKeywords.map(k => k.toLowerCase());
            for (const node of this.nodes) {
                const ev = node.event;
                const txt = `${ev.title || ''} ${ev.description || ''} ${(ev.tags || []).join(' ')}`.toLowerCase();
                node.isModuleMatch = kws.length > 0 && kws.some(k => txt.includes(k));
            }
        }

        // ─────────────────────────────────────────────────────────
        //  BACKGROUND GENERATION
        // ─────────────────────────────────────────────────────────
        _genStars(count) {
            return Array.from({ length: count }, () => ({
                x: Math.random(),
                y: Math.random(),
                r: Math.random() * 1.6 + 0.2,
                brightness: 0.3 + Math.random() * 0.7,
                twinkleSpeed: 0.5 + Math.random() * 2,
                twinklePhase: Math.random() * Math.PI * 2
            }));
        }

        _genConstellations() {
            // 8 constellations placed at different screen quadrants (relative 0-1)
            return [
                {
                    // Orion-like — top-left
                    cx: 0.12, cy: 0.15,
                    stars: [[0,0],[0.06,0.03],[0.03,0.09],[0.08,0.13],[0,-0.06],[0.12,0.01],[0.05,0.17]],
                    lines: [[0,1],[1,2],[2,3],[0,4],[1,5],[3,6]],
                    drift: { dx: 0.00003, dy: 0.00002 }, ox: 0, oy: 0
                },
                {
                    // Big Dipper-like — top-right
                    cx: 0.82, cy: 0.1,
                    stars: [[0,0],[0.04,0.03],[0.08,0.03],[0.12,0],[0.13,0.07],[0.09,0.1],[0.05,0.1]],
                    lines: [[0,1],[1,2],[2,3],[3,4],[4,5],[5,6],[6,1]],
                    drift: { dx: -0.00002, dy: 0.00003 }, ox: 0, oy: 0
                },
                {
                    // Cross-like — bottom-left
                    cx: 0.08, cy: 0.78,
                    stars: [[0,0],[0.06,0.06],[0.12,0.12],[0.04,0.1],[0.08,0.02]],
                    lines: [[0,2],[1,3],[1,4]],
                    drift: { dx: 0.00004, dy: -0.00003 }, ox: 0, oy: 0
                },
                {
                    // Triangle — bottom-right
                    cx: 0.85, cy: 0.82,
                    stars: [[0,0],[0.1,0],[0.05,0.1]],
                    lines: [[0,1],[1,2],[2,0]],
                    drift: { dx: -0.00003, dy: -0.00002 }, ox: 0, oy: 0
                },
                {
                    // W shape (Cassiopeia) — middle-top
                    cx: 0.45, cy: 0.06,
                    stars: [[0,0],[0.04,0.05],[0.08,0],[0.12,0.05],[0.16,0]],
                    lines: [[0,1],[1,2],[2,3],[3,4]],
                    drift: { dx: 0.00001, dy: 0.00004 }, ox: 0, oy: 0
                },
                {
                    // Scorpius hook — left middle
                    cx: 0.05, cy: 0.45,
                    stars: [[0,0],[0.03,-0.05],[0.06,-0.08],[0.02,0.05],[0.01,0.11],[0.04,0.16],[0.08,0.18]],
                    lines: [[0,1],[1,2],[0,3],[3,4],[4,5],[5,6]],
                    drift: { dx: 0.00005, dy: 0.00001 }, ox: 0, oy: 0
                },
                {
                    // Leo-like — right middle
                    cx: 0.88, cy: 0.48,
                    stars: [[0,0],[0.04,0.04],[0.08,0.02],[0.06,-0.04],[0.02,-0.06],[0.1,0.08],[0.07,0.13]],
                    lines: [[0,1],[1,2],[2,3],[3,4],[4,0],[2,5],[5,6]],
                    drift: { dx: -0.00002, dy: 0.00002 }, ox: 0, oy: 0
                },
                {
                    // Arrow — bottom center
                    cx: 0.48, cy: 0.9,
                    stars: [[0,0],[0.06,0],[0.12,0],[0.1,-0.04],[0.1,0.04]],
                    lines: [[0,1],[1,2],[2,3],[2,4]],
                    drift: { dx: 0.00003, dy: -0.00005 }, ox: 0, oy: 0
                }
            ];
        }

        // ─────────────────────────────────────────────────────────
        //  SHOOTING STARS
        // ─────────────────────────────────────────────────────────
        _spawnShootingStar(W, H) {
            const edge = Math.floor(Math.random() * 3); // 0=top, 1=left, 2=right
            let sx, sy, angle;
            if (edge === 0) { sx = Math.random() * W; sy = -10; angle = Math.PI / 4 + Math.random() * Math.PI / 4; }
            else if (edge === 1) { sx = -10; sy = Math.random() * H * 0.6; angle = Math.random() * Math.PI / 4; }
            else { sx = W + 10; sy = Math.random() * H * 0.6; angle = Math.PI - Math.random() * Math.PI / 4; }
            const speed = 400 + Math.random() * 300;
            this._shootingStars.push({
                x: sx, y: sy,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                len: 80 + Math.random() * 120,
                alpha: 1,
                life: 1
            });
        }

        // ─────────────────────────────────────────────────────────
        //  ZOOM CONTROLS
        // ─────────────────────────────────────────────────────────
        _createZoomControls() {
            const parent = this.canvas.parentElement;
            parent.style.position = 'relative';

            const div = document.createElement('div');
            div.id = 'univ-zoom-ctrl';
            div.style.cssText = `
                position: absolute;
                bottom: 16px;
                right: 16px;
                display: flex;
                flex-direction: column;
                gap: 4px;
                z-index: 10;
            `;

            const btnStyle = `
                width: 32px; height: 32px;
                background: rgba(0,5,17,0.85);
                border: 1px solid rgba(74,144,226,0.4);
                border-radius: 6px;
                color: rgba(74,144,226,0.9);
                font-size: 16px;
                cursor: pointer;
                display: flex; align-items: center; justify-content: center;
                transition: background 0.2s, border-color 0.2s;
                font-family: inherit;
                line-height: 1;
            `;

            const makeBtn = (label, title, fn) => {
                const b = document.createElement('button');
                b.textContent = label;
                b.title = title;
                b.style.cssText = btnStyle;
                b.addEventListener('mouseenter', () => {
                    b.style.background = 'rgba(74,144,226,0.2)';
                    b.style.borderColor = 'rgba(74,144,226,0.6)';
                });
                b.addEventListener('mouseleave', () => {
                    b.style.background = 'rgba(0,5,17,0.85)';
                    b.style.borderColor = 'rgba(74,144,226,0.4)';
                });
                b.addEventListener('click', fn);
                return b;
            };

            div.appendChild(makeBtn('+', 'Acercar', () => this._zoomAt(this.W / 2, this.H / 2, 1.3)));
            div.appendChild(makeBtn('−', 'Alejar', () => this._zoomAt(this.W / 2, this.H / 2, 1 / 1.3)));
            div.appendChild(makeBtn('⊙', 'Restablecer vista', () => this._resetCamera()));

            parent.appendChild(div);
            this._zoomCtrl = div;
        }

        _zoomAt(sx, sy, factor) {
            const worldPt = this._screenToWorld(sx, sy);
            const newScale = Math.min(4, Math.max(0.15, this.cam.ts * factor));
            // Keep worldPt under (sx, sy):
            // sx = W/2 + (worldPt.x - newCx) * newScale
            // newCx = worldPt.x - (sx - W/2) / newScale
            this.cam.tx = worldPt.x - (sx - this.W / 2) / newScale;
            this.cam.ty = worldPt.y - (sy - this.H / 2) / newScale;
            this.cam.ts = newScale;
        }

        // ─────────────────────────────────────────────────────────
        //  EVENT BINDING
        // ─────────────────────────────────────────────────────────
        _bindEvents() {
            const c = this.canvas;

            // Mouse wheel zoom
            c.addEventListener('wheel', e => {
                e.preventDefault();
                const rect = c.getBoundingClientRect();
                const sx = e.clientX - rect.left;
                const sy = e.clientY - rect.top;
                const factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;
                this._zoomAt(sx, sy, factor);
            }, { passive: false });

            // Drag
            c.addEventListener('mousedown', e => {
                if (e.button !== 0) return;
                this._dragging = true;
                this._didDrag = false;
                this._dragStart = { x: e.clientX, y: e.clientY };
                this._dragCamStart = { x: this.cam.tx, y: this.cam.ty };
            });

            window.addEventListener('mousemove', e => {
                if (this._dragging) {
                    const dx = e.clientX - this._dragStart.x;
                    const dy = e.clientY - this._dragStart.y;
                    if (!this._didDrag && (Math.abs(dx) > 5 || Math.abs(dy) > 5)) {
                        this._didDrag = true;
                    }
                    if (this._didDrag) {
                        this.cam.tx = this._dragCamStart.x - dx / this.cam.ts;
                        this.cam.ty = this._dragCamStart.y - dy / this.cam.ts;
                    }
                }
                // Update mouse world position for hover
                const rect = this.canvas.getBoundingClientRect();
                this._mouseScreen = { x: e.clientX - rect.left, y: e.clientY - rect.top };
                this._mouseWorld = this._screenToWorld(this._mouseScreen.x, this._mouseScreen.y);
            });

            window.addEventListener('mouseup', () => {
                this._dragging = false;
            });

            // Click
            c.addEventListener('click', e => {
                if (this._didDrag) return;
                const rect = c.getBoundingClientRect();
                const sx = e.clientX - rect.left;
                const sy = e.clientY - rect.top;
                this._handleClick(sx, sy);
            });

            // Double-click
            c.addEventListener('dblclick', e => {
                const rect = c.getBoundingClientRect();
                const sx = e.clientX - rect.left;
                const sy = e.clientY - rect.top;
                this._handleDblClick(sx, sy);
            });

            // Touch pinch
            c.addEventListener('touchstart', e => {
                if (e.touches.length === 2) {
                    this._pinchDist = this._getTouchDist(e.touches);
                    e.preventDefault();
                }
            }, { passive: false });

            c.addEventListener('touchmove', e => {
                if (e.touches.length === 2 && this._pinchDist !== null) {
                    e.preventDefault();
                    const newDist = this._getTouchDist(e.touches);
                    const factor = newDist / this._pinchDist;
                    const rect = c.getBoundingClientRect();
                    const cx = ((e.touches[0].clientX + e.touches[1].clientX) / 2) - rect.left;
                    const cy = ((e.touches[0].clientY + e.touches[1].clientY) / 2) - rect.top;
                    this._zoomAt(cx, cy, factor);
                    this._pinchDist = newDist;
                }
            }, { passive: false });

            c.addEventListener('touchend', () => { this._pinchDist = null; });
        }

        _getTouchDist(touches) {
            const dx = touches[0].clientX - touches[1].clientX;
            const dy = touches[0].clientY - touches[1].clientY;
            return Math.sqrt(dx * dx + dy * dy);
        }

        _handleDblClick(sx, sy) {
            const wpt = this._screenToWorld(sx, sy);
            // Check if near a system center
            for (const sys of this.systems) {
                const sp = this._worldToScreen(sys.x, sys.y);
                const dist = Math.hypot(sp.x - sx, sp.y - sy);
                if (dist < 80) {
                    this.cam.tx = sys.x;
                    this.cam.ty = sys.y;
                    this.cam.ts = 1.4;
                    return;
                }
            }
            // Check if near a node
            for (const node of this.nodes) {
                const sp = this._worldToScreen(node.x, node.y);
                const dist = Math.hypot(sp.x - sx, sp.y - sy);
                if (dist < node.radius * this.cam.scale + 10) {
                    this.cam.tx = node.x;
                    this.cam.ty = node.y;
                    this.cam.ts = Math.max(this.cam.ts, 1.2);
                    return;
                }
            }
            // Empty area: reset
            this._resetCamera();
        }

        _handleClick(sx, sy) {
            const wpt = this._screenToWorld(sx, sy);
            const ts = Date.now() / 1000;

            // Close callout if clicking in callout box area (handled by HTML events)
            // Close if clicking empty space (after checking planets below)


            // Check moon clicks first
            for (const node of this.nodes) {
                const moons = node.event.articles || [];
                for (let mi = 0; mi < moons.length; mi++) {
                    const frozen = node.frozenMoonAngles[mi];
                    const angle = frozen !== undefined ? frozen : (ts * 0.4 + (mi / moons.length) * Math.PI * 2 + node.pulsePhase);
                    const moonR = node.radius * 1.9 + mi * 12;
                    const mx = node.x + Math.cos(angle) * moonR;
                    const my = node.y + Math.sin(angle) * moonR;
                    const dist = Math.hypot(wpt.x - mx, wpt.y - my);
                    if (dist < 10) {
                        // Freeze/unfreeze
                        if (node.frozenMoonAngles[mi] !== undefined) {
                            delete node.frozenMoonAngles[mi];
                        } else {
                            node.frozenMoonAngles[mi] = angle;
                        }
                        // Open article (same domain = same tab)
                        const art = moons[mi];
                        if (art && art.id) {
                            window.location.href = `/article/${art.id}`;
                        }
                        return;
                    }
                }
            }

            // Check planet clicks
            for (const node of this.nodes) {
                const dist = Math.hypot(wpt.x - node.x, wpt.y - node.y);
                if (dist < node.radius + 4) {
                    if (this._callout && this._callout.node === node) {
                        this._closeCallout();
                    } else {
                        this._openCallout(node);
                    }
                    return;
                }
            }

            // Click on empty space: close callout
            this._closeCallout();
        }

        // ─────────────────────────────────────────────────────────
        //  CALLOUT
        // ─────────────────────────────────────────────────────────
        _openCallout(node, sx, sy) {
            this._callout = { node, anchorWorld: { x: node.x, y: node.y } };
            this._calloutProgress = 0;
            this._calloutBox = null;
            this._buildCalloutHTML(node);
        }

        _closeCallout() {
            this._callout = null;
            this._calloutProgress = 0;
            const box = document.getElementById('_univ_callout_box');
            if (box) box.remove();
            this._calloutBox = null;
        }

        _buildCalloutHTML(node) {
            const existing = document.getElementById('_univ_callout_box');
            if (existing) existing.remove();

            const ev = node.event;
            const box = document.createElement('div');
            box.id = '_univ_callout_box';
            box.style.cssText = `
                position: absolute;
                background: rgba(0,5,17,0.96);
                border: 1px solid rgba(74,144,226,0.5);
                border-radius: 10px;
                padding: 12px 16px;
                max-width: 260px;
                min-width: 180px;
                color: #e8eaf0;
                font-size: 13px;
                pointer-events: auto;
                z-index: 20;
                display: none;
                box-shadow: 0 4px 24px rgba(0,0,0,0.5), 0 0 12px rgba(74,144,226,0.15);
            `;

            const pColor = { Critical: '#ff0040', High: '#ff6600', Medium: '#ffcc00', Low: '#4a90e2' };
            const pc = pColor[ev.priority] || '#4a90e2';

            const tags = (ev.tags || []).slice(0, 3).map(t => `<span style="background:rgba(74,144,226,0.15);padding:1px 5px;border-radius:3px;font-size:11px;">#${t}</span>`).join(' ');
            const moons = (ev.articles || []).slice(0, 5);
            const moonsList = moons.map(m => `
                <div style="padding:4px 0;border-bottom:1px solid rgba(255,255,255,0.06);font-size:11px;color:#8892a4;">
                    <a href="/article/${m.id}" style="color:#8892a4;text-decoration:none;" onmouseover="this.style.color='#4a90e2'" onmouseout="this.style.color='#8892a4'">
                        ${m.title ? m.title.substring(0, 50) + (m.title.length > 50 ? '…' : '') : 'Artículo'}
                    </a>
                </div>
            `).join('');

            box.innerHTML = `
                <div style="display:flex;align-items:center;gap:8px;margin-bottom:8px;">
                    <span style="background:${pc};color:#fff;font-size:10px;font-weight:700;padding:2px 6px;border-radius:4px;">${ev.priority || ''}</span>
                    <span style="color:#8892a4;font-size:11px;">${ev.sectionName || ''}</span>
                    <button id="_univ_callout_close" style="margin-left:auto;background:none;border:none;color:#8892a4;cursor:pointer;font-size:14px;line-height:1;">×</button>
                </div>
                <div style="font-weight:600;margin-bottom:6px;line-height:1.3;">${ev.title || 'Sin título'}</div>
                <div style="color:#8892a4;font-size:12px;margin-bottom:8px;line-height:1.4;">${(ev.description || '').substring(0, 110)}${(ev.description || '').length > 110 ? '…' : ''}</div>
                ${tags ? `<div style="margin-bottom:8px;display:flex;flex-wrap:wrap;gap:3px;">${tags}</div>` : ''}
                <div style="font-size:11px;color:#4a90e2;margin-bottom:4px;">${ev.articleCount || 0} artículos · Impacto: ${(ev.impactScore || 0).toFixed(0)}</div>
                ${moonsList ? `<div style="margin-top:6px;">${moonsList}</div>` : ''}
                <div style="margin-top:10px;text-align:right;">
                    <button id="_univ_callout_modal" style="font-size:12px;color:#fff;background:rgba(74,144,226,0.8);border:none;border-radius:5px;padding:4px 10px;cursor:pointer;text-decoration:none;">Ver cronología →</button>
                </div>
            `;

            this.canvas.parentElement.appendChild(box);
            this._calloutBox = box;

            document.getElementById('_univ_callout_close')?.addEventListener('click', () => this._closeCallout());
            document.getElementById('_univ_callout_modal')?.addEventListener('click', () => {
                if (this._dotnet) {
                    try { this._dotnet.invokeMethodAsync('OpenEventById', ev.id); } catch(e) { console.warn('[universe] OpenEventById failed:', e); }
                }
            });
        }

        _updateCalloutPosition() {
            if (!this._callout || !this._calloutBox) return;
            const node = this._callout.node;
            const sp = this._worldToScreen(node.x, node.y);

            // Elbow line offsets in screen space
            const vLen = -90;
            const hLen = 100;
            const endX = sp.x + hLen;
            const endY = sp.y + vLen;

            // Position box
            const box = this._calloutBox;
            const boxW = 260;
            const boxH = box.offsetHeight || 180;
            let bx = endX + 4;
            let by = endY - boxH / 2;

            // Clamp to canvas
            bx = Math.max(4, Math.min(this.W - boxW - 4, bx));
            by = Math.max(4, Math.min(this.H - boxH - 4, by));

            box.style.left = bx + 'px';
            box.style.top = by + 'px';
            box.style.display = this._calloutProgress >= 1 ? 'block' : 'none';
        }

        _drawCalloutLine(ctx) {
            if (!this._callout) return;
            const node = this._callout.node;
            const sp = this._worldToScreen(node.x, node.y);

            const progress = Math.min(1, this._calloutProgress);
            const vLen = -90;
            const hLen = 100;

            ctx.save();
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            ctx.strokeStyle = `rgba(74,144,226,${0.7 * progress})`;
            ctx.lineWidth = 1.5;
            ctx.setLineDash([4, 3]);
            ctx.beginPath();
            ctx.moveTo(sp.x, sp.y);

            if (progress < 0.5) {
                const t = progress * 2;
                ctx.lineTo(sp.x, sp.y + vLen * t);
            } else {
                const t = (progress - 0.5) * 2;
                ctx.lineTo(sp.x, sp.y + vLen);
                ctx.lineTo(sp.x + hLen * t, sp.y + vLen);
            }
            ctx.stroke();
            ctx.setLineDash([]);
            ctx.restore();

            this._calloutProgress = Math.min(1, this._calloutProgress + 0.05);
        }

        // ─────────────────────────────────────────────────────────
        //  DRAW ROUTINES
        // ─────────────────────────────────────────────────────────
        _drawStarfield(ctx, ts) {
            const W = this.W, H = this.H;
            for (const star of this._stars) {
                const twinkle = 0.5 + 0.5 * Math.sin(ts * star.twinkleSpeed + star.twinklePhase);
                const alpha = star.brightness * (0.4 + 0.6 * twinkle);
                ctx.fillStyle = `rgba(255,255,255,${alpha})`;
                ctx.beginPath();
                ctx.arc(star.x * W, star.y * H, star.r, 0, Math.PI * 2);
                ctx.fill();
            }
        }

        _drawConstellations(ctx, ts) {
            const W = this.W, H = this.H;
            for (const con of this._constellations) {
                // Update drift
                con.ox = (con.ox || 0) + con.drift.dx;
                con.oy = (con.oy || 0) + con.drift.dy;

                const baseX = (con.cx + con.ox) * W;
                const baseY = (con.cy + con.oy) * H;
                const scale = Math.min(W, H) * 0.18;

                const pts = con.stars.map(([rx, ry]) => ({
                    x: baseX + rx * scale,
                    y: baseY + ry * scale
                }));

                // Lines
                ctx.strokeStyle = 'rgba(74,144,226,0.12)';
                ctx.lineWidth = 1;
                ctx.beginPath();
                for (const [a, b] of con.lines) {
                    ctx.moveTo(pts[a].x, pts[a].y);
                    ctx.lineTo(pts[b].x, pts[b].y);
                }
                ctx.stroke();

                // Stars
                for (const pt of pts) {
                    ctx.fillStyle = 'rgba(180,200,255,0.4)';
                    ctx.beginPath();
                    ctx.arc(pt.x, pt.y, 1.5, 0, Math.PI * 2);
                    ctx.fill();
                }
            }
        }

        _drawShootingStars(ctx, dt) {
            for (let i = this._shootingStars.length - 1; i >= 0; i--) {
                const ss = this._shootingStars[i];
                ss.x += ss.vx * dt;
                ss.y += ss.vy * dt;
                ss.life -= dt * 0.7;
                ss.alpha = Math.max(0, ss.life);

                if (ss.life <= 0) { this._shootingStars.splice(i, 1); continue; }

                const len = ss.len;
                const angle = Math.atan2(ss.vy, ss.vx);
                const grad = ctx.createLinearGradient(
                    ss.x - Math.cos(angle) * len, ss.y - Math.sin(angle) * len,
                    ss.x, ss.y
                );
                grad.addColorStop(0, `rgba(255,255,255,0)`);
                grad.addColorStop(1, `rgba(255,255,255,${ss.alpha * 0.9})`);

                ctx.strokeStyle = grad;
                ctx.lineWidth = 1.5;
                ctx.beginPath();
                ctx.moveTo(ss.x - Math.cos(angle) * len, ss.y - Math.sin(angle) * len);
                ctx.lineTo(ss.x, ss.y);
                ctx.stroke();
            }
        }

        _drawGalaxy(ctx, sys, ts) {
            const rgb = this._hexToRgb(sys.color);
            const wobble = Math.sin(ts * 0.07 + sys.pulsePhase) * 12;
            const wobble2 = Math.cos(ts * 0.05 + sys.pulsePhase * 1.3) * 8;

            // Large outer nebula
            const rOuter = 370;
            const gradOuter = ctx.createRadialGradient(
                sys.x + wobble, sys.y + wobble2, 0,
                sys.x, sys.y, rOuter
            );
            gradOuter.addColorStop(0,   `rgba(${rgb},0.09)`);
            gradOuter.addColorStop(0.35, `rgba(${rgb},0.05)`);
            gradOuter.addColorStop(0.7,  `rgba(${rgb},0.02)`);
            gradOuter.addColorStop(1,    `rgba(${rgb},0)`);
            ctx.save();
            ctx.beginPath();
            ctx.ellipse(sys.x, sys.y, rOuter * 1.15, rOuter * 0.82,
                ts * 0.015 + sys.pulsePhase, 0, Math.PI * 2);
            ctx.fillStyle = gradOuter;
            ctx.fill();

            // Inner bright core glow
            const rInner = 120;
            const gradInner = ctx.createRadialGradient(
                sys.x, sys.y, 0,
                sys.x, sys.y, rInner
            );
            gradInner.addColorStop(0,   `rgba(${rgb},0.13)`);
            gradInner.addColorStop(0.5, `rgba(${rgb},0.05)`);
            gradInner.addColorStop(1,   `rgba(${rgb},0)`);
            ctx.beginPath();
            ctx.arc(sys.x, sys.y, rInner, 0, Math.PI * 2);
            ctx.fillStyle = gradInner;
            ctx.fill();
            ctx.restore();
        }

        _drawSystemOrbits(ctx) {
            const rings = [100, 175, 255];
            ctx.save();
            ctx.setLineDash([4, 8]);
            for (const sys of this.systems) {
                for (const r of rings) {
                    ctx.strokeStyle = 'rgba(255,255,255,0.04)';
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.arc(sys.x, sys.y, r, 0, Math.PI * 2);
                    ctx.stroke();
                }
            }
            ctx.setLineDash([]);
            ctx.restore();
        }

        _drawSystemSun(ctx, sys, ts) {
            const pulse = 0.85 + 0.15 * Math.sin(ts * 1.2 + sys.pulsePhase);
            const r = 28 * pulse;
            const color = sys.color || '#4a90e2';

            // Outer halo
            const haloGrad = ctx.createRadialGradient(sys.x, sys.y, r * 0.5, sys.x, sys.y, r * 3.5);
            const rgb = this._hexToRgb(color);
            haloGrad.addColorStop(0, `rgba(${rgb},0.18)`);
            haloGrad.addColorStop(1, `rgba(${rgb},0)`);
            ctx.fillStyle = haloGrad;
            ctx.beginPath();
            ctx.arc(sys.x, sys.y, r * 3.5, 0, Math.PI * 2);
            ctx.fill();

            // Core gradient
            const grad = ctx.createRadialGradient(sys.x - r * 0.25, sys.y - r * 0.25, 0, sys.x, sys.y, r);
            grad.addColorStop(0, '#ffffff');
            grad.addColorStop(0.35, color);
            grad.addColorStop(1, `rgba(${rgb},0.6)`);
            ctx.fillStyle = grad;
            ctx.beginPath();
            ctx.arc(sys.x, sys.y, r, 0, Math.PI * 2);
            ctx.fill();

            // Spike cross
            ctx.save();
            ctx.strokeStyle = `rgba(255,255,255,0.5)`;
            ctx.lineWidth = 1;
            for (let a = 0; a < 4; a++) {
                const ang = (a / 4) * Math.PI * 2 + ts * 0.1;
                ctx.beginPath();
                ctx.moveTo(sys.x + Math.cos(ang) * r, sys.y + Math.sin(ang) * r);
                ctx.lineTo(sys.x + Math.cos(ang) * (r + 18 * pulse), sys.y + Math.sin(ang) * (r + 18 * pulse));
                ctx.stroke();
            }
            ctx.restore();

            // Name label (only when zoomed in enough)
            if (this.cam.scale >= 0.4) {
                const fontSize = Math.round(13 / this.cam.scale);
                ctx.save();
                ctx.font = `600 ${fontSize}px 'Segoe UI', system-ui, sans-serif`;
                ctx.textAlign = 'center';
                ctx.fillStyle = `rgba(${rgb},0.9)`;
                ctx.shadowColor = `rgba(${rgb},0.5)`;
                ctx.shadowBlur = 8 / this.cam.scale;
                ctx.fillText(sys.name, sys.x, sys.y + r + 22 / this.cam.scale);
                ctx.restore();
            }
        }

        _isNodeVisible(node) {
            // Frustum culling — skip off-screen nodes
            const s = this._worldToScreen(node.x, node.y);
            const margin = node.radius * this.cam.scale + 80;
            return s.x > -margin && s.x < this.W + margin &&
                   s.y > -margin && s.y < this.H + margin;
        }

        _drawNode(ctx, node, ts) {
            if (!this._isNodeVisible(node)) return;

            const { x, y, radius: dr, color, event: ev } = node;
            const rgb = this._hexToRgb(color);
            const pulse = 0.9 + 0.1 * Math.sin(ts * 1.8 + node.pulsePhase);
            const screenR = dr * this.cam.scale;

            // Performance: simplified rendering at small sizes
            const isSmall = screenR < 10;

            // Glow (skip for tiny planets)
            if (!isSmall) {
                const glow = ctx.createRadialGradient(x, y, 0, x, y, dr * 2.2);
                glow.addColorStop(0, `rgba(${rgb},0.2)`);
                glow.addColorStop(1, 'rgba(0,0,0,0)');
                ctx.fillStyle = glow;
                ctx.beginPath();
                ctx.arc(x, y, dr * 2.2, 0, Math.PI * 2);
                ctx.fill();
            }

            // Critical ring
            if (ev.priority === 'Critical') {
                const ringPulse = 0.5 + 0.5 * Math.sin(ts * 3 + node.pulsePhase);
                ctx.strokeStyle = `rgba(255,0,64,${0.4 + 0.4 * ringPulse})`;
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.arc(x, y, dr + 5 + 3 * ringPulse, 0, Math.PI * 2);
                ctx.stroke();
            }

            // Module match ring
            if (node.isModuleMatch) {
                ctx.strokeStyle = `rgba(0,255,136,0.7)`;
                ctx.lineWidth = 2.5;
                ctx.setLineDash([4, 3]);
                ctx.beginPath();
                ctx.arc(x, y, dr + 8, 0, Math.PI * 2);
                ctx.stroke();
                ctx.setLineDash([]);
            }

            // Planet core
            if (isSmall) {
                // Simple circle for tiny planets
                ctx.fillStyle = color;
                ctx.beginPath();
                ctx.arc(x, y, dr * pulse, 0, Math.PI * 2);
                ctx.fill();
            } else {
                this._drawPlanetSurface(ctx, x, y, dr * pulse, color, rgb, ts, node);
            }

            // Moons — only draw when zoomed in enough
            if (screenR >= 18) {
                const moons = (ev.articles || []).slice(0, 4);
                for (let mi = 0; mi < moons.length; mi++) {
                    const frozen = node.frozenMoonAngles[mi];
                    const angle = frozen !== undefined ? frozen : (ts * 0.4 + (mi / moons.length) * Math.PI * 2 + node.pulsePhase);
                    const moonR = dr * 1.9 + mi * 12;
                    const mx = x + Math.cos(angle) * moonR;
                    const my = y + Math.sin(angle) * moonR;
                    ctx.fillStyle = `rgba(${rgb},0.7)`;
                    ctx.beginPath();
                    ctx.arc(mx, my, 3, 0, Math.PI * 2);
                    ctx.fill();
                }
            }

            // Text INSIDE the planet (multi-line, centered)
            if (screenR >= 14 && ev.title) {
                this._drawPlanetLabel(ctx, x, y, dr * pulse, ev.title);
            }
        }

        _drawPlanetLabel(ctx, cx, cy, r, title) {
            ctx.save();
            const innerR = r * 0.78; // usable text area
            const maxWidth = innerR * 1.6;
            const fontSize = Math.max(7, Math.min(11, r * 0.38));
            ctx.font = `600 ${fontSize}px system-ui`;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillStyle = 'rgba(255,255,255,0.95)';

            // Word-wrap into lines that fit inside the planet
            const words = title.split(' ');
            const lines = [];
            let currentLine = '';
            for (const word of words) {
                const testLine = currentLine ? currentLine + ' ' + word : word;
                if (ctx.measureText(testLine).width > maxWidth && currentLine) {
                    lines.push(currentLine);
                    currentLine = word;
                } else {
                    currentLine = testLine;
                }
            }
            if (currentLine) lines.push(currentLine);

            // Limit to 3 lines max
            const maxLines = 3;
            if (lines.length > maxLines) {
                lines.length = maxLines;
                lines[maxLines - 1] = lines[maxLines - 1].slice(0, -1) + '…';
            }

            const lineHeight = fontSize + 2;
            const totalHeight = lines.length * lineHeight;
            const startY = cy - totalHeight / 2 + lineHeight / 2;

            // Draw semi-transparent overlay for readability
            ctx.fillStyle = 'rgba(0,0,0,0.4)';
            ctx.beginPath();
            ctx.arc(cx, cy, r, 0, Math.PI * 2);
            ctx.fill();

            // Draw text lines
            ctx.fillStyle = 'rgba(255,255,255,0.95)';
            for (let i = 0; i < lines.length; i++) {
                ctx.fillText(lines[i], cx, startY + i * lineHeight);
            }
            ctx.restore();
        }

        // ─────────────────────────────────────────────────────────
        //  PLANET SURFACE RENDERING
        // ─────────────────────────────────────────────────────────
        _drawPlanetSurface(ctx, x, y, r, color, rgb, ts, node) {
            const type = node.planetType;
            const seed = node.pulsePhase; // deterministic per planet

            ctx.save();
            // Clip to planet circle
            ctx.beginPath();
            ctx.arc(x, y, r, 0, Math.PI * 2);
            ctx.clip();

            // Base sphere — no white highlight, use the color's own luminosity
            const lx = x - r * 0.35, ly = y - r * 0.35;
            const base = ctx.createRadialGradient(lx, ly, 0, x, y, r);

            switch (type) {
                case 0: // Rocky — grey-brown, rough
                    base.addColorStop(0, this._mix(color, '#c0a080', 0.4));
                    base.addColorStop(0.5, color);
                    base.addColorStop(1, this._mix(color, '#000', 0.65));
                    ctx.fillStyle = base; ctx.fillRect(x - r, y - r, r * 2, r * 2);
                    // Craters
                    ctx.globalAlpha = 0.25;
                    for (let i = 0; i < 5; i++) {
                        const ca = seed + i * 1.3;
                        const cr = r * (0.12 + (i * 0.09) % 0.2);
                        const cx2 = x + Math.cos(ca) * r * 0.45;
                        const cy2 = y + Math.sin(ca) * r * 0.45;
                        ctx.strokeStyle = '#000'; ctx.lineWidth = cr * 0.4;
                        ctx.beginPath(); ctx.arc(cx2, cy2, cr, 0, Math.PI * 2); ctx.stroke();
                        ctx.fillStyle = 'rgba(0,0,0,0.3)';
                        ctx.beginPath(); ctx.arc(cx2, cy2, cr, 0, Math.PI * 2); ctx.fill();
                    }
                    ctx.globalAlpha = 1;
                    break;

                case 1: // Gas giant — Jupiter bands
                    base.addColorStop(0, this._mix(color, '#fff', 0.3));
                    base.addColorStop(0.6, color);
                    base.addColorStop(1, this._mix(color, '#000', 0.7));
                    ctx.fillStyle = base; ctx.fillRect(x - r, y - r, r * 2, r * 2);
                    // Horizontal bands
                    const bandCount = 5 + (seed | 0) % 4;
                    ctx.globalAlpha = 0.22;
                    for (let b = 0; b < bandCount; b++) {
                        const by2 = y - r + (b / bandCount) * r * 2;
                        const bh = r * (0.08 + (b % 3) * 0.05);
                        ctx.fillStyle = b % 2 === 0 ? 'rgba(0,0,0,0.5)' : 'rgba(255,255,255,0.3)';
                        ctx.fillRect(x - r, by2, r * 2, bh);
                    }
                    ctx.globalAlpha = 1;
                    break;

                case 2: // Ice planet — pale with polar caps
                    base.addColorStop(0, '#d8eeff');
                    base.addColorStop(0.4, this._mix(color, '#aaccff', 0.7));
                    base.addColorStop(1, this._mix(color, '#000033', 0.6));
                    ctx.fillStyle = base; ctx.fillRect(x - r, y - r, r * 2, r * 2);
                    // Polar ice caps
                    ctx.globalAlpha = 0.5;
                    ctx.fillStyle = 'rgba(220,240,255,0.8)';
                    ctx.beginPath(); ctx.ellipse(x, y - r * 0.7, r * 0.5, r * 0.35, 0, 0, Math.PI * 2); ctx.fill();
                    ctx.beginPath(); ctx.ellipse(x, y + r * 0.75, r * 0.35, r * 0.22, 0, 0, Math.PI * 2); ctx.fill();
                    ctx.globalAlpha = 1;
                    break;

                case 3: // Desert — reddish, sandy with wind arcs
                    base.addColorStop(0, this._mix(color, '#ffaa44', 0.5));
                    base.addColorStop(0.5, this._mix(color, '#cc6633', 0.4));
                    base.addColorStop(1, this._mix(color, '#220000', 0.7));
                    ctx.fillStyle = base; ctx.fillRect(x - r, y - r, r * 2, r * 2);
                    ctx.globalAlpha = 0.15;
                    for (let i = 0; i < 4; i++) {
                        ctx.strokeStyle = 'rgba(255,180,80,0.8)';
                        ctx.lineWidth = r * 0.06;
                        ctx.beginPath();
                        ctx.arc(x + Math.cos(seed + i) * r * 0.3,
                                y + Math.sin(seed + i) * r * 0.2,
                                r * (0.3 + i * 0.1), 0.1, 1.5);
                        ctx.stroke();
                    }
                    ctx.globalAlpha = 1;
                    break;

                case 4: // Ocean — deep blue swirls
                    base.addColorStop(0, '#4488cc');
                    base.addColorStop(0.4, this._mix(color, '#001166', 0.5));
                    base.addColorStop(1, this._mix(color, '#000022', 0.8));
                    ctx.fillStyle = base; ctx.fillRect(x - r, y - r, r * 2, r * 2);
                    // Continent patches
                    ctx.globalAlpha = 0.3;
                    for (let i = 0; i < 3; i++) {
                        const pa = seed + i * 2.1;
                        const pr = r * (0.15 + (i * 0.07) % 0.15);
                        ctx.fillStyle = 'rgba(0,180,80,0.5)';
                        ctx.beginPath();
                        ctx.ellipse(x + Math.cos(pa) * r * 0.5, y + Math.sin(pa) * r * 0.45,
                            pr, pr * 0.7, pa, 0, Math.PI * 2);
                        ctx.fill();
                    }
                    ctx.globalAlpha = 1;
                    break;

                case 5: // Volcanic — dark with lava cracks
                    base.addColorStop(0, this._mix(color, '#331100', 0.3));
                    base.addColorStop(0.5, this._mix(color, '#110000', 0.5));
                    base.addColorStop(1, '#050000');
                    ctx.fillStyle = base; ctx.fillRect(x - r, y - r, r * 2, r * 2);
                    // Lava veins
                    ctx.globalAlpha = 0.6;
                    for (let i = 0; i < 6; i++) {
                        const va = seed + i * 1.05;
                        const grd = ctx.createLinearGradient(
                            x + Math.cos(va) * r * 0.1, y + Math.sin(va) * r * 0.1,
                            x + Math.cos(va) * r * 0.7, y + Math.sin(va) * r * 0.7);
                        grd.addColorStop(0, 'rgba(255,80,0,0.8)');
                        grd.addColorStop(1, 'rgba(255,0,0,0)');
                        ctx.strokeStyle = grd;
                        ctx.lineWidth = r * 0.05;
                        ctx.beginPath();
                        ctx.moveTo(x + Math.cos(va) * r * 0.05, y + Math.sin(va) * r * 0.05);
                        ctx.lineTo(x + Math.cos(va + 0.3) * r * 0.75, y + Math.sin(va + 0.3) * r * 0.75);
                        ctx.stroke();
                    }
                    ctx.globalAlpha = 1;
                    break;

                case 6: // Lush/Jungle — green swirl
                    base.addColorStop(0, this._mix(color, '#44ff88', 0.4));
                    base.addColorStop(0.5, this._mix(color, '#004422', 0.4));
                    base.addColorStop(1, this._mix(color, '#000', 0.7));
                    ctx.fillStyle = base; ctx.fillRect(x - r, y - r, r * 2, r * 2);
                    ctx.globalAlpha = 0.2;
                    for (let i = 0; i < 3; i++) {
                        const sa = seed + i * 2.1 + ts * 0.005;
                        ctx.strokeStyle = 'rgba(0,255,100,0.7)';
                        ctx.lineWidth = r * 0.07;
                        ctx.beginPath();
                        ctx.arc(x + Math.cos(sa) * r * 0.2, y + Math.sin(sa) * r * 0.2,
                            r * (0.3 + i * 0.15), sa, sa + Math.PI * 1.2);
                        ctx.stroke();
                    }
                    ctx.globalAlpha = 1;
                    break;

                case 7: // Ringed (Saturn-style) — drawn AFTER clip restore
                default:
                    base.addColorStop(0, this._mix(color, '#ffeecc', 0.3));
                    base.addColorStop(0.5, color);
                    base.addColorStop(1, this._mix(color, '#000', 0.7));
                    ctx.fillStyle = base; ctx.fillRect(x - r, y - r, r * 2, r * 2);
                    break;
            }

            // Atmosphere rim — subtle edge glow
            const rim = ctx.createRadialGradient(x, y, r * 0.75, x, y, r);
            rim.addColorStop(0, 'rgba(0,0,0,0)');
            rim.addColorStop(1, `rgba(${rgb},0.25)`);
            ctx.fillStyle = rim; ctx.fillRect(x - r, y - r, r * 2, r * 2);

            // Specular highlight — small, subtle, off-center
            const hl = ctx.createRadialGradient(lx + r * 0.1, ly + r * 0.1, 0, lx + r * 0.1, ly + r * 0.1, r * 0.45);
            hl.addColorStop(0, 'rgba(255,255,255,0.28)');
            hl.addColorStop(0.6, 'rgba(255,255,255,0.06)');
            hl.addColorStop(1, 'rgba(255,255,255,0)');
            ctx.fillStyle = hl; ctx.fillRect(x - r, y - r, r * 2, r * 2);

            ctx.restore();

            // Rings drawn OUTSIDE clip (type 7)
            if (type === 7) {
                ctx.save();
                ctx.globalAlpha = 0.35;
                ctx.strokeStyle = `rgba(${rgb},0.8)`;
                for (let ri = 0; ri < 3; ri++) {
                    ctx.lineWidth = 2 - ri * 0.5;
                    ctx.beginPath();
                    ctx.ellipse(x, y, r * (1.6 + ri * 0.25), r * 0.22, node.pulsePhase * 0.3, 0, Math.PI * 2);
                    ctx.stroke();
                }
                ctx.restore();
            }
        }

        _mix(hex, hex2, t) {
            const a = this._hexToRgbArr(hex), b = this._hexToRgbArr(hex2);
            const r = Math.round(a[0] + (b[0]-a[0])*t);
            const g = Math.round(a[1] + (b[1]-a[1])*t);
            const bv = Math.round(a[2] + (b[2]-a[2])*t);
            return `rgb(${r},${g},${bv})`;
        }

        _hexToRgbArr(hex) {
            if (!hex || typeof hex !== 'string') return [74,144,226];
            hex = hex.replace('#','');
            if (hex.length === 3) hex = hex.split('').map(c=>c+c).join('');
            if (hex.startsWith('rgb')) {
                const m = hex.match(/\d+/g); return m ? m.map(Number) : [74,144,226];
            }
            const n = parseInt(hex,16);
            return isNaN(n) ? [74,144,226] : [(n>>16)&255,(n>>8)&255,n&255];
        }

        _drawConnections(ctx) {
            // Skip connections entirely when too many nodes (performance)
            if (this.nodes.length > 100) return;
            const maxDist = 200;
            ctx.save();
            ctx.setLineDash([3, 6]);
            for (let i = 0; i < this.nodes.length; i++) {
                const a = this.nodes[i];
                if (!this._isNodeVisible(a)) continue;
                for (let j = i + 1; j < this.nodes.length; j++) {
                    const b = this.nodes[j];
                    if (a.system !== b.system) continue;
                    const d = Math.hypot(a.x - b.x, a.y - b.y);
                    if (d > maxDist) continue;
                    const alpha = (1 - d / maxDist) * 0.1;
                    ctx.strokeStyle = `rgba(74,144,226,${alpha})`;
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.moveTo(a.x, a.y);
                    ctx.lineTo(b.x, b.y);
                    ctx.stroke();
                }
            }
            ctx.setLineDash([]);
            ctx.restore();
        }

        _drawHUD(ctx) {
            const pct = Math.round(this.cam.scale * 100);
            ctx.save();
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            ctx.font = '11px monospace';
            ctx.fillStyle = 'rgba(74,144,226,0.45)';
            ctx.textAlign = 'right';
            ctx.fillText(`${pct}%`, this.W - 52, this.H - 26);
            ctx.restore();
        }

        // ─────────────────────────────────────────────────────────
        //  MAIN LOOP
        // ─────────────────────────────────────────────────────────
        _loop(timestamp) {
            this._raf = requestAnimationFrame(ts => this._loop(ts));
            const dt = Math.min(0.05, (timestamp - this._lastTs) / 1000);
            this._lastTs = timestamp;
            const ts = timestamp / 1000;

            const ctx = this.ctx;
            const W = this.W, H = this.H;
            try {

            // 1. Clear
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            ctx.fillStyle = '#000511';
            ctx.fillRect(0, 0, W, H);

            // 2. Starfield (screen space)
            this._drawStarfield(ctx, ts);

            // 3. Constellations (screen space)
            this._drawConstellations(ctx, ts);

            // 4. Shooting stars (screen space)
            this._drawShootingStars(ctx, dt);

            // 5. Camera transform
            this._applyTransform(ctx);

            // 6. Galaxy nebula backgrounds (world)
            for (const sys of this.systems) {
                this._drawGalaxy(ctx, sys, ts);
            }

            // 6b. System orbit rings (world)
            this._drawSystemOrbits(ctx);

            // 7. Connections (world)
            this._drawConnections(ctx);

            // 8. System suns (world)
            for (const sys of this.systems) {
                this._drawSystemSun(ctx, sys, ts);
            }

            // 9. Nodes (world)
            for (const node of this.nodes) {
                this._drawNode(ctx, node, ts);
            }

            // 10. Callout line (screen space)
            this._drawCalloutLine(ctx);
            this._updateCalloutPosition();

            // 11. Reset + HUD
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            this._drawHUD(ctx);

            // 12. Animate camera
            this._animateCam();

            // 13. Shooting star spawn
            const now = Date.now();
            if (now >= this._nextShoot) {
                this._spawnShootingStar(W, H);
                this._nextShoot = now + 18000 + Math.random() * 22000;
            }
            if (this._pendingShootCount > 0) {
                this._spawnShootingStar(W, H);
                this._pendingShootCount--;
            }
            } catch (e) { console.error('[universe] frame error:', e); }
        }

        // ─────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────
        updateData(events, sections) {
            const prevCount = this.events.length;
            this.events = events || [];
            this.sections = sections || this.sections;
            this._buildSystems();
            this._buildNodes();

            // Trigger shooting star if new events added
            const added = this.events.length - prevCount;
            if (added > 0) {
                this._pendingShootCount = Math.min(3, added);
            }
        }

        setUserKeywords(keywords) {
            this._userKeywords = (keywords || []).map(k => typeof k === 'string' ? k : String(k));
            this._applyKeywords();
        }

        destroy() {
            if (this._raf) cancelAnimationFrame(this._raf);
            if (this._ro) this._ro.disconnect();
            if (this._zoomCtrl) this._zoomCtrl.remove();
            const box = document.getElementById('_univ_callout_box');
            if (box) box.remove();
        }

        // ─────────────────────────────────────────────────────────
        //  UTILITY
        // ─────────────────────────────────────────────────────────
        _hexToRgb(hex) {
            if (!hex || typeof hex !== 'string') return '74,144,226';
            hex = hex.replace('#', '');
            if (hex.length === 3) hex = hex.split('').map(c => c + c).join('');
            const n = parseInt(hex, 16);
            if (isNaN(n)) return '74,144,226';
            return `${(n >> 16) & 255},${(n >> 8) & 255},${n & 255}`;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  GLOBAL SINGLETON API
    // ─────────────────────────────────────────────────────────────
    let _engine = null;

    window.universe = {
        _engine: null,

        init(canvasId, events, sections, dotnetRef) {
            if (_engine) {
                _engine.destroy();
                _engine = null;
            }
            const canvas = document.getElementById(canvasId);
            if (!canvas) {
                console.warn('[universe.js] canvas not found:', canvasId);
                return;
            }
            console.log(`[universe.js] init: ${(events||[]).length} events, ${(sections||[]).length} sections`);
            try {
                _engine = new UniverseEngine(canvas, events, sections, dotnetRef);
                window.universe._engine = _engine;
            } catch (e) {
                console.error('[universe.js] init error:', e);
            }
        },

        updateData(events, sections) {
            if (_engine) {
                try { _engine.updateData(events, sections); } catch (e) { console.error('[universe.js] updateData error:', e); }
            }
        },

        setUserKeywords(keywords) {
            if (_engine) {
                try { _engine.setUserKeywords(keywords); } catch (e) { console.error('[universe.js] setUserKeywords error:', e); }
            }
        },

        destroy() {
            if (_engine) {
                try { _engine.destroy(); } catch { }
                _engine = null;
                window.universe._engine = null;
            }
        }
    };

})();
