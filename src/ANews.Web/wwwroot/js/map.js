/* ============================================================
   map.js — World map view for AgenteNews using Leaflet.js
   ============================================================ */
(function () {
    'use strict';

    let _map = null;
    let _markers = {};
    let _dotnet = null;
    let _noLocPanel = null;
    let _geocodeCache = {}; // location string -> {lat, lng} | null

    const PRIORITY_COLORS = {
        Critical: '#ff0040',
        High: '#ff6600',
        Medium: '#e6b800',
        Low: '#4a90e2'
    };

    function _priorityRadius(priority) {
        return { Critical: 14, High: 11, Medium: 9, Low: 7 }[priority] || 9;
    }

    window.newsMap = {
        init(containerId, events, dotnetRef) {
            if (_map) { _map.remove(); _map = null; _markers = {}; }
            if (_noLocPanel) { _noLocPanel.remove(); _noLocPanel = null; }

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

            _addEvents(events || []);
            console.log(`[map.js] init: ${(events||[]).length} events`);
        },

        updateData(events) {
            if (!_map) return;
            Object.values(_markers).forEach(({ marker }) => marker.remove());
            _markers = {};
            if (_noLocPanel) { _noLocPanel.remove(); _noLocPanel = null; }
            _addEvents(events || []);
        },

        destroy() {
            if (_map) { _map.remove(); _map = null; }
            if (_noLocPanel) { _noLocPanel.remove(); _noLocPanel = null; }
            _markers = {};
            _dotnet = null;
        }
    };

    function _placeMarker(ev, lat, lng) {
        if (!_map) return;
        const color = PRIORITY_COLORS[ev.priority] || '#4a90e2';
        const r = _priorityRadius(ev.priority);

        const icon = L.divIcon({
            className: '',
            html: `<div style="width:${r*2}px;height:${r*2}px;border-radius:50%;
                background:${color};border:2px solid rgba(255,255,255,0.6);
                box-shadow:0 0 ${r*2}px ${color},0 0 ${r}px ${color};cursor:pointer;
                animation:map-pulse 2s ease-in-out infinite alternate"></div>`,
            iconSize: [r*2, r*2], iconAnchor: [r, r]
        });

        const marker = L.marker([lat, lng], { icon });
        marker.bindTooltip(_tooltip(ev, color), {
            permanent: false, direction: 'top', offset: [0, -r], opacity: 1,
            className: 'news-map-tooltip'
        });
        marker.on('click', () => {
            if (_dotnet) try { _dotnet.invokeMethodAsync('OpenEventById', ev.id); } catch(e) {}
        });
        marker.addTo(_map);
        _markers[ev.id] = { marker, event: ev };
    }

    function _tooltip(ev, color) {
        return `<div style="background:#000d24;border:1px solid #1e3a5f;border-radius:6px;padding:8px 12px;color:#e8eaf0;font-family:system-ui;min-width:180px;max-width:260px">
            <div style="display:flex;align-items:center;gap:6px;margin-bottom:4px">
                <span style="background:${color};color:#fff;font-size:9px;font-weight:700;padding:1px 5px;border-radius:3px">${ev.priority||''}</span>
                <span style="font-size:10px;color:#8892a4">${ev.sectionName||''}</span>
            </div>
            <div style="font-size:13px;font-weight:600;line-height:1.3;margin-bottom:4px">${ev.title||''}</div>
            ${ev.location ? `<div style="font-size:11px;color:#8892a4;margin-bottom:3px">📍 ${ev.location}</div>` : ''}
            <div style="font-size:11px;color:#4a90e2">${ev.articleCount||0} artículos · Impacto: ${(ev.impactScore||0).toFixed(0)}</div>
        </div>`;
    }

    async function _geocode(locationStr) {
        if (_geocodeCache[locationStr] !== undefined) return _geocodeCache[locationStr];
        try {
            const url = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(locationStr)}&format=json&limit=1`;
            const res = await fetch(url, { headers: { 'Accept': 'application/json', 'User-Agent': 'AgenteNews/1.0' } });
            const data = await res.json();
            if (data && data[0]) {
                const result = { lat: parseFloat(data[0].lat), lng: parseFloat(data[0].lon) };
                _geocodeCache[locationStr] = result;
                return result;
            }
        } catch(e) {}
        _geocodeCache[locationStr] = null;
        return null;
    }

    async function _addEvents(events) {
        const noLoc = [];

        for (const ev of events) {
            if (ev.latitude != null && ev.longitude != null) {
                _placeMarker(ev, ev.latitude, ev.longitude);
            } else if (ev.location) {
                // Try geocoding — throttle 1 per 300ms to respect Nominatim
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

        // Inject pulse animation if not already done
        if (!document.getElementById('_map_pulse_style')) {
            const s = document.createElement('style');
            s.id = '_map_pulse_style';
            s.textContent = '@keyframes map-pulse{from{opacity:0.85;transform:scale(1)}to{opacity:1;transform:scale(1.15)}}';
            document.head.appendChild(s);
        }

        // Panel for events without location
        _buildNoLocPanel(noLoc);
    }

    function _buildNoLocPanel(events) {
        const container = _map?.getContainer();
        if (!container || events.length === 0) return;

        if (_noLocPanel) _noLocPanel.remove();
        const panel = document.createElement('div');
        panel.style.cssText = `
            position:absolute;bottom:32px;left:12px;z-index:1000;
            background:rgba(0,5,17,0.92);border:1px solid #1e2a3a;
            border-radius:10px;padding:10px 14px;max-width:260px;max-height:220px;
            overflow-y:auto;color:#e8eaf0;font-family:system-ui;font-size:12px;
        `;
        panel.innerHTML = `<div style="color:#8892a4;font-size:10px;font-weight:700;letter-spacing:.05em;margin-bottom:8px">
            SIN UBICACIÓN (${events.length})</div>` +
            events.map(ev => {
                const color = PRIORITY_COLORS[ev.priority] || '#4a90e2';
                return `<div style="padding:5px 0;border-bottom:1px solid rgba(255,255,255,0.05);cursor:pointer;display:flex;gap:6px;align-items:flex-start"
                    data-evid="${ev.id}">
                    <span style="background:${color};color:#fff;font-size:9px;font-weight:700;padding:1px 4px;border-radius:3px;flex-shrink:0;margin-top:2px">${(ev.priority||'')[0]}</span>
                    <span style="line-height:1.3">${ev.title||''}</span>
                </div>`;
            }).join('');

        panel.querySelectorAll('[data-evid]').forEach(el => {
            el.addEventListener('click', () => {
                const id = parseInt(el.getAttribute('data-evid'));
                if (_dotnet) try { _dotnet.invokeMethodAsync('OpenEventById', id); } catch(e) {}
            });
        });

        container.appendChild(panel);
        _noLocPanel = panel;
    }
})();
