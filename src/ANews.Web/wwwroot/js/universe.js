/**
 * AgenteNews Universe Engine v3
 * - Background constellations (animated, very subtle)
 * - Planet labels: fitted inside radius, shadow contrast
 * - Animated callout: dot → vertical line → 90° elbow → horizontal → box
 * - Moon hover: freeze orbit + same callout with article details + link
 * - Module-aware: green dashed ring on matching planets
 */

const CALLOUT_DURATION = 380; // ms for full animation

class NewsUniverseEngine {
    constructor() {
        this.canvas = null;
        this.ctx = null;
        this.events = [];
        this.nodes = [];
        this.animFrame = null;
        this.calloutOverlay = null;
        this.userKeywords = [];

        // Hover state
        this.hoverTarget = null;   // { type:'planet'|'moon', node, moonIdx }
        this.calloutState = null;  // { target, startTs, progress 0→1, leaving bool }

        // Constellation data (generated once per canvas size)
        this._constellations = null;
        this._starfield = null;

        this.config = {
            planetMinR: 20, planetMaxR: 50,
            bgColor: '#000511',
            fps: 60
        };
    }

    init(canvasId, events, clickCallback) {
        this.canvas = document.getElementById(canvasId);
        if (!this.canvas) return;
        this.ctx = this.canvas.getContext('2d');
        this.clickCallback = clickCallback;
        this.events = events || [];

        this._createOverlay();
        this._resizeCanvas();
        this._buildNodes();
        this._setupListeners();
        this._startLoop();
    }

    updateData(events) {
        this.events = events || [];
        this._buildNodes();
    }

    setUserKeywords(keywords) {
        this.userKeywords = (keywords || []).map(k => k.toLowerCase());
        this._updateModuleMatches();
    }

    // ─── Overlay (HTML box that fades in at end of callout) ──────────────────

    _createOverlay() {
        const old = document.getElementById('univ-callout-box');
        if (old) old.remove();
        const el = document.createElement('div');
        el.id = 'univ-callout-box';
        el.style.cssText = `
            position:absolute; display:none; pointer-events:auto; z-index:120;
            max-width:240px; min-width:180px;
            background:rgba(8,12,24,0.97);
            border:1px solid rgba(74,144,226,0.45);
            border-radius:10px; padding:12px 14px;
            color:#c8d3e0; font-family:system-ui,sans-serif; font-size:12px;
            line-height:1.5; box-shadow:0 6px 32px rgba(0,0,0,0.7);
            opacity:0; transition:opacity 0.18s ease;
        `;
        const container = this.canvas.parentElement;
        container.style.position = 'relative';
        container.appendChild(el);
        this.calloutOverlay = el;
    }

    // ─── Resize & build ───────────────────────────────────────────────────────

    _resizeCanvas() {
        const c = this.canvas.parentElement;
        this.canvas.width = c.clientWidth;
        this.canvas.height = c.clientHeight;
        this._starfield = null;
        this._constellations = null;
    }

    _buildNodes() {
        const W = this.canvas.width, H = this.canvas.height;
        const cx = W / 2, cy = H / 2;

        const sorted = [...this.events].sort((a, b) => {
            const p = { Critical: 4, High: 3, Medium: 2, Low: 1 };
            const d = (p[b.priority] || 2) - (p[a.priority] || 2);
            return d !== 0 ? d : b.impactScore - a.impactScore;
        });

        this.nodes = sorted.map((ev, i) => {
            const radius = this._planetRadius(ev);
            let x, y;
            if (i === 0) { x = cx; y = cy; }
            else {
                const ang = (i - 1) * (2 * Math.PI / Math.max(1, sorted.length - 1));
                const dist = 150 + Math.floor((i - 1) / 6) * 120;
                x = cx + dist * Math.cos(ang);
                y = cy + dist * Math.sin(ang);
            }
            return {
                id: ev.id, x, y, radius,
                event: ev,
                baseAngle: Math.random() * Math.PI * 2,
                pulsePhase: Math.random() * Math.PI * 2,
                color: this._priorityColor(ev.priority),
                isModuleMatch: false,
                frozenMoonAngles: {} // moonIdx → frozen angle when hovered
            };
        });

        this._updateModuleMatches();
        this.hoverTarget = null;
        this.calloutState = null;
        if (this.calloutOverlay) this.calloutOverlay.style.display = 'none';
    }

