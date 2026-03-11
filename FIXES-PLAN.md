# AgenteNews — Plan de Correcciones y Mejoras (Sprint 13+)

## Diagnostico de Bugs

### CASO 1: Logo pierde link + navegacion story→event rota
**Causa**: El logo en `Index.razor:22-26` es un `<div>` sin enlace `<a href="/">`. Al navegar desde StoryView a `/?eventId=X`, Blazor hace navegacion interna que NO re-ejecuta `OnAfterRenderAsync(firstRender: true)`, por lo que `AutoOpenEventId` no se procesa.
**Impacto**: Navegacion rota entre vistas, links muertos.
**Fix**: Envolver logo en `<a href="/">`. Manejar `AutoOpenEventId` en `OnParametersSetAsync` para cubrir navegaciones Blazor internas.

### CASO 2: Boton cerrar modal no funciona en vista tarjetas
**Causa**: Cuando el circuito SignalR se reconecta (visible en console: WebSocket connected), los event handlers de Blazor se pierden momentaneamente. El circuito reconecta = nueva conexion = handlers previos muertos.
**Impacto**: UI bloqueada, modal inamovible.
**Fix**: Agregar cierre via JS fallback (tecla Escape + click overlay) ademas del handler Blazor. Asegurar que `CloseEvent` fuerza `StateHasChanged`.

### CASO 3: manifest.json 404
**Causa**: `_Layout.cshtml:15` referencia `manifest.json` pero no existe en `wwwroot/`.
**Impacto**: Error 404 en cada carga de pagina, posibles problemas PWA.
**Fix**: Crear `wwwroot/manifest.json` basico.

### CASO 4: OpenEventById error (universo + mapa)
**Causa**: El metodo `[JSInvokable] OpenEventById(int id)` busca en `Events` lista local que puede estar filtrada por seccion/geo. Si el evento no esta en la lista filtrada, `ev == null` y no hace nada. PERO el error real es que `OpenEvent()` usa `Ctx` (DbContext scoped) que puede estar dispuesto tras reconexion de circuito.
**Impacto**: "Ver cronologia" falla en vista planetas y mapa.
**Fix**: En `OpenEventById`, si no se encuentra en `Events`, buscarlo en DB directamente. Manejar excepciones de DbContext dispuesto.

### CASO 5: Hilos narrativos no caben en barra
**Causa**: Los hilos narrativos se muestran en la barra lateral existente que no tiene espacio suficiente.
**Impacto**: UI desbordada, contenido cortado.
**Fix**: Crear vista "Hilos" como nuevo modo de vista (como planetas/mapa) con tarjetas grandes tipo timeline.

### CASO 6: GodMode no centrado
**Causa**: `.god-mode` tiene `max-width: 1600px` pero no `margin: 0 auto`.
**Impacto**: Layout desalineado en pantallas anchas.
**Fix**: Agregar `margin: 0 auto` a `.god-mode` y `.admin-page`.

### CASO 7: Tarjeta Costes IA demasiado grande
**Causa**: El panel de costes en dashboard ocupa todo el ancho de la columna sin limitar altura.
**Impacto**: Desproporcion visual.
**Fix**: Reducir tamano, limitar filas visibles, colapsar detalle.

### CASO 8: GodMode Secciones crash (circuit terminated)
**Causa**: `GmSections.razor:137` usa `s.Keywords.Count` en proyeccion LINQ. `Keywords` es `List<string>` JSONB — EF Core/Npgsql NO puede traducir `.Count` de una columna JSONB a SQL.
**Impacto**: Circuit crash, pagina muerta.
**Fix**: Cargar secciones sin `.Keywords.Count` en la proyeccion SQL, calcularlo despues de materializar.

### CASO 9: GodMode Usuarios fuera de estilo
**Causa**: Los form controls (`form-control`, `btn`, `btn-sm`) dependen de Bootstrap o estilos custom que no estan correctamente definidos en el contexto admin.
**Impacto**: Controles visualmente rotos.
**Fix**: Asegurar que `.form-control`, `.btn`, `.btn-primary`, `.btn-secondary` esten correctamente estilizados en app.css para el contexto admin.

### CASO 10: Editar proveedor IA fuera de estilo
**Causa**: Mismo problema que caso 9 — modales admin no tienen estilos consistentes.
**Impacto**: UI rota en edicion.
**Fix**: Estandarizar estilos de modales admin.

### CASO 11: Costes sin paginacion
**Causa**: `GmCosts.razor:110` hace `_entries.Take(100)` sin paginacion real.
**Impacto**: Tabla enorme, rendimiento pobre.
**Fix**: Implementar paginacion real con controles.

