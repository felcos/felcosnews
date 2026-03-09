/* ============================================================
   map.js — World map view for AgenteNews using Leaflet.js
   ============================================================ */
(function () {
    'use strict';

    let _map = null;
    let _markers = {};
    let _dotnet = null;
    let _noLocPanel = null;
    let _geocodeCache = {};
    let _callout = null; // { svg, box }

    const PRIORITY_COLORS = {
        Critical: '#ff0040',
        High: '#ff6600',
        Medium: '#e6b800',
        Low: '#4a90e2'
    };

    // Manual coords for regions Nominatim gets wrong
    const REGION_COORDS = {
        'middle east': { lat: 29.5, lng: 42.5 },
        'near east': { lat: 29.5, lng: 42.5 },
        'eastern europe': { lat: 50.0, lng: 30.0 },
        'western europe': { lat: 48.0, lng: 10.0 },
        'central europe': { lat: 48.0, lng: 16.0 },
        'northern europe': { lat: 60.0, lng: 15.0 },
        'southern europe': { lat: 42.0, lng: 14.0 },
        'central asia': { lat: 45.0, lng: 65.0 },
        'south asia': { lat: 23.0, lng: 77.0 },
        'southeast asia': { lat: 10.0, lng: 108.0 },
        'east asia': { lat: 35.0, lng: 115.0 },
        'sub-saharan africa': { lat: 0.0, lng: 20.0 },
        'north africa': { lat: 27.0, lng: 17.0 },
        'west africa': { lat: 12.0, lng: -2.0 },
        'east africa': { lat: 0.0, lng: 37.0 },
        'latin america': { lat: -15.0, lng: -60.0 },
        'south america': { lat: -15.0, lng: -55.0 },
        'central america': { lat: 12.0, lng: -85.0 },
        'caribbean': { lat: 18.0, lng: -66.0 },
        'balkans': { lat: 43.0, lng: 20.0 },
        'caucasus': { lat: 42.0, lng: 45.0 },
        'horn of africa': { lat: 8.0, lng: 42.0 },
        'persian gulf': { lat: 26.0, lng: 52.0 },
        'gulf states': { lat: 24.0, lng: 51.0 },
        'red sea': { lat: 20.0, lng: 38.0 },
        'mediterranean': { lat: 38.0, lng: 18.0 },
        'north atlantic': { lat: 45.0, lng: -40.0 },
        'pacific': { lat: 0.0, lng: -160.0 },
        'south pacific': { lat: -20.0, lng: -150.0 },
        'arctic': { lat: 80.0, lng: 0.0 },
        'global': { lat: 20.0, lng: 0.0 },
        'worldwide': { lat: 20.0, lng: 0.0 },
    };

    function _priorityRadius(priority) {
        return { Critical: 14, High: 11, Medium: 9, Low: 7 }[priority] || 9;
    }

    window.newsMap = {
        init(containerId, events, dotnetRef) {
            if (_map) { _map.remove(); _map = null; _markers = {}; }
            if (_noLocPanel) { _noLocPanel.remove(); _noLocPanel = null; }
            _hideCallout();
            _dotnet = dotnetRef;

            const container = document.getElementById(containerId);
            if (!container) { console.warn('[map.js] container not found:', containerId); return; }

            _map = L.map(containerId, {
                center: [20, 0], zoom: 2, minZoom: 1, maxZoom: 10, zoomControl: true
            });

            L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
                attribution: '© OpenStreetMap contributors © CARTO',
                subdomains: 'abcd', maxZoom: 19
            }).addTo(_map);

            _map.on('click', _hideCallout);

            _addEvents(events || []);
            console.log(`[map.js] init: ${(events||[]).length} events`);
        },

        updateData(events) {
            if (!_map) return;
            Object.values(_markers).forEach(({ marker }) => marker.remove());
            _markers = {};
            if (_noLocPanel) { _noLocPanel.remove(); _noLocPanel = null; }
            _hideCallout();
            _addEvents(events || []);
        },

        destroy() {
            if (_map) { _map.remove(); _map = null; }
            if (_noLocPanel) { _noLocPanel.remove(); _noLocPanel = null; }
            _hideCallout();
            _markers = {};
            _dotnet = null;
        }
    };

    // ── Callout (same style as universe.js) ─────────────────────────────────
    function _showCallout(ev, lat, lng) {
        _hideCallout();
        if (!_map) return;

        const pt = _map.latLngToContainerPoint([lat, lng]);
        const container = _map.getContainer();
        const W = container.offsetWidth;
        const H = container.offsetHeight;
        const color = PRIORITY_COLORS[ev.priority] || '#4a90e2';

        const BOX_W = 260;
        const BOX_H = 170;
        const ELBOW_V = 60;  // vertical segment
        const ELBOW_H = 70;  // horizontal segment

        const goLeft = pt.x > W * 0.55;
        const goUp   = pt.y > H * 0.5;

        // Elbow tip (end of vertical segment)
        const tipX = pt.x;
        const tipY = goUp ? pt.y - ELBOW_V : pt.y + ELBOW_V;

        // Horizontal end (where box edge is)
        const hEndX = goLeft ? tipX - ELBOW_H : tipX + ELBOW_H;

        // Box position
        const bx = goLeft ? hEndX - BOX_W : hEndX;
        const by = goUp ? tipY - BOX_H / 2 : tipY - BOX_H / 2;

        // SVG overlay
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;pointer-events:none;z-index:1001';

        const poly = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
        poly.setAttribute('points', `${pt.x},${pt.y} ${tipX},${tipY} ${hEndX},${tipY}`);
        poly.setAttribute('fill', 'none');
        poly.setAttribute('stroke', color);
        poly.setAttribute('stroke-width', '1.5');
        poly.setAttribute('opacity', '0.85');
        svg.appendChild(poly);

        const dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        dot.setAttribute('cx', pt.x); dot.setAttribute('cy', pt.y);
        dot.setAttribute('r', '4'); dot.setAttribute('fill', color);
        svg.appendChild(dot);

        container.appendChild(svg);

        // Callout box
        const box = document.createElement('div');
        const clampedBy = Math.max(4, Math.min(H - BOX_H - 4, by));
        const clampedBx = Math.max(4, Math.min(W - BOX_W - 4, bx));
        box.style.cssText = `position:absolute;left:${clampedBx}px;top:${clampedBy}px;width:${BOX_W}px;
            z-index:1002;background:#000d24;border:1px solid ${color};border-radius:8px;
            padding:12px 14px;color:#e8eaf0;font-family:system-ui;
            box-shadow:0 0 24px rgba(0,0,0,0.9),0 0 8px ${color}44;`;

        const desc = (ev.description || '').substring(0, 110);
        box.innerHTML =
            '<div style="display:flex;align-items:center;gap:6px;margin-bottom:7px">' +
                '<span style="background:' + color + ';color:#fff;font-size:9px;font-weight:700;padding:1px 5px;border-radius:3px">' + (ev.priority||'') + '</span>' +
                '<span style="font-size:10px;color:#8892a4;flex:1">' + (ev.sectionName||'') + '</span>' +
                '<button id="_mc_close" style="background:none;border:none;color:#8892a4;font-size:14px;cursor:pointer;padding:0;line-height:1">×</button>' +
            '</div>' +
            '<div style="font-size:13px;font-weight:600;line-height:1.3;margin-bottom:6px">' + (ev.title||'') + '</div>' +
            (ev.location ? '<div style="font-size:11px;color:#8892a4;margin-bottom:5px">📍 ' + ev.location + '</div>' : '') +
            (desc ? '<div style="font-size:11px;color:#aab;line-height:1.4;margin-bottom:8px">' + desc + (desc.length >= 110 ? '…' : '') + '</div>' : '') +
            '<div style="font-size:11px;color:#4a90e2;margin-bottom:8px">' + (ev.articleCount||0) + ' artículos · Impacto: ' + ((ev.impactScore||0)).toFixed(0) + '</div>' +
            '<button id="_mc_open" style="background:rgba(74,144,226,0.12);border:1px solid rgba(74,144,226,0.35);' +
                'color:#4a90e2;padding:5px 10px;border-radius:5px;font-size:11px;cursor:pointer;width:100%">' +
                'Ver cronología →</button>';

        container.appendChild(box);

        box.querySelector('#_mc_close').addEventListener('click', (e) => { e.stopPropagation(); _hideCallout(); });
        box.querySelector('#_mc_open').addEventListener('click', (e) => {
            e.stopPropagation();
            if (_dotnet) try { _dotnet.invokeMethodAsync('OpenEventById', ev.id); } catch(_e) {}
            _hideCallout();
        });

        _callout = { svg, box };
    }

    function _hideCallout() {
        if (_callout) {
            try { _callout.svg.remove(); } catch(_e) {}
            try { _callout.box.remove(); } catch(_e) {}
            _callout = null;
        }
    }

    // ── Markers ──────────────────────────────────────────────────────────────
    function _placeMarker(ev, lat, lng) {
        if (!_map) return;
        const color = PRIORITY_COLORS[ev.priority] || '#4a90e2';
        const r = _priorityRadius(ev.priority);

        const icon = L.divIcon({
            className: '',
            html: '<div style="width:' + (r*2) + 'px;height:' + (r*2) + 'px;border-radius:50%;' +
                'background:' + color + ';border:2px solid rgba(255,255,255,0.6);' +
                'box-shadow:0 0 ' + (r*2) + 'px ' + color + ',0 0 ' + r + 'px ' + color + ';cursor:pointer;' +
                'animation:map-pulse 2s ease-in-out infinite alternate"></div>',
            iconSize: [r*2, r*2], iconAnchor: [r, r]
        });

        const marker = L.marker([lat, lng], { icon });
        marker.bindTooltip(_tooltip(ev, color), {
            permanent: false, direction: 'top', offset: [0, -r], opacity: 1,
            className: 'news-map-tooltip'
        });
        marker.on('click', (e) => {
            L.DomEvent.stopPropagation(e);
            _showCallout(ev, lat, lng);
        });
        marker.addTo(_map);
        _markers[ev.id] = { marker, event: ev, lat, lng };
    }

    function _tooltip(ev, color) {
        return '<div style="background:#000d24;border:1px solid #1e3a5f;border-radius:6px;padding:8px 12px;color:#e8eaf0;font-family:system-ui;min-width:180px;max-width:260px">' +
            '<div style="display:flex;align-items:center;gap:6px;margin-bottom:4px">' +
                '<span style="background:' + color + ';color:#fff;font-size:9px;font-weight:700;padding:1px 5px;border-radius:3px">' + (ev.priority||'') + '</span>' +
                '<span style="font-size:10px;color:#8892a4">' + (ev.sectionName||'') + '</span>' +
            '</div>' +
            '<div style="font-size:13px;font-weight:600;line-height:1.3;margin-bottom:4px">' + (ev.title||'') + '</div>' +
            (ev.location ? '<div style="font-size:11px;color:#8892a4;margin-bottom:3px">📍 ' + ev.location + '</div>' : '') +
            '<div style="font-size:11px;color:#4a90e2">' + (ev.articleCount||0) + ' artículos · Impacto: ' + ((ev.impactScore||0)).toFixed(0) + '</div>' +
            '<div style="font-size:10px;color:#556;margin-top:3px">Clic para ver detalle</div>' +
        '</div>';
    }

    // ── Geocoding ─────────────────────────────────────────────────────────────
    async function _geocode(locationStr) {
        const key = locationStr.toLowerCase().trim();

        // Check cache
        if (_geocodeCache[key] !== undefined) return _geocodeCache[key];

        // Check manual region overrides first
        const override = REGION_COORDS[key];
        if (override) {
            _geocodeCache[key] = override;
            return override;
        }

        // Nominatim fallback
        try {
            const url = 'https://nominatim.openstreetmap.org/search?q=' + encodeURIComponent(locationStr) + '&format=json&limit=1';
            const res = await fetch(url, { headers: { 'Accept': 'application/json', 'User-Agent': 'AgenteNews/1.0' } });
            const data = await res.json();
            if (data && data[0]) {
                const result = { lat: parseFloat(data[0].lat), lng: parseFloat(data[0].lon) };
                _geocodeCache[key] = result;
                return result;
            }
        } catch(_e) {}

        _geocodeCache[key] = null;
        return null;
    }

    async function _addEvents(events) {
        const noLoc = [];

        if (!document.getElementById('_map_pulse_style')) {
            const s = document.createElement('style');
            s.id = '_map_pulse_style';
            s.textContent = '@keyframes map-pulse{from{opacity:0.85;transform:scale(1)}to{opacity:1;transform:scale(1.15)}}';
            document.head.appendChild(s);
        }

        for (const ev of events) {
            if (ev.latitude != null && ev.longitude != null) {
                _placeMarker(ev, ev.latitude, ev.longitude);
            } else if (ev.location) {
                await new Promise(r => setTimeout(r, 300));
                const coords = await _geocode(ev.location);
                if (coords) {
                    _placeMarker(ev, coords.lat, coords.lng);
                } else {
                    noLoc.push(ev);
                }
            } else {
                noLoc.push(ev);
            }
        }

        _buildNoLocPanel(noLoc);
    }

    function _buildNoLocPanel(events) {
        const container = _map?.getContainer();
        if (!container || events.length === 0) return;

        if (_noLocPanel) _noLocPanel.remove();
        const panel = document.createElement('div');
        panel.style.cssText =
            'position:absolute;bottom:32px;left:12px;z-index:1000;' +
            'background:rgba(0,5,17,0.92);border:1px solid #1e2a3a;' +
            'border-radius:10px;padding:10px 14px;max-width:260px;max-height:220px;' +
            'overflow-y:auto;color:#e8eaf0;font-family:system-ui;font-size:12px;';

        panel.innerHTML = '<div style="color:#8892a4;font-size:10px;font-weight:700;letter-spacing:.05em;margin-bottom:8px">SIN UBICACIÓN (' + events.length + ')</div>' +
            events.map(ev => {
                const color = PRIORITY_COLORS[ev.priority] || '#4a90e2';
                return '<div style="padding:5px 0;border-bottom:1px solid rgba(255,255,255,0.05);cursor:pointer;display:flex;gap:6px;align-items:flex-start" data-evid="' + ev.id + '">' +
                    '<span style="background:' + color + ';color:#fff;font-size:9px;font-weight:700;padding:1px 4px;border-radius:3px;flex-shrink:0;margin-top:2px">' + (ev.priority||'')[0] + '</span>' +
                    '<span style="line-height:1.3">' + (ev.title||'') + '</span>' +
                '</div>';
            }).join('');

        panel.querySelectorAll('[data-evid]').forEach(el => {
            el.addEventListener('click', () => {
                const id = parseInt(el.getAttribute('data-evid'));
                if (_dotnet) try { _dotnet.invokeMethodAsync('OpenEventById', id); } catch(_e) {}
            });
        });

        container.appendChild(panel);
        _noLocPanel = panel;
    }
})();
