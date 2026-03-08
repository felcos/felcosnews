# AgenteNews — Plan Maestro de Desarrollo

## Estado del Proyecto
- **Última revisión:** 2026-03-08
- **Fase actual:** Sprint 4 EN PROGRESO

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

## Sprint 4 — Agentes Avanzados y UX (EN PROGRESO)

| # | Descripción | Estado |
|---|---|---|
| S4-01 | Fix JSONB cardinality en ArticleSummarizerAgent (`!Any()`) | ✅ |
| S4-02 | DigestSenderAgent: email digest periódico por frecuencia de usuario | ✅ |
| S4-03 | Filtrar keywords placeholder ("ai"/"rss") en ArticlePage | ✅ |
| S4-04 | Dashboard: eventos recientes clickables → abre modal en portada | ✅ |
| S4-05 | Index.razor: auto-abrir evento desde query param `?eventId=` | ✅ |
| S4-06 | LastDigestSentAt en ApplicationUser + migración AddDigestTracking | ✅ |
| S4-07 | DigestSenderAgent registrado en GodMode, AgentsMonitor, trigger endpoint | ✅ |

---

## Contratos de Componentes (NO cambiar sin actualizar aquí)

### BaseAgent (Infrastructure/Agents/BaseAgent.cs)
```
public void TriggerNow()
  - Libera semáforo interno (_triggerNow)
  - El ciclo ExecuteAsync despierta inmediatamente
  - Si ya hay un ciclo en ejecución, encola uno más (máx 1)
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
Secciones: upsert por Slug (no duplicar)
Fuentes RSS: upsert por Url (no duplicar)
AI Config: crear solo si Count == 0 (placeholder inactivo)
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

# Subir a VM
scp -i ssh-key-2026-01-16.key -r publish/. ubuntu@79.72.56.98:/opt/anews/

# Reiniciar servicio
ssh -i ssh-key-2026-01-16.key ubuntu@79.72.56.98 "sudo systemctl restart anews"

# Ver logs
ssh -i ssh-key-2026-01-16.key ubuntu@79.72.56.98 "sudo journalctl -u anews -f"
```

**DB Producción:** `anews_prod`, usuario `anews`, contraseña en `/opt/anews/appsettings.Production.json`
**Admin login:** `admin@anews.local` / `Admin@123456!`