    _updateModuleMatches() {
        if (!this.userKeywords.length) return;
        for (const node of this.nodes) {
            const txt = `${node.event.title} ${node.event.description || ''} ${(node.event.tags || []).join(' ')}`.toLowerCase();
            node.isModuleMatch = this.userKeywords.some(kw => txt.includes(kw));
        }
    }

    // ─── Listeners ────────────────────────────────────────────────────────────

    _setupListeners() {
        this.canvas.onclick = (e) => this._handleClick(e);
        this.canvas.onmousemove = (e) => this._handleMouseMove(e);
        this.canvas.onmouseleave = () => {
            this._startLeaving();
            this.canvas.style.cursor = 'default';
        };
        if (window.ResizeObserver) {
            this._ro = new ResizeObserver(() => { this._resizeCanvas(); this._buildNodes(); });
            this._ro.observe(this.canvas.parentElement);
        }
    }

    _handleClick(e) {
        const { mx, my } = this._mouse(e);
        // Planet click
        for (const node of this.nodes) {
            if (Math.hypot(mx - node.x, my - node.y) <= node.radius + 10) {
                if (this.clickCallback) this.clickCallback(node.event);
                return;
            }
        }
        // Moon click → navigate to article
        const moon = this._moonAt(mx, my, performance.now());
        if (moon) {
            const art = moon.node.event.articles?.[moon.moonIdx];
            if (art) window.open(`/article/${art.id}`, '_self');
        }
    }

    _handleMouseMove(e) {
        const { mx, my } = this._mouse(e);
        const ts = performance.now();

        // Check planets first
        let newTarget = null;
        for (const node of this.nodes) {
            if (Math.hypot(mx - node.x, my - node.y) <= node.radius + 8) {
                newTarget = { type: 'planet', node };
                break;
            }
        }
        // Then moons
        if (!newTarget) {
            const moon = this._moonAt(mx, my, ts);
            if (moon) newTarget = { type: 'moon', node: moon.node, moonIdx: moon.moonIdx };
        }

        const changed = JSON.stringify(this._targetKey(newTarget)) !== JSON.stringify(this._targetKey(this.hoverTarget));
        if (!changed) return;

        // Unfreeze previous moon
        if (this.hoverTarget?.type === 'moon') {
            delete this.hoverTarget.node.frozenMoonAngles[this.hoverTarget.moonIdx];
        }

        this.hoverTarget = newTarget;
        this.canvas.style.cursor = newTarget ? 'pointer' : 'default';

        if (newTarget) {
            // Freeze moon angle
            if (newTarget.type === 'moon') {
                const angle = this._moonAngle(newTarget.node, newTarget.moonIdx, ts);
                newTarget.node.frozenMoonAngles[newTarget.moonIdx] = angle;
            }
            this._startCallout(newTarget, ts);
        } else {
            this._startLeaving();
        }
    }

    _targetKey(t) {
        if (!t) return null;
        return t.type === 'moon' ? `moon-${t.node.id}-${t.moonIdx}` : `planet-${t.node.id}`;
    }

    _mouse(e) {
        const r = this.canvas.getBoundingClientRect();
        return { mx: e.clientX - r.left, my: e.clientY - r.top };
    }

    _moonAt(mx, my, ts) {
        for (const node of this.nodes) {
            const count = Math.min(node.event.articleCount || 0, 6);
            for (let i = 0; i < count; i++) {
                const angle = node.frozenMoonAngles[i] ?? this._moonAngle(node, i, ts);
                const orbitR = node.radius + 18 + i * 8;
                const ox = node.x + orbitR * Math.cos(angle);
                const oy = node.y + orbitR * Math.sin(angle);
                if (Math.hypot(mx - ox, my - oy) <= 7) return { node, moonIdx: i };
            }
        }
        return null;
    }

    _moonAngle(node, moonIdx, ts) {
        const speed = 0.0003 + moonIdx * 0.00005;
        return node.baseAngle + moonIdx * (Math.PI * 2 / Math.max(1, Math.min(node.event.articleCount || 0, 6))) + ts * speed;
    }

    // ─── Callout state machine ────────────────────────────────────────────────

