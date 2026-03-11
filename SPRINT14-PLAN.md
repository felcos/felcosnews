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

### SubscriptionPlan (tabla `SubscriptionPlans`) — NEW
```
Id, Name, Description, Tier (PlanTier), MonthlyPrice (decimal),
SectionLimits (jsonb dict), DefaultMaxReadsPerMonth, Features (jsonb string[]),
IsActive, SortOrder, CreatedAt, UpdatedAt
```

## Tareas

### Fase 1: Dashboard de Actividad (GmActivity component) ✅
- [x] Crear `GmActivity.razor` en Components/Admin/
- [x] KPIs: usuarios activos, lecturas totales, secciones mas leidas
- [x] Top usuarios mas activos
- [x] Desglose por tipo de actividad
- [x] Perfiles de lectura overview
- [x] Cuotas overview
- [x] Agregar tab "actividad" en GodMode

### Fase 2: Gestion de Planes (GmPlans component) ✅
- [x] Crear entidad `SubscriptionPlan`
- [x] Crear migracion AddSubscriptionPlans
- [x] CRUD de planes en GodMode
- [x] Asignar plan a usuarios (PlanTier + SubscriptionPlanId en ApplicationUser)
- [x] Agregar tab "planes" en GodMode

### Fase 3: Gestion de Cuotas — parcial
- [x] Assign plan to user desde GmPlans
- [ ] Auto-creacion de cuotas al asignar plan
- [ ] Dashboard de consumo avanzado: usuarios cerca del limite
- [x] Reset mensual automatico (ya implementado en /api/activity/track)

### Fase 4: Perfiles de Lectura ✅
- [x] Vista de ReaderProfile en GmActivity
- [x] TopInterests y SemanticProfile como texto descriptivo
- [x] Overview admin de perfiles

### Fase 5: UX de Limites (Lado usuario) ✅
- [x] Mostrar barra de consumo mensual en perfil de usuario (Profile.razor)
- [x] Aviso visual cuando se acerca al limite (80% amarillo, 100% rojo)
- [x] Mostrar perfil de lectura en pagina de perfil
- [x] Plan tier badge con estilos por nivel
- [ ] Modal de "limite alcanzado" con opcion de upgrade (futuro)
- [ ] Pagina de planes para upgrade (futuro)

## Dependencias
- Chart.js (ya incluido)
- SignalR AdminHub (ya existe)
- ReaderProfileAgent (ya implementado, corre cada 6h)
- UserActivity tracking (ya implementado en Index.razor, ArticlePage, StoryView)

## Notas tecnicas
- Las cuotas se verifican en POST /api/activity/track (ya implementado)
- El reset mensual se hace comparando PeriodStart con inicio de mes actual
- MaxReadsPerMonth = -1 significa ilimitado (plan actual Free)
- Para monetizacion: integrar Stripe/PayPal en fase futura
- Cache busting: v15 en _Layout.cshtml