### CASO 12: Auditoria vacia
**Causa**: Nadie escribe en la tabla `AuditLogs`. No hay SaveChanges interceptor ni middleware de auditoria.
**Impacto**: Funcionalidad muerta.
**Fix**: Implementar `SaveChanges` override en AppDbContext que registre cambios automaticamente.

### CASO 13: Activity tracking + admin de planes/cuotas
**Causa**: Las entidades UserActivity, SectionQuota y ReaderProfile ya existen en BD. Falta: (1) panel admin para gestionar planes/cuotas, (2) dashboard de actividad de usuarios, (3) visualizacion de perfiles de lectura.
**Impacto**: No hay forma de administrar el sistema de monetizacion.
**Fix**: Sprint dedicado — ver seccion Sprint 14 abajo.

---

## Plan de Ejecucion

### Sprint 13A — Bugs Criticos (AHORA)
1. [x] Crear manifest.json (Caso 3)
2. [x] Fix logo link en Index.razor (Caso 1 parcial)
3. [x] Fix GmSections crash — Keywords.Count JSONB (Caso 8)
4. [x] Fix OpenEventById — buscar en DB si no esta en lista (Caso 4)
5. [x] Fix eventId navigation — manejar en OnParametersSetAsync (Caso 1 completo)
6. [x] Fix modal close — agregar JS fallback Escape + click (Caso 2)
7. [x] Fix GodMode centering (Caso 6)
8. [x] Fix form-control estilos admin (Casos 9, 10)
9. [x] Fix costes panel tamano (Caso 7)
10. [x] Fix costes paginacion (Caso 11)

### Sprint 13B — Funcionalidades Pendientes
11. [x] Implementar auditoria automatica via SaveChanges override (Caso 12)
12. [x] Vista "Hilos Narrativos" como modo de vista completo (Caso 5)

### Sprint 14 — Admin de Planes y Actividad (Caso 13)
- Panel GmActivity: Dashboard de actividad de usuarios
- Panel GmPlans: CRUD de planes de suscripcion
- Panel GmQuotas: Gestion de cuotas por seccion
- Visualizacion de perfiles de lectura (ReaderProfile)

---

## 20 Mejoras Propuestas

### UI/UX
1. **Modo oscuro/claro persistente en admin** — actualmente solo funciona en public
2. **Breadcrumbs en GodMode** — navegacion contextual entre secciones
3. **Notificaciones toast en admin** — feedback visual en acciones CRUD
4. **Drag & drop para ordenar secciones** — SortOrder visual
5. **Graficas interactivas en dashboard** — Chart.js con tooltips y drill-down
6. **Skeleton loaders** — placeholders animados durante cargas
7. **Modo responsive mobile en GodMode** — sidebar colapsable, tablas scrollables
8. **Preview de articulos inline** — expandir articulo sin cambiar pagina
9. **Filtro avanzado en noticias** — por fuente, fecha, relevancia, tipo
10. **Barra de progreso de agentes** — barra visual de items procesados vs total

### Funcionales
11. **Dashboard de actividad de usuarios** — graficas de uso por hora/dia/seccion
12. **Sistema de alertas por email** — eventos criticos notificados por correo
13. **Exportacion de datos** — CSV/JSON de eventos, articulos, costes
14. **Comparador de fuentes** — vista lado a lado de como diferentes fuentes cubren un evento
15. **Timeline interactiva de hilos** — visualizacion temporal de eventos relacionados
16. **Bookmarks y favoritos** — usuarios marcan eventos para seguimiento
17. **Busqueda full-text con ranking** — PostgreSQL tsvector para busqueda avanzada
18. **API rate limiting por API key** — planes diferenciados para API publica
19. **Webhooks para integraciones** — notificaciones push a sistemas externos
20. **A/B testing de briefings** — comparar diferentes formatos de briefing

---

## Progreso

| # | Caso | Estado | Fecha |
|---|------|--------|-------|
| 1 | Logo + navigation | HECHO | 2026-03-11 |
| 2 | Modal close | HECHO | 2026-03-11 |
| 3 | manifest.json | HECHO | 2026-03-11 |
| 4 | OpenEventById | HECHO | 2026-03-11 |
| 5 | Hilos narrativos | HECHO | 2026-03-11 |
| 6 | GodMode centering | HECHO | 2026-03-11 |
| 7 | Costes tamano | HECHO | 2026-03-11 |
| 8 | Secciones crash | HECHO | 2026-03-11 |
| 9 | Users estilos | HECHO | 2026-03-11 |
| 10 | AI provider estilos | HECHO | 2026-03-11 |
| 11 | Costes paginacion | HECHO | 2026-03-11 |
| 12 | Auditoria vacia | HECHO | 2026-03-11 |
| 13 | Activity admin | HECHO | 2026-03-11 |