    _startCallout(target, ts) {
        this.calloutState = { target, startTs: ts, progress: 0, leaving: false };
        if (this.calloutOverlay) {
            this.calloutOverlay.style.display = 'none';
            this.calloutOverlay.style.opacity = '0';
        }
    }

    _startLeaving() {
        if (this.calloutState) this.calloutState.leaving = true;
        if (this.calloutOverlay) {
            this.calloutOverlay.style.opacity = '0';
            setTimeout(() => {
                if (this.calloutOverlay) this.calloutOverlay.style.display = 'none';
            }, 200);
        }
    }

    // ─── Main loop ────────────────────────────────────────────────────────────

    _startLoop() {
        const interval = 1000 / this.config.fps;
        let last = 0;
        const loop = (ts) => {
            if (ts - last >= interval) { this._draw(ts); last = ts; }
            this.animFrame = requestAnimationFrame(loop);
        };
        this.animFrame = requestAnimationFrame(loop);
    }

    _draw(ts) {
        const ctx = this.ctx;
        const W = this.canvas.width, H = this.canvas.height;

        ctx.fillStyle = this.config.bgColor;
        ctx.fillRect(0, 0, W, H);

        this._drawStarfield(ts);
        this._drawConstellations(ts);

        if (!this.nodes.length) { this._drawEmpty(W, H); return; }

        this._drawConnections(ctx);
        for (const node of this.nodes) this._drawNode(ctx, node, ts);

        // Animate and draw callout line
        if (this.calloutState) {
            const cs = this.calloutState;
            if (!cs.leaving) {
                cs.progress = Math.min(1, (ts - cs.startTs) / CALLOUT_DURATION);
            }
            if (cs.progress > 0) {
                this._drawCalloutLine(ctx, cs.target, cs.progress, ts, W, H);
                if (cs.progress >= 1 && !cs.leaving) {
                    this._showCalloutBox(cs.target, ts, W, H);
                }
            }
            if (cs.leaving && cs.progress === 0) this.calloutState = null;
        }
    }

    // ─── Background: starfield ────────────────────────────────────────────────

