/**
 * AgenteNews Universe Engine v2
 * Canvas-based news visualization
 * v2 changes:
 * - Labels: text inside planet with drop-shadow for contrast
 * - Hover callout: dot on planet edge → elbow line → floating HTML label
 * - Module-aware: nodes matching user keywords get green highlight ring
 */

class NewsUniverseEngine {
    constructor() {
        this.canvas = null;
        this.ctx = null;
        this.events = [];
        this.nodes = [];
        this.animFrame = null;
        this.calloutEl = null;
        this.hoveredNode = null;
        this.clickCallback = null;
        this.userKeywords = []; // keywords from user modules

        this.config = {
            starMinRadius: 20,
            starMaxRadius: 50,
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

        // Create the callout HTML element
        this._createCallout();

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

    _createCallout() {
        // Remove existing callout if any
        const old = document.getElementById('universe-callout');
        if (old) old.remove();

        const el = document.createElement('div');
        el.id = 'universe-callout';
        el.style.cssText = `
            position: absolute;
            display: none;
            pointer-events: none;
            z-index: 100;
            max-width: 220px;
            background: rgba(10, 14, 26, 0.95);
            border: 1px solid rgba(74,144,226,0.5);
            border-radius: 8px;
            padding: 10px 12px;
            color: #c8d3e0;
            font-family: system-ui, sans-serif;
            font-size: 12px;
            line-height: 1.5;
            box-shadow: 0 4px 20px rgba(0,0,0,0.6);
            backdrop-filter: blur(4px);
        `;

        const container = this.canvas.parentElement;
        container.style.position = 'relative';
        container.appendChild(el);
        this.calloutEl = el;
    }

    _resizeCanvas() {
        const container = this.canvas.parentElement;
        this.canvas.width = container.clientWidth;
        this.canvas.height = container.clientHeight;
        this._starfield = null; // reset starfield on resize
    }

    _buildNodes() {
        const W = this.canvas.width;
        const H = this.canvas.height;
        const cx = W / 2;
        const cy = H / 2;

        const sorted = [...this.events].sort((a, b) => {
            const pa = { Critical: 4, High: 3, Medium: 2, Low: 1 };
            const diff = (pa[b.priority] || 2) - (pa[a.priority] || 2);
            return diff !== 0 ? diff : b.impactScore - a.impactScore;
        });

        this.nodes = sorted.map((ev, i) => {
            const radius = this._getRadius(ev);
            let x, y;

            if (i === 0) {
                x = cx; y = cy;
            } else {
                const angle = (i - 1) * (2 * Math.PI / Math.max(1, sorted.length - 1));
                const dist = 150 + Math.floor((i - 1) / 6) * 120;
                x = cx + dist * Math.cos(angle);
                y = cy + dist * Math.sin(angle);
            }

            return {
                id: ev.id,
                x, y, radius,
                event: ev,
                orbitAngle: Math.random() * Math.PI * 2,
                pulsePhase: Math.random() * Math.PI * 2,
                color: this._getColor(ev.priority),
                glowColor: this._getColor(ev.priority),
                isModuleMatch: false
            };
        });

        this._updateModuleMatches();
    }

    _updateModuleMatches() {
        if (!this.userKeywords.length) return;
        for (const node of this.nodes) {
            const ev = node.event;
            const text = `${ev.title} ${ev.description || ''} ${(ev.tags || []).join(' ')}`.toLowerCase();
            node.isModuleMatch = this.userKeywords.some(kw => text.includes(kw));
        }
    }

    _setupListeners() {
        this.canvas.onclick = (e) => this._handleClick(e);
        this.canvas.onmousemove = (e) => this._handleMouseMove(e);
        this.canvas.onmouseleave = () => {
            this.hoveredNode = null;
            if (this.calloutEl) this.calloutEl.style.display = 'none';
            this.canvas.style.cursor = 'default';
        };

        if (window.ResizeObserver) {
            this._resizeObserver = new ResizeObserver(() => {
                this._resizeCanvas();
                this._buildNodes();
            });
            this._resizeObserver.observe(this.canvas.parentElement);
        }
    }

    _handleClick(e) {
        const rect = this.canvas.getBoundingClientRect();
        const mx = e.clientX - rect.left;
        const my = e.clientY - rect.top;

        for (const node of this.nodes) {
            if (Math.hypot(mx - node.x, my - node.y) <= node.radius + 10) {
                if (this.clickCallback) this.clickCallback(node.event);
                return;
            }
        }
    }

    _handleMouseMove(e) {
        const rect = this.canvas.getBoundingClientRect();
        const mx = e.clientX - rect.left;
        const my = e.clientY - rect.top;

        let found = null;
        for (const node of this.nodes) {
            if (Math.hypot(mx - node.x, my - node.y) <= node.radius + 10) {
                found = node;
                break;
            }
        }

        if (found !== this.hoveredNode) {
            this.hoveredNode = found;
            this.canvas.style.cursor = found ? 'pointer' : 'default';
            this._updateCallout(found, mx, my);
        }
    }

    _updateCallout(node, mx, my) {
        if (!this.calloutEl) return;

        if (!node) {
            this.calloutEl.style.display = 'none';
            return;
        }

        const ev = node.event;
        const priorityColors = {
            Critical: '#ff0040', High: '#ff8c42', Medium: '#ffcc00', Low: '#4a90e2'
        };
        const pc = priorityColors[ev.priority] || '#4a90e2';
        const moduleTag = node.isModuleMatch
            ? `<span style="background:#00ff8820;color:#00ff88;border:1px solid #00ff8840;border-radius:4px;padding:1px 6px;font-size:10px;margin-left:4px;">tu módulo</span>`
            : '';

        this.calloutEl.innerHTML = `
            <div style="font-weight:700;color:#fff;font-size:13px;margin-bottom:4px;line-height:1.3">${ev.title}</div>
            <div style="margin-bottom:6px">
                <span style="background:${pc}22;color:${pc};border:1px solid ${pc}44;border-radius:4px;padding:1px 7px;font-size:10px;font-weight:600">${ev.priority}</span>
                ${moduleTag}
            </div>
            <div style="color:#8899bb;font-size:11px;margin-bottom:4px">
                <span>📡 ${ev.sectionName || ''}</span> &nbsp;·&nbsp;
                <span>⚡ Impacto ${Math.round(ev.impactScore)}</span> &nbsp;·&nbsp;
                <span>📰 ${ev.articleCount || 0} artículos</span>
            </div>
            ${ev.description ? `<div style="color:#9aabb8;font-size:11px;line-height:1.4;margin-top:4px">${ev.description.substring(0, 120)}${ev.description.length > 120 ? '…' : ''}</div>` : ''}
            <div style="margin-top:8px;font-size:10px;color:#4a90e2">Clic para ver detalles →</div>
        `;

        // Position callout: prefer right side, flip if near edge
        const W = this.canvas.width;
        const H = this.canvas.height;
        const margin = 12;
        const calloutW = 220;
        const left = mx + margin + calloutW > W ? mx - calloutW - margin : mx + margin;
        const top = Math.min(my - 10, H - 180);

        this.calloutEl.style.left = left + 'px';
        this.calloutEl.style.top = Math.max(0, top) + 'px';
        this.calloutEl.style.display = 'block';
    }

    _startLoop() {
        const interval = 1000 / this.config.fps;
        let last = 0;
        const loop = (ts) => {
            if (ts - last >= interval) {
                this._draw(ts);
                last = ts;
            }
            this.animFrame = requestAnimationFrame(loop);
        };
        this.animFrame = requestAnimationFrame(loop);
    }

    _draw(ts) {
        const ctx = this.ctx;
        const W = this.canvas.width;
        const H = this.canvas.height;

        ctx.fillStyle = this.config.bgColor;
        ctx.fillRect(0, 0, W, H);

        this._drawStarfield(ts);

        if (this.nodes.length === 0) {
            this._drawEmptyState(W, H);
            return;
        }

        this._drawConnections(ctx);

        for (const node of this.nodes) {
            this._drawNode(ctx, node, ts);
        }

        // Draw callout connector line for hovered node
        if (this.hoveredNode && this.calloutEl && this.calloutEl.style.display !== 'none') {
            this._drawCalloutLine(ctx, this.hoveredNode);
        }
    }

    _drawCalloutLine(ctx, node) {
        const calloutLeft = parseInt(this.calloutEl.style.left);
        const calloutTop = parseInt(this.calloutEl.style.top);
        const calloutH = this.calloutEl.offsetHeight || 100;

        // Connection point on callout: left-center or right-center
        const nx = node.x;
        const ny = node.y;
        const isRight = calloutLeft > nx;

        // Start: point on planet edge toward the callout
        const angle = Math.atan2(calloutTop + calloutH / 2 - ny, calloutLeft - nx);
        const ex = nx + node.radius * Math.cos(angle);
        const ey = ny + node.radius * Math.sin(angle);

        // End: side of callout box
        const tx = isRight ? calloutLeft : calloutLeft + 220;
        const ty = calloutTop + calloutH / 2;

        // Elbow point: same x as target, same y as start edge
        const elbowX = tx;
        const elbowY = ey;

        ctx.save();
        ctx.strokeStyle = 'rgba(74,144,226,0.5)';
        ctx.lineWidth = 1;
        ctx.setLineDash([3, 5]);
        ctx.beginPath();

        // Small dot at planet edge
        ctx.arc(ex, ey, 3, 0, Math.PI * 2);
        ctx.fillStyle = '#4a90e2';
        ctx.fill();

        // Line: planet edge → elbow → callout
        ctx.beginPath();
        ctx.moveTo(ex, ey);
        ctx.lineTo(elbowX, elbowY);
        ctx.lineTo(tx, ty);
        ctx.stroke();

        ctx.restore();
    }

    _drawStarfield(ts) {
        const ctx = this.ctx;
        if (!this._starfield) {
            this._starfield = Array.from({ length: 80 }, (_, i) => ({
                x: ((i * 137.5) % 1) * this.canvas.width,
                y: ((i * 97.3) % 1) * this.canvas.height,
                r: Math.random() * 1.5 + 0.3,
                alpha: Math.random() * 0.5 + 0.1
            }));
        }
        ctx.save();
        for (const star of this._starfield) {
            const twinkle = 0.5 + 0.5 * Math.sin(ts * 0.001 + star.x);
            ctx.fillStyle = `rgba(255,255,255,${star.alpha * twinkle})`;
            ctx.beginPath();
            ctx.arc(star.x, star.y, star.r, 0, Math.PI * 2);
            ctx.fill();
        }
        ctx.restore();
    }

    _drawConnections(ctx) {
        for (let i = 0; i < this.nodes.length; i++) {
            for (let j = i + 1; j < this.nodes.length; j++) {
                const a = this.nodes[i];
                const b = this.nodes[j];
                if (a.event.sectionSlug === b.event.sectionSlug) {
                    const dist = Math.hypot(a.x - b.x, a.y - b.y);
                    if (dist < 300) {
                        const alpha = (1 - dist / 300) * 0.12;
                        ctx.save();
                        ctx.strokeStyle = `rgba(74,144,226,${alpha})`;
                        ctx.lineWidth = 1;
                        ctx.setLineDash([4, 8]);
                        ctx.beginPath();
                        ctx.moveTo(a.x, a.y);
                        ctx.lineTo(b.x, b.y);
                        ctx.stroke();
                        ctx.restore();
                    }
                }
            }
        }
    }

    _drawNode(ctx, node, ts) {
        const { x, y, radius, event, color, glowColor } = node;
        const isHovered = node === this.hoveredNode;
        const pulse = Math.sin(ts * 0.002 + node.pulsePhase);
        const isCritical = event.priority === 'Critical';
        const displayRadius = isHovered ? radius * 1.08 : radius;

        ctx.save();

        // Glow
        const glowSize = isCritical ? radius * 2.5 + pulse * 10 : radius * 1.8;
        const grd = ctx.createRadialGradient(x, y, 0, x, y, glowSize);
        grd.addColorStop(0, glowColor + '35');
        grd.addColorStop(1, 'transparent');
        ctx.fillStyle = grd;
        ctx.beginPath();
        ctx.arc(x, y, glowSize, 0, Math.PI * 2);
        ctx.fill();

        // Critical pulse ring
        if (isCritical) {
            const ringR = radius + 10 + pulse * 8;
            ctx.strokeStyle = color + '55';
            ctx.lineWidth = 2;
            ctx.setLineDash([]);
            ctx.beginPath();
            ctx.arc(x, y, ringR, 0, Math.PI * 2);
            ctx.stroke();
        }

        // Module match ring (green highlight)
        if (node.isModuleMatch) {
            ctx.strokeStyle = 'rgba(0,255,136,0.7)';
            ctx.lineWidth = 2.5;
            ctx.setLineDash([6, 4]);
            ctx.beginPath();
            ctx.arc(x, y, displayRadius + 6, 0, Math.PI * 2);
            ctx.stroke();
        }

        // Core planet
        ctx.setLineDash([]);
        const coreGrd = ctx.createRadialGradient(x - radius * 0.3, y - radius * 0.3, 0, x, y, displayRadius);
        coreGrd.addColorStop(0, this._lighten(color, 45));
        coreGrd.addColorStop(0.7, color);
        coreGrd.addColorStop(1, this._darken(color, 20));
        ctx.fillStyle = coreGrd;
        ctx.beginPath();
        ctx.arc(x, y, displayRadius, 0, Math.PI * 2);
        ctx.fill();

        // Border
        ctx.strokeStyle = isHovered ? 'rgba(255,255,255,0.9)' : color + 'aa';
        ctx.lineWidth = isHovered ? 2 : 1;
        ctx.stroke();

        // Orbiting article dots
        const articles = Math.min(event.articleCount || 0, 6);
        for (let i = 0; i < articles; i++) {
            const orbitR = displayRadius + 16 + i * 7;
            const speed = 0.0003 + i * 0.00005;
            const angle = node.orbitAngle + i * (Math.PI * 2 / articles) + ts * speed;
            ctx.fillStyle = 'rgba(74,144,226,0.85)';
            ctx.beginPath();
            ctx.arc(x + orbitR * Math.cos(angle), y + orbitR * Math.sin(angle), 2.5, 0, Math.PI * 2);
            ctx.fill();
        }

        // Label inside planet — with text shadow for contrast
        const fontSize = Math.max(9, Math.min(12, radius * 0.38));
        const maxChars = Math.floor(displayRadius * 1.5 / (fontSize * 0.55));
        const label = event.title.length > maxChars
            ? event.title.substring(0, maxChars - 1) + '…'
            : event.title;

        ctx.font = `${isHovered ? '600 ' : ''}${fontSize}px system-ui`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        // Shadow pass
        ctx.shadowColor = 'rgba(0,0,0,0.9)';
        ctx.shadowBlur = 6;
        ctx.fillStyle = 'rgba(0,0,0,0.5)';
        ctx.fillText(label, x + 1, y + 1);

        // Text pass
        ctx.shadowBlur = 0;
        ctx.fillStyle = '#ffffff';
        ctx.fillText(label, x, y);

        // Impact score below — only if radius is big enough
        if (radius > 28) {
            ctx.font = '9px system-ui';
            ctx.shadowColor = 'rgba(0,0,0,0.8)';
            ctx.shadowBlur = 4;
            ctx.fillStyle = 'rgba(255,255,255,0.65)';
            ctx.fillText(`${Math.round(event.impactScore)}`, x, y + fontSize + 3);
            ctx.shadowBlur = 0;
        }

        ctx.restore();
    }

    _drawEmptyState(W, H) {
        const ctx = this.ctx;
        ctx.save();
        ctx.fillStyle = 'rgba(74,144,226,0.4)';
        ctx.font = '48px system-ui';
        ctx.textAlign = 'center';
        ctx.fillText('📡', W / 2, H / 2 - 20);
        ctx.fillStyle = 'rgba(200,210,224,0.5)';
        ctx.font = '16px system-ui';
        ctx.fillText('Esperando noticias...', W / 2, H / 2 + 30);
        ctx.restore();
    }

    _getRadius(ev) {
        const base = this.config.starMinRadius;
        const max = this.config.starMaxRadius;
        const impact = Math.min(100, Math.max(0, ev.impactScore || 50)) / 100;
        const priorityBonus = { Critical: 15, High: 10, Medium: 5, Low: 0 };
        return base + (max - base) * impact + (priorityBonus[ev.priority] || 0);
    }

    _getColor(priority) {
        return { Critical: '#ff0040', High: '#ff6600', Medium: '#e6b800', Low: '#4a90e2' }[priority] || '#4a90e2';
    }

    _lighten(hex, amount) {
        const num = parseInt(hex.replace('#', ''), 16);
        const r = Math.min(255, (num >> 16) + amount);
        const g = Math.min(255, ((num >> 8) & 0xFF) + amount);
        const b = Math.min(255, (num & 0xFF) + amount);
        return `rgb(${r},${g},${b})`;
    }

    _darken(hex, amount) {
        const num = parseInt(hex.replace('#', ''), 16);
        const r = Math.max(0, (num >> 16) - amount);
        const g = Math.max(0, ((num >> 8) & 0xFF) - amount);
        const b = Math.max(0, (num & 0xFF) - amount);
        return `rgb(${r},${g},${b})`;
    }

    destroy() {
        if (this.animFrame) cancelAnimationFrame(this.animFrame);
        if (this._resizeObserver) this._resizeObserver.disconnect();
        if (this.calloutEl) this.calloutEl.remove();
        if (this.canvas) {
            this.canvas.onclick = null;
            this.canvas.onmousemove = null;
            this.canvas.onmouseleave = null;
        }
    }
}

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
        if (this._engine) {
            this._engine.destroy();
            this._engine = null;
        }
    }
};
