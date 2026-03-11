# Sprint 14 — Admin de Actividad, Planes y Cuotas

## Objetivo
Construir el modulo de administracion para gestionar:
1. Dashboard de actividad de usuarios (que leen, cuando, cuanto)
2. Planes de suscripcion (Free, Basic, Pro, Unlimited)
3. Cuotas por seccion (limites mensuales de lectura)
4. Visualizacion de perfiles de lectura generados por ReaderProfileAgent

## Entidades existentes (ya en BD)

### UserActivity (tabla `UserActivities`)
```
Id (long), UserId, ActivityType (enum), NewsSectionId?, NewsEventId?,
NewsArticleId?, StoryThreadId?, Metadata?, CreatedAt
```
ActivityType: EventOpened, ArticleRead, ArticleExpanded, SectionFiltered,
SearchPerformed, StoryThreadViewed, BookmarkCreated, BriefingViewed,
ExternalLinkClicked, MorningBriefViewed

### SectionQuota (tabla `SectionQuotas`)
```
Id, UserId, NewsSectionId, MaxReadsPerMonth (-1=ilimitado),
CurrentReads, PeriodStart, PlanTier (enum), CreatedAt, UpdatedAt
```
PlanTier: Free, Basic, Pro, Unlimited

### ReaderProfile (tabla `ReaderProfiles`)
```
Id, UserId, SemanticProfile?, TopInterests (jsonb string[]),
AvoidTopics (jsonb string[]), PreferredDepth?,
ArticlesRead, EventsOpened, LastAnalyzedAt?, LastActivityAt?
```

## Tareas

### Fase 1: Dashboard de Actividad (GmActivity component)
- [ ] Crear `GmActivity.razor` en Components/Admin/
- [ ] KPIs: usuarios activos hoy, lecturas totales, secciones mas leidas
- [ ] Grafica de actividad por hora (ultimas 24h) con Chart.js
- [ ] Grafica de actividad por dia (ultimos 30 dias)
- [ ] Top 10 usuarios mas activos
- [ ] Top 10 eventos mas vistos
- [ ] Desglose por tipo de actividad (pie chart)
- [ ] Agregar tab "actividad" en GodMode sidebar

### Fase 2: Gestion de Planes (GmPlans component)
- [ ] Crear entidad `SubscriptionPlan` con campos:
  - Name, Description, PlanTier, MonthlyPrice
  - Limites por seccion (JSONB dict: sectionSlug → maxReads)
  - Features (JSONB string[])
  - IsActive, SortOrder
- [ ] Crear migracion
- [ ] CRUD de planes en GodMode
- [ ] Asignar plan a usuarios (campo PlanTier en ApplicationUser o tabla)
- [ ] Agregar tab "planes" en GodMode sidebar

### Fase 3: Gestion de Cuotas
- [ ] Vista de cuotas actuales por usuario en GmUsers
- [ ] Boton "Gestionar cuotas" por usuario
- [ ] Auto-creacion de cuotas al asignar plan
- [ ] Reset mensual automatico (ya implementado en endpoint /api/activity/track)
- [ ] Dashboard de consumo: usuarios cerca del limite, usuarios bloqueados

### Fase 4: Perfiles de Lectura
- [ ] Vista de ReaderProfile en detalle de usuario
- [ ] Graficas de intereses por seccion
- [ ] SemanticProfile como texto descriptivo
- [ ] Historial de cambios de perfil
- [ ] Comparativa entre usuarios (admin analytics)

### Fase 5: UX de Limites (Lado usuario)
- [ ] Mostrar barra de consumo mensual en perfil
- [ ] Aviso cuando se acerca al limite (80%)
- [ ] Modal de "limite alcanzado" con opcion de upgrade
- [ ] Pagina de planes para upgrade

## Dependencias
- Chart.js (ya incluido)
- SignalR AdminHub (ya existe)
- ReaderProfileAgent (ya implementado, corre cada 6h)
- UserActivity tracking (ya implementado en Index.razor, ArticlePage, StoryView)

## Notas tecnicas
- Las cuotas se verifican en POST /api/activity/track (ya implementado)
- El reset mensual se hace comparando PeriodStart con inicio de mes actual
- MaxReadsPerMonth = -1 significa ilimitado (plan actual Free)
- Para monetizacion: integrar Stripe/PayPal en Fase 5+
