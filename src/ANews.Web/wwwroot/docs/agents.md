# AgenteNews — Agentes de IA

## Arquitectura

Todos los agentes heredan de `BaseAgent` (BackgroundService). Cada agente:
- Se registra como **Singleton + HostedService** en `DependencyInjection.cs`
- Tiene un intervalo configurable via `AgentConfig` en BD
- Soporta `TriggerNow()` para ejecucion manual (SemaphoreSlim)
- Registra ejecuciones en `AgentExecutions` y logs en `AgentLogs`
- Obtiene proveedor IA via `AiProviderFactory.GetDefaultProviderAsync()`

---

## 1. NewsScannerAgent
**Tipo**: `AgentType.NewsScanner`
**Intervalo**: 15 minutos
**Funcion**: Escanea fuentes RSS y extrae articulos nuevos.
**Skills**:
- Parseo de feeds RSS/Atom
- Deteccion de duplicados via ContentHash (SHA256)
- Extraccion de metadatos (titulo, fecha, autor, imagen)
- Manejo de errores por fuente con conteo de fallos consecutivos
- Auto-desactivacion de fuentes con >10 fallos consecutivos

## 2. EventDetectorAgent
**Tipo**: `AgentType.EventDetector`
**Intervalo**: 30 minutos
**Funcion**: Agrupa articulos sin clasificar en eventos noticiosos usando IA.
**Skills**:
- Analisis semantico de titulos y descripciones
- Clustering de articulos por similaridad tematica
- Asignacion de prioridad (Critical/High/Medium/Low)
- Calculo de ImpactScore (0-100)
- Deteccion de tendencias (Rising/Stable/Declining)
- Merge de eventos duplicados
- Ventana temporal: articulos de ultimas 72h

## 3. AlertGeneratorAgent
**Tipo**: `AgentType.AlertGenerator`
**Intervalo**: 10 minutos
**Funcion**: Genera alertas para eventos criticos/importantes.
**Skills**:
- Evaluacion de criterios de alerta (prioridad + impacto)
- Deduplicacion de alertas por evento
- Severidad calculada automaticamente
- Integracion con SignalR para notificaciones push

## 4. NotificationDispatcherAgent
**Tipo**: `AgentType.NotificationDispatcher`
**Intervalo**: 5 minutos
**Funcion**: Envia notificaciones pendientes a usuarios suscritos.
**Skills**:
- Routing por canal (email, push, webhook)
- Rate limiting por usuario
- Retry con backoff exponencial
- Respeto de preferencias de usuario

## 5. ArticleSummarizerAgent
**Tipo**: `AgentType.ArticleSummarizer`
**Intervalo**: 20 minutos
**Funcion**: Resume articulos sin resumen usando IA.
**Skills**:
- Generacion de resumenes concisos (2-3 frases)
- Deteccion de idioma (ES/EN)
- Analisis de sentimiento
- Puntuacion de credibilidad
- Extraccion de keywords

## 6. DigestSenderAgent
**Tipo**: `AgentType.DigestSender`
**Intervalo**: 1 hora
**Funcion**: Envia digests periodicos a usuarios suscritos.
**Skills**:
- Digests diarios/semanales segun preferencia
- Seleccion inteligente de contenido relevante
- Formato HTML para email
- Tracking de ultimo envio por usuario

## 7. SourceDiscoveryAgent
**Tipo**: `AgentType.SourceDiscovery`
**Intervalo**: 24 horas
**Funcion**: Descubre nuevas fuentes RSS usando IA.
**Skills**:
- Busqueda de fuentes por seccion tematica
- Validacion de URLs RSS
- Scoring de relevancia
- Deteccion de idioma
- Sugerencia de fuentes para secciones vacias

## 8. ThreadWeaverAgent
**Tipo**: `AgentType.ThreadWeaver`
**Intervalo**: 2 horas
**Funcion**: Teje hilos narrativos enlazando eventos relacionados.
**Skills**:
- Deteccion de relaciones temporales entre eventos
- Agrupacion por actores clave (personas, organizaciones, paises)
- Analisis de progresion narrativa
- Generacion de resumen de hilo
- Deteccion de escalada/resolucion
- Campos: WhyItMatters, WhatToWatch, KeyActors, Tags

## 9. BriefingGeneratorAgent
**Tipo**: `AgentType.BriefingGenerator`
**Intervalo**: 4 horas
**Funcion**: Genera briefings contextuales y morning briefs.
**Skills**:
- Briefings por evento (Background, KeyActors, WhatToWatch)
- Morning Brief diario (TopStories, DeepDive, Developing, Surprise)
- Analisis multi-fuente
- Formato editorial profesional
- Actualizacion de briefings existentes

## 10. SourceAnalyzerAgent
**Tipo**: `AgentType.SourceAnalyzer`
**Intervalo**: 6 horas
**Funcion**: Analiza calidad y sesgo de fuentes periodisticas.
**Skills**:
- Calculo de CredibilityScore
- Deteccion de sesgo (Left/CenterLeft/Center/CenterRight/Right)
- Densidad factual (FactDensityAvg)
- Historial de fiabilidad (scans exitosos vs fallidos)
- Recomendaciones de confianza

## 11. TelegramEditorialAgent
**Tipo**: `AgentType.TelegramEditorial`
**Intervalo**: 30 minutos
**Funcion**: Publica en canal de Telegram editorial.
**Skills**:
- Formato HTML para Telegram (ParseMode.Html)
- Publicacion de Morning Brief con emojis
- Breaking news (Priority >= Critical, ImpactScore >= 70)
- Deduplicacion via AgentLog
- Configuracion: Telegram:BotToken + Telegram:EditorialChannelId

## 12. ReaderProfileAgent
**Tipo**: `AgentType.ReaderProfileAnalyzer`
**Intervalo**: 6 horas
**Funcion**: Analiza comportamiento de lectura de usuarios.
**Skills**:
- Tracking de actividad: clics, lecturas, filtros, busquedas
- Calculo de TopInterests (top 5 secciones)
- Generacion de SemanticProfile via IA
- Deteccion de AvoidTopics
- PreferredDepth (quick/standard/deep)
- Estadisticas: ArticlesRead, EventsOpened
- Base para personalizacion y recomendaciones

---

## Configuracion

Cada agente puede configurarse desde GodMode:
- **Intervalo**: Tiempo entre ejecuciones
- **Habilitado**: On/Off
- **Hot-patch**: Cambiar intervalo sin reiniciar via AgentConfig

## Monitoreo

- KPIs en dashboard: ejecuciones, errores, costes
- Log en tiempo real via SignalR (AdminHub)
- Historial de ejecuciones con duracion y resultado
- Costes de IA por agente y proveedor