    _drawStarfield(ts) {
        const ctx = this.ctx;
        if (!this._starfield) {
            this._starfield = Array.from({ length: 100 }, (_, i) => ({
                x: ((i * 137.508) % 1) * this.canvas.width,
                y: ((i * 97.3) % 1) * this.canvas.height,
                r: Math.random() * 1.4 + 0.2,
                a: Math.random() * 0.45 + 0.05,
                phase: Math.random() * Math.PI * 2
            }));
        }
        ctx.save();
        for (const s of this._starfield) {
            const twinkle = 0.6 + 0.4 * Math.sin(ts * 0.0008 + s.phase);
            ctx.fillStyle = `rgba(255,255,255,${s.a * twinkle})`;
            ctx.beginPath();
            ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2);
            ctx.fill();
        }
        ctx.restore();
    }

    // ─── Background: constellations ───────────────────────────────────────────

    _drawConstellations(ts) {
        const ctx = this.ctx;
        const W = this.canvas.width, H = this.canvas.height;

        // Build constellations relative to canvas size (once per resize)
        if (!this._constellations) {
            this._constellations = [
                // Orion-like: 7 stars, top-left quadrant
                { stars: [[0.08,0.12],[0.12,0.08],[0.17,0.10],[0.14,0.17],[0.10,0.20],[0.07,0.18],[0.12,0.23]],
                  lines: [[0,1],[1,2],[2,3],[3,4],[4,5],[5,0],[3,6]], drift: 0.00012 },
                // Triangle: top-right
                { stars: [[0.82,0.08],[0.90,0.05],[0.87,0.14]],
                  lines: [[0,1],[1,2],[2,0]], drift: 0.00009 },
                // Dipper: bottom-left
                { stars: [[0.05,0.75],[0.10,0.72],[0.16,0.74],[0.22,0.72],[0.22,0.68],[0.18,0.65],[0.14,0.66]],
                  lines: [[0,1],[1,2],[2,3],[3,4],[4,5],[5,6]], drift: 0.00007 },
                // Cross: bottom-right
                { stars: [[0.88,0.80],[0.88,0.70],[0.88,0.90],[0.83,0.80],[0.93,0.80]],
                  lines: [[1,0],[0,2],[3,0],[0,4]], drift: 0.00011 },
                // Arc: top-center
                { stars: [[0.38,0.06],[0.44,0.04],[0.50,0.05],[0.56,0.04],[0.62,0.06]],
                  lines: [[0,1],[1,2],[2,3],[3,4]], drift: 0.00008 },
            ];
        }

        ctx.save();
        for (const c of this._constellations) {
            // Very slow drift offset per constellation
            const drift = Math.sin(ts * c.drift) * 6;
            const driftY = Math.cos(ts * c.drift * 0.7) * 4;

            const pts = c.stars.map(([rx, ry]) => ({
                x: rx * W + drift,
                y: ry * H + driftY
            }));

            // Draw lines — very faint
            ctx.strokeStyle = 'rgba(74,144,226,0.06)';
            ctx.lineWidth = 0.8;
            ctx.setLineDash([]);
            for (const [ai, bi] of c.lines) {
                ctx.beginPath();
                ctx.moveTo(pts[ai].x, pts[ai].y);
                ctx.lineTo(pts[bi].x, pts[bi].y);
                ctx.stroke();
            }

            // Draw stars — tiny glowing dots
            for (const pt of pts) {
                const pulse = 0.5 + 0.5 * Math.sin(ts * 0.001 + pt.x);
                ctx.fillStyle = `rgba(180,210,255,${0.12 + 0.08 * pulse})`;
                ctx.beginPath();
                ctx.arc(pt.x, pt.y, 1.2, 0, Math.PI * 2);
                ctx.fill();
            }
        }
        ctx.restore();
    }

    // ─── Section connections ──────────────────────────────────────────────────

    _drawConnections(ctx) {
        for (let i = 0; i < this.nodes.length; i++) {
            for (let j = i + 1; j < this.nodes.length; j++) {
                const a = this.nodes[i], b = this.nodes[j];
                if (a.event.sectionSlug !== b.event.sectionSlug) continue;
                const dist = Math.hypot(a.x - b.x, a.y - b.y);
                if (dist >= 300) continue;
                const alpha = (1 - dist / 300) * 0.10;
                ctx.save();
                ctx.strokeStyle = `rgba(74,144,226,${alpha})`;
                ctx.lineWidth = 1;
                ctx.setLineDash([4, 9]);
                ctx.beginPath();
                ctx.moveTo(a.x, a.y);
                ctx.lineTo(b.x, b.y);
                ctx.stroke();
                ctx.restore();
            }
        }
    }

    // ─── Draw planet node ─────────────────────────────────────────────────────

    _drawNode(ctx, node, ts) {
        const { x, y, radius, event, color } = node;
        const isHoveredPlanet = this.hoverTarget?.type === 'planet' && this.hoverTarget.node === node;
        const pulse = Math.sin(ts * 0.002 + node.pulsePhase);
        const isCritical = event.priority === 'Critical';
        const dr = isHoveredPlanet ? radius * 1.08 : radius;

        ctx.save();

        // Outer glow
        const glowR = isCritical ? radius * 2.8 + pulse * 10 : radius * 1.9;
        const grd = ctx.createRadialGradient(x, y, 0, x, y, glowR);
        grd.addColorStop(0, color + '30');
        grd.addColorStop(1, 'transparent');
        ctx.fillStyle = grd;
        ctx.beginPath(); ctx.arc(x, y, glowR, 0, Math.PI * 2); ctx.fill();

        // Critical pulse ring
        if (isCritical) {
            const ringR = radius + 12 + pulse * 8;
            ctx.strokeStyle = color + '50'; ctx.lineWidth = 2; ctx.setLineDash([]);
            ctx.beginPath(); ctx.arc(x, y, ringR, 0, Math.PI * 2); ctx.stroke();
        }

        // Module match ring
        if (node.isModuleMatch) {
            ctx.strokeStyle = 'rgba(0,255,136,0.65)'; ctx.lineWidth = 2.5;
            ctx.setLineDash([6, 4]);
            ctx.beginPath(); ctx.arc(x, y, dr + 7, 0, Math.PI * 2); ctx.stroke();
        }

        // Planet core
        ctx.setLineDash([]);
        const cGrd = ctx.createRadialGradient(x - radius * 0.3, y - radius * 0.3, 0, x, y, dr);
        cGrd.addColorStop(0, this._lighten(color, 50));
        cGrd.addColorStop(0.65, color);
        cGrd.addColorStop(1, this._darken(color, 25));
        ctx.fillStyle = cGrd;
        ctx.beginPath(); ctx.arc(x, y, dr, 0, Math.PI * 2); ctx.fill();

        ctx.strokeStyle = isHoveredPlanet ? 'rgba(255,255,255,0.9)' : color + 'aa';
        ctx.lineWidth = isHoveredPlanet ? 2 : 1;
        ctx.stroke();

        // Orbiting moons
        const moonCount = Math.min(event.articleCount || 0, 6);
        for (let i = 0; i < moonCount; i++) {
            const isMoonHovered = this.hoverTarget?.type === 'moon'
                && this.hoverTarget.node === node && this.hoverTarget.moonIdx === i;
            const angle = node.frozenMoonAngles[i] ?? this._moonAngle(node, i, ts);
            const orbitR = dr + 18 + i * 8;
            const ox = x + orbitR * Math.cos(angle);
            const oy = y + orbitR * Math.sin(angle);

            // Moon glow on hover
            if (isMoonHovered) {
                ctx.shadowColor = '#4a90e2';
                ctx.shadowBlur = 8;
            }
            ctx.fillStyle = isMoonHovered ? '#80bbff' : 'rgba(74,144,226,0.85)';
            ctx.beginPath();
            ctx.arc(ox, oy, isMoonHovered ? 4.5 : 3, 0, Math.PI * 2);
            ctx.fill();
            ctx.shadowBlur = 0;
        }

        // Label fitted inside planet
        this._drawLabel(ctx, node, ts, dr);

        ctx.restore();
    }

    _drawLabel(ctx, node, ts, dr) {
        const { x, y, event } = node;
        const fontSize = Math.max(8, Math.min(13, dr * 0.36));
        // Max chars that fit inside chord of radius dr (inscribed width ≈ dr * 1.4)
        const charsPerPx = fontSize * 0.54;
        const maxW = dr * 1.35;
        const maxChars = Math.max(4, Math.floor(maxW / charsPerPx));
        const label = event.title.length > maxChars
            ? event.title.substring(0, maxChars - 1) + '…'
            : event.title;

        ctx.font = `600 ${fontSize}px system-ui`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        // Contrast shadow
        ctx.shadowColor = 'rgba(0,0,0,0.95)';
        ctx.shadowBlur = 5;
        ctx.fillStyle = 'rgba(0,0,0,0.4)';
        ctx.fillText(label, x + 0.5, y + 0.5);
        ctx.shadowBlur = 0;
        ctx.fillStyle = '#ffffff';
        ctx.fillText(label, x, y);

        // Impact score below if room
        if (dr > 30) {
            ctx.font = `${Math.max(8, fontSize - 2)}px system-ui`;
            ctx.shadowColor = 'rgba(0,0,0,0.85)';
            ctx.shadowBlur = 4;
            ctx.fillStyle = 'rgba(255,255,255,0.6)';
            ctx.fillText(`${Math.round(event.impactScore)}`, x, y + fontSize + 2);
            ctx.shadowBlur = 0;
        }
    }

    // ─── Callout line animation ───────────────────────────────────────────────
    // progress 0→1: 0–0.45 vertical, 0.45–0.75 horizontal, 0.75–1.0 box (HTML)

    _calloutGeometry(target, ts, W, H) {
        let ax, ay; // anchor point (planet center or moon position)
        if (target.type === 'moon') {
            const angle = target.node.frozenMoonAngles[target.moonIdx]
                ?? this._moonAngle(target.node, target.moonIdx, ts);
            const orbitR = target.node.radius + 18 + target.moonIdx * 8;
            ax = target.node.x + orbitR * Math.cos(angle);
            ay = target.node.y + orbitR * Math.sin(angle);
        } else {
            ax = target.node.x;
            ay = target.node.y;
        }

        const goUp = ay > H * 0.5;
        const goRight = ax < W * 0.5;
        const vLen = target.type === 'moon' ? 40 : 60;  // vertical segment length
        const hLen = 80;                                  // horizontal segment length

        const ex = ax; // line start x (on planet/moon edge offset handled by dot)
        const ey = goUp ? ay - (target.type === 'planet' ? target.node.radius : 5)
                        : ay + (target.type === 'planet' ? target.node.radius : 5);

        const elbowY = goUp ? ey - vLen : ey + vLen;
        const endX = goRight ? ex + hLen : ex - hLen;
        const boxX = goRight ? endX + 8 : endX - 248; // 240px max width

        return { ax, ay, ex, ey, elbowY, endX, goUp, goRight, boxX, boxY: elbowY - 8 };
    }

    _drawCalloutLine(ctx, target, progress, ts, W, H) {
        const g = this._calloutGeometry(target, ts, W, H);
        const { ex, ey, elbowY, endX, goUp } = g;

        // Phase split
        const p1End = 0.45, p2End = 0.75;
        const p1 = Math.min(1, progress / p1End);
        const p2 = progress > p1End ? Math.min(1, (progress - p1End) / (p2End - p1End)) : 0;

        ctx.save();
        ctx.strokeStyle = 'rgba(74,144,226,0.6)';
        ctx.lineWidth = 1.2;
        ctx.setLineDash([]);

        // Starting dot
        ctx.fillStyle = '#4a90e2';
        ctx.beginPath(); ctx.arc(ex, ey, 3, 0, Math.PI * 2); ctx.fill();

        if (p1 > 0) {
            // Vertical segment
            const curY = ey + (elbowY - ey) * p1;
            ctx.beginPath(); ctx.moveTo(ex, ey); ctx.lineTo(ex, curY); ctx.stroke();
        }
        if (p2 > 0) {
            // Elbow dot
            ctx.fillStyle = 'rgba(74,144,226,0.5)';
            ctx.beginPath(); ctx.arc(ex, elbowY, 2, 0, Math.PI * 2); ctx.fill();
            // Horizontal segment
            const curX = ex + (endX - ex) * p2;
            ctx.beginPath(); ctx.moveTo(ex, elbowY); ctx.lineTo(curX, elbowY); ctx.stroke();
        }

        ctx.restore();
    }

    _showCalloutBox(target, ts, W, H) {
        if (!this.calloutOverlay) return;
        const g = this._calloutGeometry(target, ts, W, H);
        const el = this.calloutOverlay;

        el.innerHTML = this._buildCalloutHTML(target);
        el.style.left = Math.max(0, Math.min(W - 248, g.boxX)) + 'px';
        el.style.top = Math.max(0, g.boxY) + 'px';
        el.style.display = 'block';
        requestAnimationFrame(() => { el.style.opacity = '1'; });
    }

    _buildCalloutHTML(target) {
        const pc = { Critical: '#ff0040', High: '#ff6600', Medium: '#e6b800', Low: '#4a90e2' };

        if (target.type === 'moon') {
            const art = target.node.event.articles?.[target.moonIdx];
            if (!art) return '<div style="color:#666">Artículo no disponible</div>';
            return `
                <div style="font-size:10px;color:#5a7aa0;margin-bottom:4px;text-transform:uppercase;letter-spacing:.05em">
                    Artículo · ${target.node.event.sectionName || ''}
                </div>
                <div style="font-weight:600;color:#e8eaf0;font-size:12px;line-height:1.35;margin-bottom:8px">
                    ${art.title}
                </div>
                <div style="font-size:11px;color:#7a99bb;margin-bottom:8px">📡 ${art.sourceName}</div>
                <a href="/article/${art.id}" style="display:inline-flex;align-items:center;gap:5px;
                    background:#1a2a4a;color:#4a90e2;border:1px solid #2a3a6a;border-radius:5px;
                    padding:5px 10px;font-size:11px;text-decoration:none;font-weight:600;">
                    <i class="fa fa-external-link" style="font-size:10px"></i> Ver artículo
                </a>
            `;
        }

        const ev = target.node.event;
        const color = pc[ev.priority] || '#4a90e2';
        const moduleTag = target.node.isModuleMatch
            ? `<span style="background:#00ff8818;color:#00ff88;border:1px solid #00ff8830;
                border-radius:4px;padding:1px 6px;font-size:10px;margin-left:5px">tu módulo</span>`
            : '';

        return `
            <div style="font-size:10px;color:#5a7aa0;margin-bottom:4px;text-transform:uppercase;letter-spacing:.05em">
                ${ev.sectionName || ''}
            </div>
            <div style="font-weight:700;color:#fff;font-size:13px;line-height:1.3;margin-bottom:6px">
                ${ev.title}
            </div>
            <div style="margin-bottom:7px">
                <span style="background:${color}20;color:${color};border:1px solid ${color}40;
                    border-radius:4px;padding:1px 8px;font-size:10px;font-weight:700">${ev.priority}</span>
                ${moduleTag}
            </div>
            <div style="font-size:11px;color:#8899bb;margin-bottom:6px">
                ⚡ Impacto <strong style="color:#c8d3e0">${Math.round(ev.impactScore)}</strong>
                &nbsp;·&nbsp;
                📰 <strong style="color:#c8d3e0">${ev.articleCount}</strong> artículos
            </div>
            ${ev.description ? `<div style="font-size:11px;color:#8aacbf;line-height:1.45;margin-bottom:8px">
                ${ev.description.substring(0, 130)}${ev.description.length > 130 ? '…' : ''}
            </div>` : ''}
            <div style="display:flex;gap:6px;flex-wrap:wrap">
                <a href="/situation/${ev.id}" style="display:inline-flex;align-items:center;gap:5px;
                    background:#1a2a4a;color:#4a90e2;border:1px solid #2a3a6a;border-radius:5px;
                    padding:5px 10px;font-size:11px;text-decoration:none;font-weight:600;">
                    <i class="fa fa-map" style="font-size:10px"></i> Situación
                </a>
                <span style="display:inline-flex;align-items:center;gap:4px;color:#5a7099;
                    font-size:10px;padding:5px 6px;">
                    Clic en planeta para abrir →
                </span>
            </div>
        `;
    }

    // ─── Empty state ──────────────────────────────────────────────────────────

    _drawEmpty(W, H) {
        const ctx = this.ctx;
        ctx.save();
        ctx.fillStyle = 'rgba(200,210,224,0.35)';
        ctx.font = '16px system-ui';
        ctx.textAlign = 'center';
        ctx.fillText('Esperando noticias...', W / 2, H / 2 + 30);
        ctx.restore();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    _planetRadius(ev) {
        const base = this.config.planetMinR, max = this.config.planetMaxR;
        const impact = Math.min(100, Math.max(0, ev.impactScore || 50)) / 100;
        const bonus = { Critical: 15, High: 10, Medium: 5, Low: 0 };
        return base + (max - base) * impact + (bonus[ev.priority] || 0);
    }

    _priorityColor(p) {
        return { Critical: '#ff0040', High: '#ff6600', Medium: '#e6b800', Low: '#4a90e2' }[p] || '#4a90e2';
    }

    _lighten(hex, n) {
        const v = parseInt(hex.replace('#', ''), 16);
        return `rgb(${Math.min(255,(v>>16)+n)},${Math.min(255,((v>>8)&255)+n)},${Math.min(255,(v&255)+n)})`;
    }

    _darken(hex, n) {
        const v = parseInt(hex.replace('#', ''), 16);
        return `rgb(${Math.max(0,(v>>16)-n)},${Math.max(0,((v>>8)&255)-n)},${Math.max(0,(v&255)-n)})`;
    }

    destroy() {
        if (this.animFrame) cancelAnimationFrame(this.animFrame);
        if (this._ro) this._ro.disconnect();
        if (this.calloutOverlay) this.calloutOverlay.remove();
        if (this.canvas) {
            this.canvas.onclick = null;
            this.canvas.onmousemove = null;
            this.canvas.onmouseleave = null;
        }
    }
}

// ─── Global singleton ─────────────────────────────────────────────────────────

window.universe = {
    _engine: null,

    init(canvasId, events) {
        if (this._engine) this._engine.destroy();
        this._engine = new NewsUniverseEngine();
        this._engine.init(canvasId, events, (ev) => {
            document.dispatchEvent(new CustomEvent('universe:eventClick', { detail: ev }));
        });
    },

    updateData(events) {
        if (this._engine) this._engine.updateData(events);
    },

    setUserKeywords(keywords) {
        if (this._engine) this._engine.setUserKeywords(keywords);
    },

    destroy() {
        if (this._engine) { this._engine.destroy(); this._engine = null; }
    }
};
