# AgenteNews — Plan Maestro de Desarrollo

## Estado del Proyecto
- **Última revisión:** 2026-03-09
- **Fase actual:** Sprint 6 COMPLETADO

---

## Infraestructura / Despliegue
| Item | Estado |
|---|---|
| .NET 8 + PostgreSQL + Redis en VM Oracle Cloud | ✅ |
| Systemd service + nginx reverse proxy | ✅ |
| HTTPS (Let's Encrypt) `news.websoftware.es` | ✅ |
| HTTPS landing `websoftware.es` | ✅ |
| Login funcional (fix Blazor Server cookie) | ✅ |
| Logout funcional | ✅ |
| Repositorio git local inicializado (commit `7d26a49`) | ✅ |
| GitHub repo `felcos/felcosnews` — push pendiente (manual) | ⏳ |
| CI/CD GitHub Actions → VM (requiere secrets en GitHub) | ⏳ |

---

## Sprint 1 — Core ✅ COMPLETADO

### Bugs críticos
| # | Descripción | Estado |
|---|---|---|
| B-01 | Logout roto (Blazor Server cookie issue) | ✅ |
| B-02 | App vacía al arrancar (sin secciones, fuentes, proveedor IA) | ✅ |
| B-03 | TriggerAgentAsync en GodMode es un stub | ✅ |

### Features
| # | Descripción | Estado |
|---|---|---|
| F-01 | Mecanismo trigger de agentes (BaseAgent.TriggerNow) | ✅ |
| F-02 | Endpoint HTTP POST /api/admin/agents/{type}/trigger | ✅ |
| F-03 | Botón "Ejecutar ahora" en AgentsMonitor y GodMode | ✅ |
| F-04 | Feed de logs en vivo en AgentsMonitor (SignalR) | ✅ |
| F-05 | Seed data rico: secciones temáticas + RSS + Claude placeholder | ✅ |
| F-06 | Redespliegue en VM tras cada cambio | ✅ |
| F-07b | Register fix (Blazor Server cookie) | ✅ |
| F-08b | Página edición evento /admin/events/{id} | ✅ |
| F-09b | Crear evento desde NewsManagement | ✅ |

---

## Sprint 2 — Contenido y Agentes IA ✅ COMPLETADO

| # | Descripción | Estado |
|---|---|---|
| F-07 | EventDetectorAgent: clasificación real con IA | ✅ |
| F-08 | AlertGeneratorAgent: alertas con criterios configurables | ✅ |
| F-09 | Dashboard de usuario: eventos filtrados por módulos | ✅ |
| F-10 | RSS feed por módulo de usuario (/api/rss/{token}) | ✅ |
| F-11 | Notificaciones Telegram/Email funcionales | ✅ |
| F-12 | Página de artículo individual con resumen IA (/article/{id}) | ✅ |

### Fixes adicionales Sprint 2
- AiConfigs: inyectar AiProviderFactory desde DI (fix NullRef en TestConnection)
- OpenAiProvider: fix URL base para Groq/compatibles (relative path en PostAsync)
- TelegramService: no lanza si BotToken no está configurado (graceful)
- Notifications: TestAsync usa INotificationService real; SendVerification real
- Index.razor: artículos en modal linkan a /article/{id}

---

## Sprint 3 — Navegación, Usabilidad y Producción (EN PROGRESO)

### Bugs críticos resueltos en Sprint 3
| # | Descripción | Estado |
|---|---|---|
| B-04 | SetupSignalRAsync en OnInitializedAsync → retry loop prerendering | ✅ |
| B-05 | nginx proxy_send_timeout bajo → drops WebSocket | ✅ |
| B-06 | EventDetector ventana 3h → artículos sin clasificar | ✅ |
| B-07 | AiProviderFactory pasaba API key en base64 sin decodificar → 401 Groq | ✅ |
| B-08 | EventDetectorAgent: JSON snake_case no mapeaba a C# PascalCase → article_indices vacíos → 0 eventos creados | ✅ |
| B-09 | COMException crash en aarch64/Oracle Cloud → JIT tiered compilation bug | ✅ DOTNET_TieredCompilation=0 |
| B-10 | JS interop sin try-catch → excepciones no capturadas crasheaban el circuito | ✅ |

### Usabilidad
| # | Descripción | Estado | Prioridad |
|---|---|---|---|
| U-01 | Indicador de carga mientras Blazor conecta (skeleton UI) | ✅ | Media |
| U-02 | Búsqueda en portada pública (texto libre sobre eventos) | ✅ | Alta |
| U-03 | Paginación en NewsManagement (DB-level, server-side) | ✅ | Alta |
| U-04 | Feedback visual al guardar en formularios (toast) | ✅ | Media |
| U-05 | Navegación móvil (sidebar responsive, hamburger) | ✅ | Media |
| U-06 | Preview de artículos en lista antes de abrir modal | ✅ Toggle inline en modal | Baja |
| U-07 | Confirmación de acciones destructivas (confirm dialog) | ✅ | Media |
| U-08 | Breadcrumb en panel admin con nombre de página real | ✅ | Baja |
| U-09 | Empty states con call-to-action | ✅ | Media |
| U-10 | Ordenación de columnas en tablas admin | ✅ | Baja |

### Features Sprint 3
| # | Descripción | Estado |
|---|---|---|
| F-13 | Rate limiting y seguridad API | ✅ |
| F-14 | Internacionalización (i18n) | ⬜ Postergado |
| F-15 | CI/CD pipeline (GitHub Actions → VM) | ✅ .github/workflows/deploy.yml |
| F-16 | Backups automáticos PostgreSQL | ✅ pg_dump diario vía cron |
| F-17 | Monitorización (healthchecks, alertas de caída) | ✅ /health/detail endpoint |
| F-18 | Búsqueda full-text en eventos (portada) | ✅ (cliente, ver U-02) |
| F-19 | Sistema de usuarios: verificación email real | ✅ IdentityEmailSender + /account/confirm-email |
| F-20 | Resumen de artículo por IA al escanear (automático) | ✅ ArticleSummarizerAgent (cada 45 min) |

### Features Histórico/Evolución (NUEVO)
| # | Descripción | Estado |
|---|---|---|
| H-A | Lifecycle automático: archivado tras 14 días sin actividad (AlertGeneratorAgent) | ✅ |
| H-B | Deduplicación de eventos: merge de clusters similares en EventDetectorAgent | ✅ |
| H-C | Página /situation/{id} con timeline completo y gráfica ImpactScore | ✅ |
| H-D | Página /explore con selector de fechas y vista "¿qué pasaba el X?" | ✅ |
| H-E | UI de purga controlada en /admin/news (borrar eventos > N días) | ✅ |

---

## Sprint 4 — Agentes Avanzados y UX ✅ COMPLETADO

| # | Descripción | Estado |
|---|---|---|
| S4-01 | Fix JSONB cardinality en ArticleSummarizerAgent (`!Any()`) | ✅ |
| S4-02 | DigestSenderAgent: email digest periódico por frecuencia de usuario | ✅ |
| S4-03 | Filtrar keywords placeholder ("ai"/"rss") en ArticlePage | ✅ |
| S4-04 | Dashboard: eventos recientes clickables → abre modal en portada | ✅ |
| S4-05 | Index.razor: auto-abrir evento desde query param `?eventId=` | ✅ |
| S4-06 | LastDigestSentAt en ApplicationUser + migración AddDigestTracking | ✅ |
| S4-07 | DigestSenderAgent registrado en GodMode, AgentsMonitor, trigger endpoint | ✅ |
| S4-08 | WhatsAppService: graceful si Twilio no configurado (no crashea) | ✅ |
| S4-09 | Universe v2: callout HTML con línea elbow 90° al hacer hover | ✅ |
| S4-10 | Universe v2: texto en planeta con sombra para contraste garantizado | ✅ |
| S4-11 | Universe v2: anillo verde en planetas que coinciden con módulos del usuario | ✅ |
| S4-12 | Index.razor: carga keywords de módulos del usuario logueado → canvas | ✅ |
| S4-13 | Git repo inicializado con .gitignore correcto (excluye secretos) | ✅ |

---

## Sprint 5 — Producción Real y Canales Funcionales (PRÓXIMO)

### Prerequisitos de infraestructura
| # | Descripción | Estado |
|---|---|---|
| P-01 | Push a GitHub `felcos/felcosnews` + configurar secrets CI/CD | ⬜ |
| P-02 | `VM_SSH_KEY`, `VM_HOST=79.72.56.98`, `VM_USER=ubuntu` en GitHub Secrets | ⬜ |
| P-03 | Deploy del código actual con migración `AddDigestTracking` aplicada en producción | ⬜ |

### Canales de notificación end-to-end
| # | Descripción | Estado |
|---|---|---|
| C-01 | Telegram: bot creado con @BotFather + `Telegram:BotToken` en appsettings.Production.json | ⬜ |
| C-02 | Email: SMTP configurado (Gmail App Password o Brevo/SendGrid) | ⬜ |
| C-03 | Discord: webhook URL por usuario (ya funciona si el usuario la pone) | ⬜ |
| C-04 | WhatsApp: Twilio account + sandbox (o número verificado) | ⬜ |
| C-05 | Test de cada canal desde `/user/notifications` → botón "Probar" | ⬜ |

### Mejoras UX prioritarias
| # | Descripción | Estado |
|---|---|---|
| U-11 | Bookmark desde ArticlePage (botón "Guardar" para usuarios logueados) | ⬜ |
| U-12 | Indicador numérico de módulos coincidentes en header del universo | ⬜ |
| U-13 | Búsqueda en módulos del usuario (filter bar en /user/modules) | ⬜ |
| U-14 | Notificación in-app (badge en el icono de campana del header) | ⬜ |

### Features nuevas
| # | Descripción | Estado |
|---|---|---|
| F-21 | Trending: sección "Más activo hoy" en portada (eventos con más artículos nuevos) | ⬜ |
| F-22 | Universe: modo "Mis Módulos" filtrado — solo muestra eventos que coinciden con el usuario | ⬜ |
| F-23 | API pública documentada con Swagger/OpenAPI | ⬜ |
| F-24 | Exportar eventos a CSV/JSON desde /admin/news | ⬜ |

---

## Contratos de Componentes (NO cambiar sin actualizar aquí)

### BaseAgent (Infrastructure/Agents/BaseAgent.cs)
```
public void TriggerNow()
  - Libera semáforo interno (_triggerNow)
  - El ciclo ExecuteAsync despierta inmediatamente
  - Si ya hay un ciclo en ejecución, encola uno más (máx 1)

public void UpdateInterval(int minutes)
  - Actualiza _interval en memoria SIN reiniciar el HostedService
  - El nuevo intervalo aplica desde el próximo ciclo de espera

public void SetEnabled(bool enabled)
  - Si false: el agente hace await _triggerNow.WaitAsync() indefinido
  - Si true: se reactiva en el siguiente ciclo

protected abstract TimeSpan DefaultInterval
  - Usado cuando no hay AgentConfig en DB para este agente
  - Siempre renombrar de "Interval" a "DefaultInterval" en nuevos agentes
```

### DI Registration (Infrastructure/DependencyInjection.cs)
```
Agentes se registran como:
  services.AddSingleton<XxxAgent>();
  services.AddHostedService(sp => sp.GetRequiredService<XxxAgent>());
→ Permite resolverlos desde DI para llamar TriggerNow()
```

### API Endpoint Agent Trigger (Web/Program.cs)
```
POST /api/admin/agents/{type}/trigger
  Requiere: Authorize("RequireAdmin")
  type: newsscanner | eventdetector | alertgenerator | notificationdispatcher
  200: { "message": "Agente activado", "agent": "..." }
  404: tipo no encontrado
```

### RSS Feed por Módulo (Web/Program.cs)
```
GET /api/rss/{token}
  Sin autenticación (el token es el secreto)
  Devuelve RSS XML con eventos que coincidan con keywords del módulo
  token: UserModule.RssFeedToken (GUID generado al activar RSS)
```

### Logout Endpoint (Web/Program.cs)
```
GET /account/logout
  - Llama SignInManager.SignOutAsync()
  - Redirige a "/"
```

### Login Endpoint (Web/Program.cs)
```
POST /account/login
  form: email, password, rememberMe, returnUrl
  200: redirect a returnUrl o "/"
  error: redirect a /login?error=invalid|locked
```

### AI Provider (Domain/Interfaces/IAiProvider.cs)
```
IAiProvider.CompleteAsync(AiRequest, CancellationToken) → AiResponse
AiProviderFactory.GetDefaultProviderAsync() → lanza si no hay config activa
→ Los agentes deben manejar gracefully el caso de no tener proveedor
```

### Seed Data (contratos de unicidad)
```
Secciones: upsert por Slug con IgnoreQueryFilters() — actualiza nombre/color/icono si ya existe
Fuentes RSS: upsert por Url (no duplicar)
AI Config: crear solo si Count == 0 (placeholder inactivo)
AgentConfig: 7 filas sembradas en migración (una por agente registrado)
```

### Patrón SignalR en Blazor Server
```
CRÍTICO: HubConnection.StartAsync() NO puede llamarse en OnInitializedAsync()
  → OnInitializedAsync corre durante server-side prerendering (sin browser)
  → StartAsync falla → AutoReconnect entra en retry loop → degrada circuito
CORRECTO: Llamar SetupSignalRAsync() desde OnAfterRenderAsync(firstRender)
  → OnAfterRenderAsync sólo corre en el browser, nunca en prerendering
```

---

## Decisiones Arquitectónicas (ADRs)

### ADR-001: Auth con HTTP endpoints, no Blazor components
**Motivo:** Blazor Server corre sobre SignalR; cookies de autenticación no se pueden establecer desde el circuito. Login y logout requieren endpoints HTTP reales.
**Impacto:** Cualquier acción que modifique cookies debe ser un endpoint HTTP (MapPost/MapGet), no código Blazor.

### ADR-002: Agentes como Singletons accesibles + HostedService
**Motivo:** BackgroundService registrado solo con AddHostedService no es resolvible desde DI. Para poder llamar TriggerNow() desde endpoints HTTP se necesita resolución directa.
**Impacto:** Al añadir un nuevo agente, registrar con AMBOS: AddSingleton<T>() + AddHostedService(sp => sp.GetRequiredService<T>()).

### ADR-003: ForwardedHeaders antes de UseHttpsRedirection
**Motivo:** nginx termina TLS. Sin UseForwardedHeaders, el app ve HTTP y redirige a HTTPS interno → SignalR negotiate (POST) recibe 301 → sigue como GET → 405.
**Impacto:** No mover ni eliminar UseForwardedHeaders de Program.cs.

### ADR-004: API Keys en base64 (temporal)
**Motivo:** No hay HSM ni vault disponible. Base64 como ofuscación mínima en DB.
**Pendiente:** Implementar cifrado real con AES + clave derivada de config en Sprint 3.

### ADR-005: NewsScanner → eventos "Unclassified" → EventDetector clasifica con IA
**Motivo:** Separar responsabilidades. Scanner solo descarga, Detector añade inteligencia.
**Impacto:** EventDetector requiere un AiProviderConfig activo y default. Si no hay, falla gracefully con log Warning.

### ADR-006: SignalR clients en OnAfterRenderAsync, no OnInitializedAsync
**Motivo:** Con ServerPrerendered, OnInitializedAsync corre server-side sin WebSocket. HubConnection.StartAsync() falla y AutoReconnect entra en loop infinito degradando el circuito.
**Impacto:** Cualquier componente que cree HubConnection debe moverlo a OnAfterRenderAsync(firstRender).

### ADR-008: System.Text.Json snake_case ↔ PascalCase
**Motivo:** `PropertyNameCaseInsensitive = true` en System.Text.Json NO convierte snake_case a PascalCase (p.ej. `article_indices` → `ArticleIndices`). Requiere `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` O `[JsonPropertyName("...")]` en los DTOs.
**Impacto:** Cualquier deserialización de respuestas IA en JSON con snake_case necesita ambas opciones o atributos explícitos.

### ADR-009: DOTNET_TieredCompilation=0 en Oracle Cloud ARM64
**Motivo:** Oracle Cloud Free Tier usa instancias Ampere ARM (aarch64). .NET 8 tiene un bug de JIT en modo tiered compilation en aarch64 que causa `COMException (0x80131130): Unable to get nested type properties` con crash SIGABRT del proceso.
**Impacto:** Añadir `DOTNET_TieredCompilation=0` y `DOTNET_TieredPGO=0` al service de systemd. Sin esto el proceso se reinicia cada 3-4 minutos.

### ADR-010: JS interop en Blazor Server — siempre con try-catch
**Motivo:** `JS.InvokeVoidAsync()` lanza `JSException` si el componente está siendo eliminado, el circuito está desconectado, o el JS target no existe. Sin try-catch, la excepción escapa al thread pool como unobserved y crashea el proceso.
**Impacto:** Todo llamado a `JS.InvokeVoidAsync` y `JS.InvokeAsync` debe estar envuelto en try-catch.

### ADR-011: EventDetector 2-Phase architecture
**Motivo:** Con secciones generadas por la fuente (Reuters World = Geopolítica), artículos de crimen/salud/cultura acababan clasificados en secciones incorrectas. No había "Justicia & Crimen", así que violaciones aparecían en Ciberguerra.
**Solución:** Fase 1 (Section Router) reasigna artículos a sección correcta por contenido, Fase 2 (Clusterer) agrupa dentro de la sección. El router tiene temperatura 0.0 y reglas explícitas.
**Impacto:** EventDetector procesa más tokens por ciclo. Añadir OperationTag diferente por fase para desglose de costes.

### ADR-012: AgentConfig en DB para intervalos hot-patchables
**Motivo:** Cambiar el intervalo de un agente requería modificar código y redesplegar.
**Solución:** `AgentConfig` en DB (unique index en AgentType). `BaseAgent` carga config en arranque, expone `UpdateInterval()` y `SetEnabled()`. GodMode persiste y aplica sin restart.
**Impacto:** Todos los agentes deben usar `DefaultInterval` (no `Interval`) como fallback.

### ADR-013: Razor switch expressions con patrones relacionales `< N`
**Motivo:** El compilador Razor parsea `< 60` en switch expressions dentro de `@code {}` como etiqueta HTML de apertura → `RZ1006` + errores de tag sin cerrar.
**Solución:** Usar if/else chains en lugar de switch con patrones relacionales en bloques `@code {}`.
**Impacto:** Cualquier switch con `< N`, `> N`, `<= N` en .razor debe convertirse a if/else.

### ADR-007: OpenAI-compatible providers necesitan BaseAddress con /v1/ incluido
**Motivo:** PostAsync con path absoluto "/v1/chat/completions" ignora el path del BaseAddress. Para Groq (base: https://api.groq.com/openai/v1), esto resulta en 404.
**Impacto:** BaseAddress siempre termina en "/" y paths de endpoints son relativos ("chat/completions").

---

## Estructura de Archivos Clave

```
src/
  ANews.Domain/
    Entities/         ← Entidades puras, sin dependencias
    Enums/Enums.cs    ← TODOS los enums aquí
    Interfaces/       ← IAiProvider, INotificationService, IRepository
  ANews.Infrastructure/
    Agents/           ← BackgroundServices (BaseAgent + 4 agentes)
    AI/               ← Claude, OpenAI, Gemini, Kimi, Groq, Factory
    Data/AppDbContext.cs  ← ApplicationUser definido AQUÍ
    DependencyInjection.cs
  ANews.Web/
    Pages/Admin/      ← GodMode, AgentsMonitor, AiConfigs, Sources, Sections...
    Pages/User/       ← Dashboard, MyModules, Bookmarks, Notifications, ApiTokens
    Pages/Auth/       ← Login, Logout, Register
    Pages/Public/     ← Index (portada pública), ArticlePage
    Hubs/             ← NewsHub, AdminHub, AgentMonitorHub
    Program.cs        ← Configuración completa + seed + endpoints HTTP
```

---

## Convenciones de Código

- UI en español (labels, mensajes, logs)
- CSS vars: `--accent: #4a90e2`, `--accent-green: #00ff88`, `--accent-red: #ff0040`
- Fondo oscuro space theme: `--bg: #0a0e1a`
- Sin emojis en código (solo en strings UI si procede)
- Logs de agentes: `[{AgentName}] mensaje` con Serilog
- Todos los endpoints admin: `RequireAuthorization("RequireAdmin")`

---

## Notas de Despliegue

```bash
# Build y publicar
dotnet publish src/ANews.Web/ANews.Web.csproj -c Release -o publish/

# Subir a VM (ver VM_HOST y SSH key en GitHub Secrets o appsettings)
scp -i $SSH_KEY -r publish/. $VM_USER@$VM_HOST:/opt/anews/

# Reiniciar servicio
ssh -i $SSH_KEY $VM_USER@$VM_HOST "sudo systemctl restart anews"

# Ver logs
ssh -i $SSH_KEY $VM_USER@$VM_HOST "sudo journalctl -u anews -f"
```

**DB Produccion:** `anews_prod`, usuario `anews`, contrasena en `/opt/anews/appsettings.Production.json`
**Admin login:** Ver `appsettings.Production.json` en la VM (no almacenar credenciales en el repo)

---

## Sprint 5 — Workspace Geográfico + Mejoras UX (2026-03-09)

### Objetivo
Permitir al usuario enfocar la experiencia en uno o varios países/regiones, con la IA filtrando eventos, descubriendo fuentes y agrupando noticias con contexto geográfico preciso.

### Entregado en este sprint

| # | Feature | Estado |
|---|---|---|
| S5-01 | Workspace picker overlay (selección geográfica al entrar) | ✅ |
| S5-02 | Geo-filtro en FilteredEvents (España, Venezuela, México, etc.) | ✅ |
| S5-03 | Combinaciones personalizadas de países (España+Venezuela) | ✅ |
| S5-04 | Persistencia de workspace en localStorage | ✅ |
| S5-05 | Indicador de workspace activo en header | ✅ |
| S5-06 | Prompt de EventDetectorAgent completamente reescrito | ✅ |
| S5-07 | Geocodificación automática en EventDetectorAgent (Nominatim) | ✅ |
| S5-08 | Fix "Middle East → USA" — diccionario de regiones en map.js y GodMode | ✅ |
| S5-09 | Callout en mapa igual que planetas (punto + línea L + caja resumen) | ✅ |
| S5-10 | Botón "Geocodificar eventos en mapa" en God Mode (lógica directa, sin HTTP) | ✅ |
| S5-11 | Planetas con apariencia realista (8 tipos: rocoso, gaseoso, hielo, etc.) | ✅ |
| S5-12 | Toggle ES/EN en resúmenes de artículos (ArticlePage y modal Timeline) | ✅ |
| S5-13 | Descubrimiento de fuentes con IA (SourcesManagement) | ✅ |
| S5-14 | Selector de tema (8 temas CSS) con persistencia localStorage | ✅ |
| S5-15 | Section strip rediseñada (chips icon+expand, scroll horizontal) | ✅ |
| S5-16 | Fix ruta de despliegue: /opt/anews/ (no /opt/anews/app/) | ✅ |

### Presets de workspace disponibles
- 🌍 Internacional (global, sin filtro)
- 🇪🇸 España, 🇻🇪 Venezuela, 🇲🇽 México, 🇺🇦 Ucrania, ⚔️ Israel/Gaza, 🇺🇸 EEUU, 🇨🇳 China, 🇷🇺 Rusia
- Combinación personalizada: cualquier nombre separado por comas

### Geo-matching logic
- Cada workspace tiene una lista de términos de búsqueda (país, capital, adjetivo gentilicio, líderes)
- Se compara contra `Location + Title + Description` del evento (case-insensitive)
- Ejemplo: "España" → busca Spain, España, Spanish, Madrid, Barcelona, Catalonia...

### Arquitectura del workspace
```
localStorage["anews-workspace"] = "Global" | "España" | "España+Venezuela"
_geoFilter: string[] → términos para filtrar FilteredEvents
_geoTerms: Dictionary<string, string[]> → expansión de términos por país
MatchesGeoFilterFor(ev, geo) → bool
```

### Pendiente (Sprint 6) → COMPLETADO ver abajo

---

## Sprint 6 — Clasificación Inteligente + GodMode Total Control (2026-03-09)

### Problema raíz resuelto
El EventDetectorAgent asignaba artículos al evento "Unclassified" de la sección de la *fuente*, no al evento correcto por *contenido*. Una violación sexual de Reuters World (Geopolítica) acababa en Geopolítica. Con solo 8 secciones especializadas en seguridad, no había donde encajar crimen, salud, cultura, etc.

### Taxonomía de secciones rediseñada (19 secciones)
Inspirada en Reuters, BBC, AP, Al Jazeera. Upsert en Program.cs al arrancar:

| Slug | Nombre | Descripción |
|---|---|---|
| mundo | Mundo | Noticias internacionales generales |
| politica | Política | Gobiernos, elecciones, legislación |
| economia | Economía | Mercados, finanzas, macro |
| negocios | Negocios | Empresas, M&A, startup |
| tecnologia | Tecnología | Tech, software, innovación |
| ciencia | Ciencia | Investigación, espacio, descubrimientos |
| salud | Salud | Medicina, pandemias, salud pública |
| sociedad | Sociedad | Educación, migración, derechos humanos |
| justicia | Justicia & Crimen | Tribunales, crimen organizado, violencia |
| conflictos | Conflictos | Guerras, conflictos armados |
| seguridad | Seguridad | Terrorismo, crimen organizado, mafias |
| ciberseguridad | Ciberseguridad | Hackeos, ransomware, ciberataques |
| medioambiente | Medio Ambiente | Clima, energía, catástrofes naturales |
| cultura | Cultura & Arte | Arte, literatura, patrimonio |
| deportes | Deportes | Deporte profesional, competiciones |
| entretenimiento | Entretenimiento | Celebs, cine, música, farándula |
| geopolitica | Geopolítica | Relaciones internacionales, diplomacia |
| inteligencia | Inteligencia | Servicios secretos, espionaje |
| nbq | NBQ & Armas | Armas biológicas, químicas, nucleares |

### EventDetectorAgent — Arquitectura 2 fases

**Fase 1 — Section Router** (`ReclassifyArticlesBySectionAsync`):
- Procesa hasta 80 artículos pendientes por ciclo
- Un call AI asigna cada artículo al slug correcto según título+descripción
- Reglas explícitas en el prompt:
  - Violencia sexual, asesinatos, corrupción, tribunales → `justicia`
  - Hackeos, ransomware, ciberataques → `ciberseguridad`
  - Celebrities, series, espectáculos → `entretenimiento`
  - Terrorismo, crimen organizado → `seguridad`
- Mueve el artículo al evento "Unclassified" de la sección correcta
- `MaxTokens=1200, Temperature=0.0, OperationTag="section_routing"`

**Fase 2 — Event Clusterer** (`ClusterArticlesWithAiAsync`):
- Agrupa artículos por sección, luego llama AI por cada sección
- Crea/actualiza eventos con título, descripción, prioridad, categorías
- Contexto de sección en el prompt para guiar la clasificación intra-sección
- `MaxTokens=4000, Temperature=0.1, OperationTag="event_detection"`

### AgentConfig — Configuración hot-patch de agentes

Nueva entidad en DB (`AgentConfig`):
- `AgentType` (unique string), `IntervalMinutes`, `IsEnabled`, `MaxItemsPerCycle`, `Notes`
- Migración: `20260309202951_AddAgentConfig`

`BaseAgent` actualizado:
- `protected abstract TimeSpan DefaultInterval` (fallback si no hay DB config)
- `_interval` mutable + `UpdateInterval(int minutes)` — cambia intervalo sin reiniciar
- `IsAgentEnabled` + `SetEnabled(bool)` — pausa/resume sin reiniciar
- Carga config de DB al arrancar en `ExecuteAsync`

### GodMode — Rediseño completo con sidebar

Navegación: Dashboard | Agentes | Fuentes | Sistema

**Panel Agentes:**
- Tabla `AgentConfig` con toggle enabled + input intervalo por agente
- `SaveAgentConfigsAsync()` persiste en DB y hot-patches los agentes en memoria
- Trigger "Ejecutar ahora" + countdown de próxima ejecución (se actualiza solo)
- Gráfico de costes IA redimensionado (`height=90`)

**Panel Sistema:**
- Métricas de proceso en vivo: RAM, uptime, threads, GC, CPU%
- Tabla de proveedores IA con estado
- Log search/pause/export
- Audit log filtrado

**Panel Fuentes:**
- Tabla de salud de fuentes (reliability %, dot de estado ok/warn/fail)

### SourcesManagement — Bulk operations + filtros

Nuevos filtros:
- Por sección (dropdown con todas las secciones)
- Por salud (ok/error/nunca escaneada)

Operaciones por lotes sobre los resultados filtrados:
- **Activar todas** — enable todas las fuentes visibles
- **Desactivar todas** — disable todas las visibles
- **Resetear errores** — FailedScans=0, ErrorMessage=null en todas las visibles
- **Eliminar filtradas** — borrado con confirmación de todas las visibles

### Selector de tema — Rediseño orbital

8 temas con nombre de planeta: Void, Pulsar, Neptune, Matrix, Sol, Neon, Cosmos, Nova
- Trigger: pequeño orbe luminoso con anillo orbital discontinuo animado
- Dropdown: grid 4 columnas con planet-orbit-ring en el activo
- `color-mix()` para gradientes radiales dinámicos
- `@keyframes planet-orbit-spin` para rotación del anillo

### Entregado en Sprint 6

| # | Feature | Estado |
|---|---|---|
| S6-01 | AgentConfig entity + migración BD (hot-patch sin reiniciar) | ✅ |
| S6-02 | BaseAgent: DefaultInterval + UpdateInterval + SetEnabled | ✅ |
| S6-03 | EventDetectorAgent: arquitectura 2 fases (Section Router + Clusterer) | ✅ |
| S6-04 | Taxonomía 19 secciones (upsert en Program.cs) | ✅ |
| S6-05 | GodMode: sidebar 4 secciones, CRUD AgentConfig hot-patch | ✅ |
| S6-06 | GodMode: métricas proceso, audit log, log export, countdown próxima ejecución | ✅ |
| S6-07 | SourcesManagement: filtro por sección y por salud | ✅ |
| S6-08 | SourcesManagement: bulk enable/disable/reset-errors/delete filtradas | ✅ |
| S6-09 | Selector de tema orbital (8 planetas con anillo animado) | ✅ |
| S6-10 | Gráfico de costes redimensionado (height=90) | ✅ |
| S6-11 | admin.js: downloadTextFile helper para exportar logs | ✅ |
| S6-12 | Fix: FormatInterval usa if/else (no switch con `< N` en Razor) | ✅ |
| S6-13 | Fix: AuditLog usa Timestamp/UserEmail/EntityType (nombres reales) | ✅ |

### Pendiente (Sprint 7)
- [ ] Reclasificación masiva de eventos antiguos (trigger "Reclasificar todo" en GodMode)
- [ ] Fuentes RSS para nuevas secciones (Salud, Justicia, Deportes, Sociedad...)
- [ ] Migración de artículos en secciones obsoletas (farandula→entretenimiento, social→sociedad)
- [ ] S6-03 geo: Prompt de agente con contexto geográfico cuando workspace ≠ Global
- [ ] S6-04 geo: Workspace multi-usuario (guardar en DB, no solo localStorage)
- [ ] S6-06 geo: NewsSource.Country field + migración → filtrado de fuentes por workspace
