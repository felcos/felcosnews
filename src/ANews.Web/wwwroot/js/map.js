/* ============================================================
   map.js — World map view for AgenteNews using Leaflet.js
   ============================================================ */
(function () {
    'use strict';

    let _map = null;
    let _markers = {};
    let _dotnet = null;
    let _layers = {};

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
            if (_map) {
                _map.remove();
                _map = null;
                _markers = {};
            }

            _dotnet = dotnetRef;

            const container = document.getElementById(containerId);
            if (!container) { console.warn('[map.js] container not found:', containerId); return; }

            // Dark tile layer — Carto Dark Matter (free, no API key needed)
            _map = L.map(containerId, {
                center: [20, 0],
                zoom: 2,
                minZoom: 1,
                maxZoom: 10,
                zoomControl: true
            });

            L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
                attribution: '© OpenStreetMap contributors © CARTO',
                subdomains: 'abcd',
                maxZoom: 19
            }).addTo(_map);

            // Add events
            _addEvents(events || []);

            console.log(`[map.js] init: ${(events||[]).length} events`);
        },

        updateData(events) {
            if (!_map) return;
            // Remove old markers
            Object.values(_markers).forEach(m => m.remove());
            _markers = {};
            _addEvents(events || []);
        },

        filterBySection(sectionSlug) {
            Object.values(_markers).forEach(({ marker, event: ev }) => {
                if (!sectionSlug || sectionSlug === 'all' || ev.sectionSlug === sectionSlug) {
                    marker.addTo(_map);
                } else {
                    marker.remove();
                }
            });
        },

        destroy() {
            if (_map) { _map.remove(); _map = null; }
            _markers = {};
            _dotnet = null;
        }
    };

    function _addEvents(events) {
        events.forEach(ev => {
            if (ev.latitude == null || ev.longitude == null) return;

            const color = PRIORITY_COLORS[ev.priority] || '#4a90e2';
            const r = _priorityRadius(ev.priority);

            const icon = L.divIcon({
                className: '',
                html: `<div style="
                    width:${r*2}px;height:${r*2}px;border-radius:50%;
                    background:${color};
                    border:2px solid rgba(255,255,255,0.6);
                    box-shadow:0 0 ${r*2}px ${color},0 0 ${r}px ${color};
                    cursor:pointer;
                "></div>`,
                iconSize: [r*2, r*2],
                iconAnchor: [r, r]
            });

            const marker = L.marker([ev.latitude, ev.longitude], { icon });

            // Tooltip on hover
            marker.bindTooltip(`
                <div style="background:#000d24;border:1px solid #1e3a5f;border-radius:6px;padding:8px 12px;color:#e8eaf0;font-family:system-ui;min-width:180px;max-width:260px">
                    <div style="display:flex;align-items:center;gap:6px;margin-bottom:4px">
                        <span style="background:${color};color:#fff;font-size:9px;font-weight:700;padding:1px 5px;border-radius:3px">${ev.priority||''}</span>
                        <span style="font-size:10px;color:#8892a4">${ev.sectionName||''}</span>
                    </div>
                    <div style="font-size:13px;font-weight:600;line-height:1.3;margin-bottom:4px">${ev.title||''}</div>
                    <div style="font-size:11px;color:#4a90e2">${ev.articleCount||0} artículos · Impacto: ${(ev.impactScore||0).toFixed(0)}</div>
                </div>
            `, {
                permanent: false,
                direction: 'top',
                offset: [0, -r],
                opacity: 1,
                className: 'news-map-tooltip'
            });

            marker.on('click', () => {
                if (_dotnet) {
                    try { _dotnet.invokeMethodAsync('OpenEventById', ev.id); } catch(e) {}
                }
            });

            marker.addTo(_map);
            _markers[ev.id] = { marker, event: ev };
        });
    }
})();
