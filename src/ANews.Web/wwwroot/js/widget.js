/**
 * AgenteNews Embeddable Widget v1.0
 * Usage: <div id="agentenews-widget" data-theme="dark" data-limit="10" data-section="tech"></div>
 *        <script src="https://news.websoftware.es/js/widget.js" async></script>
 */
(function () {
  'use strict';

  var DEFAULTS = {
    apiUrl: 'https://news.websoftware.es/api/widget/events',
    appUrl: 'https://news.websoftware.es',
    containerId: 'agentenews-widget',
    limit: 10,
    refreshInterval: 300000 // 5 minutes
  };

  var PRIORITY_COLORS = {
    Low: '#22c55e',
    Medium: '#eab308',
    High: '#f97316',
    Critical: '#ef4444'
  };

  function injectStyles() {
    if (document.getElementById('anews-widget-styles')) return;
    var style = document.createElement('style');
    style.id = 'anews-widget-styles';
    style.textContent = [
      ':root .anews-widget {',
      '  --anw-bg: #ffffff;',
      '  --anw-bg-card: #f8f9fa;',
      '  --anw-bg-card-hover: #f0f1f3;',
      '  --anw-text: #1a1a2e;',
      '  --anw-text-secondary: #6b7280;',
      '  --anw-border: #e5e7eb;',
      '  --anw-accent: #4a90e2;',
      '  --anw-tag-bg: #e8f0fe;',
      '  --anw-tag-text: #1a73e8;',
      '  --anw-footer-bg: #f3f4f6;',
      '  --anw-shadow: 0 1px 3px rgba(0,0,0,0.08);',
      '  --anw-radius: 8px;',
      '}',
      '.anews-widget[data-theme="dark"] {',
      '  --anw-bg: #0f172a;',
      '  --anw-bg-card: #1e293b;',
      '  --anw-bg-card-hover: #273548;',
      '  --anw-text: #e2e8f0;',
      '  --anw-text-secondary: #94a3b8;',
      '  --anw-border: #334155;',
      '  --anw-accent: #60a5fa;',
      '  --anw-tag-bg: #1e3a5f;',
      '  --anw-tag-text: #93c5fd;',
      '  --anw-footer-bg: #0c1322;',
      '  --anw-shadow: 0 1px 3px rgba(0,0,0,0.3);',
      '}',
      '.anews-widget {',
      '  font-family: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;',
      '  background: var(--anw-bg);',
      '  border: 1px solid var(--anw-border);',
      '  border-radius: var(--anw-radius);',
      '  overflow: hidden;',
      '  color: var(--anw-text);',
      '  font-size: 14px;',
      '  line-height: 1.5;',
      '  max-width: 480px;',
      '  box-shadow: var(--anw-shadow);',
      '}',
      '.anews-widget * { box-sizing: border-box; margin: 0; padding: 0; }',
      '.anews-widget-header {',
      '  padding: 12px 16px;',
      '  border-bottom: 1px solid var(--anw-border);',
      '  display: flex;',
      '  align-items: center;',
      '  justify-content: space-between;',
      '}',
      '.anews-widget-header-title {',
      '  font-size: 13px;',
      '  font-weight: 600;',
      '  color: var(--anw-text-secondary);',
      '  text-transform: uppercase;',
      '  letter-spacing: 0.5px;',
      '}',
      '.anews-widget-header-dot {',
      '  width: 8px; height: 8px;',
      '  background: #22c55e;',
      '  border-radius: 50%;',
      '  animation: anews-pulse 2s ease-in-out infinite;',
      '}',
      '@keyframes anews-pulse {',
      '  0%, 100% { opacity: 1; }',
      '  50% { opacity: 0.4; }',
      '}',
      '.anews-widget-list {',
      '  list-style: none;',
      '  max-height: 520px;',
      '  overflow-y: auto;',
      '}',
      '.anews-widget-list::-webkit-scrollbar { width: 4px; }',
      '.anews-widget-list::-webkit-scrollbar-track { background: transparent; }',
      '.anews-widget-list::-webkit-scrollbar-thumb { background: var(--anw-border); border-radius: 4px; }',
      '.anews-widget-item {',
      '  padding: 10px 16px;',
      '  border-bottom: 1px solid var(--anw-border);',
      '  transition: background 0.15s ease;',
      '  cursor: pointer;',
      '}',
      '.anews-widget-item:last-child { border-bottom: none; }',
      '.anews-widget-item:hover { background: var(--anw-bg-card-hover); }',
      '.anews-widget-item-top {',
      '  display: flex;',
      '  align-items: center;',
      '  gap: 8px;',
      '  margin-bottom: 4px;',
      '}',
      '.anews-widget-priority {',
      '  width: 8px; height: 8px;',
      '  border-radius: 50%;',
      '  flex-shrink: 0;',
      '}',
      '.anews-widget-section {',
      '  font-size: 11px;',
      '  font-weight: 600;',
      '  padding: 1px 7px;',
      '  border-radius: 10px;',
      '  background: var(--anw-tag-bg);',
      '  color: var(--anw-tag-text);',
      '  white-space: nowrap;',
      '  text-transform: uppercase;',
      '  letter-spacing: 0.3px;',
      '}',
      '.anews-widget-time {',
      '  font-size: 11px;',
      '  color: var(--anw-text-secondary);',
      '  margin-left: auto;',
      '  white-space: nowrap;',
      '}',
      '.anews-widget-title {',
      '  font-size: 13px;',
      '  font-weight: 500;',
      '  color: var(--anw-text);',
      '  text-decoration: none;',
      '  display: block;',
      '  line-height: 1.4;',
      '}',
      '.anews-widget-title:hover { color: var(--anw-accent); }',
      '.anews-widget-empty {',
      '  padding: 32px 16px;',
      '  text-align: center;',
      '  color: var(--anw-text-secondary);',
      '  font-size: 13px;',
      '}',
      '.anews-widget-loading {',
      '  padding: 32px 16px;',
      '  text-align: center;',
      '  color: var(--anw-text-secondary);',
      '  font-size: 13px;',
      '}',
      '.anews-widget-error {',
      '  padding: 16px;',
      '  text-align: center;',
      '  color: #ef4444;',
      '  font-size: 13px;',
      '}',
      '.anews-widget-footer {',
      '  padding: 8px 16px;',
      '  text-align: center;',
      '  background: var(--anw-footer-bg);',
      '  border-top: 1px solid var(--anw-border);',
      '}',
      '.anews-widget-footer a {',
      '  font-size: 11px;',
      '  color: var(--anw-text-secondary);',
      '  text-decoration: none;',
      '  font-weight: 500;',
      '}',
      '.anews-widget-footer a:hover { color: var(--anw-accent); }',
      '.anews-widget-footer a span { color: var(--anw-accent); font-weight: 700; }'
    ].join('\n');
    document.head.appendChild(style);
  }

  function escapeHtml(str) {
    if (!str) return '';
    var div = document.createElement('div');
    div.appendChild(document.createTextNode(str));
    return div.innerHTML;
  }

  function buildUrl(base, params) {
    var parts = [];
    for (var key in params) {
      if (params[key] != null && params[key] !== '') {
        parts.push(encodeURIComponent(key) + '=' + encodeURIComponent(params[key]));
      }
    }
    return base + (parts.length ? '?' + parts.join('&') : '');
  }

  function ANewsWidget(container) {
    this.container = container;
    this.theme = container.getAttribute('data-theme') || 'light';
    this.limit = parseInt(container.getAttribute('data-limit'), 10) || DEFAULTS.limit;
    this.section = container.getAttribute('data-section') || null;
    this.apiUrl = container.getAttribute('data-api-url') || DEFAULTS.apiUrl;
    this.appUrl = container.getAttribute('data-app-url') || DEFAULTS.appUrl;
    this.refreshTimer = null;

    this.init();
  }

  ANewsWidget.prototype.init = function () {
    this.container.classList.add('anews-widget');
    if (this.theme === 'dark') {
      this.container.setAttribute('data-theme', 'dark');
    }
    this.render();
    this.fetchEvents();
    this.startAutoRefresh();
  };

  ANewsWidget.prototype.render = function () {
    this.container.innerHTML = [
      '<div class="anews-widget-header">',
      '  <span class="anews-widget-header-title">Noticias en vivo</span>',
      '  <span class="anews-widget-header-dot"></span>',
      '</div>',
      '<div class="anews-widget-body">',
      '  <div class="anews-widget-loading">Cargando noticias...</div>',
      '</div>',
      '<div class="anews-widget-footer">',
      '  <a href="' + escapeHtml(this.appUrl) + '" target="_blank" rel="noopener">',
      '    Powered by <span>AgenteNews</span>',
      '  </a>',
      '</div>'
    ].join('\n');
    this.body = this.container.querySelector('.anews-widget-body');
  };

  ANewsWidget.prototype.fetchEvents = function () {
    var self = this;
    var url = buildUrl(this.apiUrl, {
      limit: this.limit,
      section: this.section
    });

    fetch(url)
      .then(function (res) {
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return res.json();
      })
      .then(function (events) {
        self.renderEvents(events);
      })
      .catch(function (err) {
        self.renderError(err.message);
      });
  };

  ANewsWidget.prototype.renderEvents = function (events) {
    if (!events || events.length === 0) {
      this.body.innerHTML = '<div class="anews-widget-empty">No hay noticias recientes</div>';
      return;
    }

    var html = '<ul class="anews-widget-list">';
    for (var i = 0; i < events.length; i++) {
      var ev = events[i];
      var priorityColor = PRIORITY_COLORS[ev.priority] || PRIORITY_COLORS.Medium;
      var eventUrl = ev.url || (this.appUrl + '/?eventId=' + ev.id);

      html += '<li class="anews-widget-item" onclick="window.open(\'' + escapeHtml(eventUrl) + '\',\'_blank\')">';
      html += '  <div class="anews-widget-item-top">';
      html += '    <span class="anews-widget-priority" style="background:' + priorityColor + '"></span>';
      html += '    <span class="anews-widget-section">' + escapeHtml(ev.section) + '</span>';
      html += '    <span class="anews-widget-time">' + escapeHtml(ev.timeAgo) + '</span>';
      html += '  </div>';
      html += '  <a class="anews-widget-title" href="' + escapeHtml(eventUrl) + '" target="_blank" rel="noopener" onclick="event.stopPropagation()">';
      html += escapeHtml(ev.title);
      html += '  </a>';
      html += '</li>';
    }
    html += '</ul>';
    this.body.innerHTML = html;
  };

  ANewsWidget.prototype.renderError = function (msg) {
    this.body.innerHTML = '<div class="anews-widget-error">Error al cargar: ' + escapeHtml(msg) + '</div>';
  };

  ANewsWidget.prototype.startAutoRefresh = function () {
    var self = this;
    this.refreshTimer = setInterval(function () {
      self.fetchEvents();
    }, DEFAULTS.refreshInterval);
  };

  ANewsWidget.prototype.destroy = function () {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
    this.container.innerHTML = '';
    this.container.classList.remove('anews-widget');
  };

  // --- Bootstrap ---

  function bootstrap() {
    injectStyles();
    var containers = document.querySelectorAll('#' + DEFAULTS.containerId + ', [data-anews-widget]');
    for (var i = 0; i < containers.length; i++) {
      if (!containers[i]._anewsWidget) {
        containers[i]._anewsWidget = new ANewsWidget(containers[i]);
      }
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bootstrap);
  } else {
    bootstrap();
  }

  // Expose for programmatic use
  window.ANewsWidget = ANewsWidget;

})();
