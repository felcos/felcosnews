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
        constructor(canvasId, events, sections) {
            this.canvasId = canvasId;
            this.canvas = document.getElementById(canvasId);
            this.ctx = this.canvas.getContext('2d');
            this.events = events || [];
            this.sections = sections || [];

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
            const pad = 420;
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
            if (n === 2) return [{ x: -400, y: 0 }, { x: 400, y: 0 }];
            if (n === 3) {
                return [
                    { x: 0, y: -350 },
                    { x: -400, y: 280 },
                    { x: 400, y: 280 }
                ];
            }
            if (n <= 6) {
                // Two rows
                const cols = Math.ceil(n / 2);
                const rows = 2;
                const spacingX = 820;
                const spacingY = 700;
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
            // 7+: ellipse
            const rx = 520, ry = 380;
            // Scale up for many systems
            const scale = 1 + (n - 7) * 0.12;
            return Array.from({ length: n }, (_, i) => {
                const angle = (i / n) * Math.PI * 2 - Math.PI / 2;
                return {
                    x: Math.cos(angle) * rx * scale * 1.8,
                    y: Math.sin(angle) * ry * scale * 1.8
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

            // Ring layout per system
            const rings = [
                { count: 5, radius: 150 },
                { count: 7, radius: 265 },
                { count: 9, radius: 370 }
            ];
            // Angular stagger between rings
            const ringStagger = [0, Math.PI / rings[1].count, Math.PI / rings[2].count / 1.5];

            for (const sys of this.systems) {
                const evList = groups[sys.slug] || [];
                let ringIdx = 0;
                let posInRing = 0;

                for (let i = 0; i < evList.length; i++) {
                    const ev = evList[i];
                    // Find current ring
                    while (ringIdx < rings.length - 1 && posInRing >= rings[ringIdx].count) {
                        ringIdx++;
                        posInRing = 0;
                    }
                    const ring = rings[Math.min(ringIdx, rings.length - 1)];
                    const totalInRing = Math.min(ring.count, evList.length - (
                        rings.slice(0, Math.min(ringIdx, rings.length - 1)).reduce((s, r) => s + r.count, 0)
                    ));
                    const angle = (posInRing / totalInRing) * Math.PI * 2 + ringStagger[Math.min(ringIdx, ringStagger.length - 1)];
                    const r = ring.radius;

                    const node = {
                        id: ev.id,
                        x: sys.x + Math.cos(angle) * r,
                        y: sys.y + Math.sin(angle) * r,
                        radius: this._planetRadius(ev),
                        event: ev,
                        system: sys,
                        pulsePhase: Math.random() * Math.PI * 2,
                        color: sys.color,
                        isModuleMatch: false,
                        frozenMoonAngles: {}
                    };

                    this.nodes.push(node);
                    posInRing++;
                    if (posInRing >= ring.count) {
                        ringIdx++;
                        posInRing = 0;
                    }
                }
            }

            // Apply current keywords
            if (this._userKeywords.length > 0) {
                this._applyKeywords();
            }
        }

        _planetRadius(ev) {
            const bonus = { Critical: 14, High: 9, Medium: 4, Low: 0 };
            const b = bonus[ev.priority] || 0;
            const impact = Math.min(1, Math.max(0, (ev.impactScore || 0) / 100));
            return Math.min(46, 18 + b + impact * 14);
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
                        // Open article
                        const art = moons[mi];
                        if (art && art.id) {
                            window.open(`/article/${art.id}`, '_blank');
                        }
                        return;
                    }
                }
            }

            // Check planet clicks
            for (const node of this.nodes) {
                const dist = Math.hypot(wpt.x - node.x, wpt.y - node.y);
                if (dist < node.radius + 4) {
                    this._openCallout(node, sx, sy);
                    return;
                }
            }

            // Click on empty: close callout
            if (this._callout) {
                this._closeCallout();
            }
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
                    <a href="/article/${m.id}" target="_blank" style="color:#8892a4;text-decoration:none;" onmouseover="this.style.color='#4a90e2'" onmouseout="this.style.color='#8892a4'">
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
                    <a href="/situation/${ev.id}" style="font-size:12px;color:#4a90e2;text-decoration:none;">Ver cronología →</a>
                </div>
            `;

            this.canvas.parentElement.appendChild(box);
            this._calloutBox = box;

            document.getElementById('_univ_callout_close')?.addEventListener('click', () => this._closeCallout());
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

        _drawSystemOrbits(ctx) {
            const rings = [150, 265, 370];
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
            const halo = ctx.createRadialGradient(sys.x, sys.y, r * 0.5, sys.x, sys.y, r * 3.5);
            halo.addColorStop(0, color.replace('#', 'rgba(') + ',0.12)'.replace('rgba(', 'rgba(') );
            halo.addColorStop(1, 'rgba(0,0,0,0)');
            // Build proper rgba
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

        _drawNode(ctx, node, ts) {
            const { x, y, radius: dr, color, event: ev } = node;
            const rgb = this._hexToRgb(color);
            const pulse = 0.9 + 0.1 * Math.sin(ts * 1.8 + node.pulsePhase);

            // Glow
            const glow = ctx.createRadialGradient(x, y, 0, x, y, dr * 2.8);
            glow.addColorStop(0, `rgba(${rgb},0.25)`);
            glow.addColorStop(1, 'rgba(0,0,0,0)');
            ctx.fillStyle = glow;
            ctx.beginPath();
            ctx.arc(x, y, dr * 2.8, 0, Math.PI * 2);
            ctx.fill();

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
            const grad = ctx.createRadialGradient(x - dr * 0.3, y - dr * 0.3, 0, x, y, dr * pulse);
            grad.addColorStop(0, '#ffffff');
            grad.addColorStop(0.2, color);
            grad.addColorStop(1, `rgba(${rgb},0.5)`);
            ctx.fillStyle = grad;
            ctx.beginPath();
            ctx.arc(x, y, dr * pulse, 0, Math.PI * 2);
            ctx.fill();

            // Moons
            const moons = (ev.articles || []).slice(0, 5);
            for (let mi = 0; mi < moons.length; mi++) {
                const frozen = node.frozenMoonAngles[mi];
                const angle = frozen !== undefined ? frozen : (ts * 0.4 + (mi / moons.length) * Math.PI * 2 + node.pulsePhase);
                const moonR = dr * 1.9 + mi * 12;
                const mx = x + Math.cos(angle) * moonR;
                const my = y + Math.sin(angle) * moonR;
                const moonSize = 3.5;

                ctx.fillStyle = `rgba(${rgb},0.7)`;
                ctx.beginPath();
                ctx.arc(mx, my, moonSize, 0, Math.PI * 2);
                ctx.fill();

                // Moon orbit trail (faint)
                ctx.strokeStyle = `rgba(${rgb},0.07)`;
                ctx.lineWidth = 1;
                ctx.beginPath();
                ctx.arc(x, y, moonR, 0, Math.PI * 2);
                ctx.stroke();
            }

            // Label
            if (dr * this.cam.scale >= 14) {
                const fontSize = Math.max(9, Math.round(11 / this.cam.scale));
                ctx.save();
                ctx.font = `${fontSize}px 'Segoe UI', system-ui, sans-serif`;
                ctx.textAlign = 'center';
                ctx.fillStyle = 'rgba(232,234,240,0.85)';
                ctx.shadowColor = 'rgba(0,0,0,0.8)';
                ctx.shadowBlur = 4 / this.cam.scale;
                const label = ev.title ? ev.title.substring(0, 28) + (ev.title.length > 28 ? '…' : '') : '';
                ctx.fillText(label, x, y + dr + 14 / this.cam.scale);
                ctx.restore();
            }
        }

        _drawConnections(ctx) {
            const maxDist = 280;
            ctx.save();
            ctx.setLineDash([3, 6]);
            for (let i = 0; i < this.nodes.length; i++) {
                for (let j = i + 1; j < this.nodes.length; j++) {
                    const a = this.nodes[i], b = this.nodes[j];
                    if (a.system !== b.system) continue;
                    const d = Math.hypot(a.x - b.x, a.y - b.y);
                    if (d > maxDist) continue;
                    const alpha = (1 - d / maxDist) * 0.12;
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
            const dt = Math.min(0.05, (timestamp - this._lastTs) / 1000);
            this._lastTs = timestamp;
            const ts = timestamp / 1000;

            const ctx = this.ctx;
            const W = this.W, H = this.H;

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

            // 6. System orbit rings (world)
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

            // 10. Callout line (screen space, resets transform internally)
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

            this._raf = requestAnimationFrame(ts => this._loop(ts));
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

        init(canvasId, events, sections) {
            if (_engine) {
                _engine.destroy();
                _engine = null;
            }
            try {
                _engine = new UniverseEngine(canvasId, events, sections);
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
