# AgenteNews — Manual de Usuario

## ¿Qué es AgenteNews?

AgenteNews es un sistema de inteligencia de noticias que:
1. **Escanea** fuentes RSS de seguridad/geopolítica cada hora
2. **Clasifica** los artículos en eventos usando IA (Groq/Claude)
3. **Alerta** cuando hay eventos críticos
4. **Notifica** a tus canales (Telegram, Email, Discord) con lo que te importa

---

## Acceso

| URL | Descripción |
|---|---|
| `https://news.websoftware.es` | Portada pública (sin login) |
| `https://news.websoftware.es/login` | Iniciar sesión |
| `https://news.websoftware.es/register` | Crear cuenta |
| `https://news.websoftware.es/user` | Panel de usuario |
| `https://news.websoftware.es/admin` | Panel de administración (solo admins) |

**Cuenta admin:** `admin@anews.local` / `Admin@123456!`

---

## Portada Pública (`/`)

La portada muestra todos los eventos activos clasificados por IA.

### Vistas disponibles
- **Grid** (cuadrícula): tarjetas de eventos con prioridad, sección e impacto
- **Lista**: vista compacta con más eventos en pantalla
- **Universo**: visualización canvas interactiva (cada evento es una "estrella")

### Navegación por secciones
Usa el menú superior para filtrar por sección:
- Todo | NBQ | Geopolítica | Conflictos | Ciberguerra | Inteligencia | Terrorismo | Tecnología Dual | Economía

### Bandas de alertas críticas
Si hay eventos `Critical`, aparece una banda roja en la parte superior con un ticker de alertas.

### Ver detalles de un evento
Haz clic en cualquier evento para abrir el modal de detalles:
- Descripción completa
- Etiquetas temáticas
- Lista de artículos (haz clic en el título para ver el artículo con resumen IA)
- Enlace al artículo fuente original (icono de enlace externo)

### Página de artículo (`/article/{id}`)
- Resumen generado por IA (o botón "Generar resumen con IA")
- Enlace a la fuente original
- Artículos relacionados del mismo evento

---

## Panel de Usuario (`/user`)

Para acceder necesitas cuenta registrada.

### Dashboard (`/user`)
- Estadísticas: módulos activos, canales, eventos coincidentes, guardados
- Últimos eventos que coinciden con tus módulos (filtrado por keywords)
- Estado de tus canales de notificación

### Módulos (`/user/modules`)

Los módulos son filtros de noticias personalizados.

**Crear un módulo:**
1. Clic en "Nuevo módulo"
2. Introduce nombre (ej: "Ciberataques Rusia")
3. Añade keywords, una por línea. Puedes añadir peso: `keyword:2.0`
4. Selecciona frecuencia de notificación (Instantánea/Diaria/Semanal/Mensual)
5. Activa "Notificaciones" si quieres alertas por tus canales
6. Activa "Feed RSS" para obtener una URL RSS que puedes añadir a cualquier lector

**Ejemplos de keywords:**
```
russia
cyber attack:2.0
NATO
ukraine:1.5
malware
```

**Feed RSS:** Si activas la opción RSS, se genera una URL tipo `/api/rss/tu-token` que puedes añadir a Feedly, RSS.app, etc.

### Notificaciones (`/user/notifications`)

Configura por dónde recibir las alertas de tus módulos.

**Canales disponibles:**
- **Telegram**: Introduce tu Chat ID (obtenerlo con `@userinfobot` en Telegram)
- **Email**: Tu dirección de correo
- **Discord**: Webhook URL de tu servidor Discord
- **Webhook**: Cualquier URL que acepte POST JSON

**Para activar un canal:**
1. Clic en "Añadir canal"
2. Selecciona tipo y rellena los datos
3. Guarda
4. Haz clic en "Verificar" → recibirás un mensaje de prueba con un código
5. Haz clic en "Probar" para enviar un mensaje de prueba real

**Nota Telegram:** Para obtener tu Chat ID:
1. Abre Telegram
2. Busca `@userinfobot`
3. Envía `/start`
4. Te responde con tu ID numérico

### Guardados (`/user/bookmarks`)

Artículos que has marcado para leer más tarde. Puedes añadir notas a cada guardado.

### API Tokens (`/user/api-tokens`)

Genera tokens para acceder a la API de AgenteNews desde herramientas externas.

**Scopes disponibles:**
- `read:events` — leer eventos
- `read:articles` — leer artículos
- `read:modules` — leer tus módulos
- `write:modules` — modificar tus módulos

**Importante:** El token completo solo se muestra UNA VEZ al crearlo. Guárdalo.

---

## Panel de Administración (`/admin`)

Solo accesible con rol Admin o SuperAdmin.

### God Mode (`/admin`)

Vista general del sistema:
- Estado de los 4 agentes (corriendo/parado/error)
- Últimas ejecuciones de cada agente
- Salud del sistema (base de datos, Redis, fuentes, IA)
- Botones "Ejecutar ahora" para forzar el ciclo de cualquier agente

**Qué hace cada agente:**
| Agente | Intervalo | Función |
|---|---|---|
| NewsScannerAgent | Cada hora | Descarga artículos de todas las fuentes RSS activas |
| EventDetectorAgent | Cada 2h | Clasifica artículos sin clasificar en eventos usando IA |
| AlertGeneratorAgent | Cada 15min | Crea alertas para eventos Critical/High nuevos |
| NotificationDispatcherAgent | Cada 5min | Envía notificaciones a los canales de usuarios |
| ArticleSummarizerAgent | Cada 45min | Genera resúmenes IA y extrae keywords de artículos nuevos |
| DigestSenderAgent | Cada hora | Envía emails de digest periódico a usuarios que lo tienen activado |

### Agentes (`/admin/agents`)

Monitor de ejecuciones de agentes en tiempo real:
- Feed de logs en vivo (actualización por WebSocket)
- Historial de ejecuciones con duración, artículos procesados, coste IA
- Botones para forzar ejecución manual

### Costes IA (`/admin/costs`)

Dashboard de gasto en API de IA:
- Coste total, hoy, tokens entrada/salida
- Desglose por proveedor y por agente
- Gráfica de tendencia diaria

### Noticias (`/admin/news`)

CRUD de eventos de noticias:
- Ver todos los eventos con filtros
- Editar título, descripción, prioridad, sección, tags
- Crear eventos manualmente
- Activar/desactivar eventos

### Secciones (`/admin/sections`)

Gestión de las categorías temáticas:
- Añadir/editar/borrar secciones
- Configurar color, icono, descripción
- Gestionar si es pública o de sistema

### Fuentes RSS (`/admin/sources`)

Gestión de las fuentes de noticias:
- Añadir nuevas fuentes RSS
- Ver estado (última vez escaneada, errores, artículos procesados)
- Activar/desactivar fuentes
- Ver errores de las fuentes que fallan

### Usuarios (`/admin/users`)

Gestión de cuentas de usuario:
- Ver todos los usuarios con módulos y canales
- Cambiar rol (User / Admin / SuperAdmin)
- Resetear contraseña
- Activar/desactivar cuentas

### Proveedores IA (`/admin/ai`)

Configuración de los proveedores de inteligencia artificial:
- Añadir proveedor (Claude, OpenAI, Groq, Gemini, Kimi)
- Probar conexión con botón "Test"
- Configurar presupuesto mensual máximo
- Establecer proveedor por defecto

**Para Groq (gratuito):**
- API Key: desde `console.groq.com`
- Base URL: `https://api.groq.com/openai/v1`
- Modelo: `llama-3.3-70b-versatile`

### Auditoría (`/admin/audit`)

Log de todas las acciones en el sistema:
- Filtrar por tipo de acción (Create/Update/Delete/Login)
- Ver cambios antes/después en formato JSON
- Filtrar por fecha

---

## Flujo de trabajo típico

### Primera vez (admin)

1. Ve a `/admin/ai` → Configura y activa un proveedor IA
2. Ve a `/admin/sources` → Activa las fuentes RSS que quieras
3. Ve a `/admin` → Pulsa "Ejecutar ahora" en **NewsScannerAgent** (espera ~2 min)
4. Pulsa "Ejecutar ahora" en **EventDetectorAgent** (espera ~1 min, usa IA)
5. Vuelve a la portada `/` → Ya verás eventos clasificados

### Uso diario (usuario)

Los agentes trabajan solos en segundo plano. Solo necesitas:
1. Visitar la portada para ver las últimas noticias
2. Revisar tu Dashboard `/user` para ver lo que coincide con tus módulos
3. Si configuraste notificaciones → recibes alertas automáticamente

### Añadir una nueva fuente RSS

1. `/admin/sources` → "Nueva fuente"
2. Nombre, URL del feed RSS, sección temática, puntuación de credibilidad
3. Guardar
4. El scanner la incluirá en su próximo ciclo (o "Ejecutar ahora")

---

## Solución de problemas

### "No veo noticias en la portada"
- Los agentes necesitan tiempo para el primer ciclo
- Ve a `/admin` y ejecuta manualmente **NewsScanner** y luego **EventDetector**
- Comprueba en `/admin/ai` que hay un proveedor activo y por defecto

### "El EventDetector no clasifica los artículos"
- Ve a `/admin/ai` → pulsa "Test" en tu proveedor → debe decir "Conexión exitosa"
- Si falla, revisa la API Key y la Base URL
- Para Groq la Base URL debe ser exactamente: `https://api.groq.com/openai/v1`

### "No recibo notificaciones de Telegram"
- Ve a `/user/notifications` → el canal debe estar **activo** y **verificado**
- Pulsa "Probar" para verificar que funciona manualmente
- El bot necesita que le hayas enviado un mensaje antes (busca el bot, envía /start)
- Nota: el token del bot de Telegram debe configurarse en el servidor

### "La navegación del panel no funciona"
- Problema conocido resuelto en versión actual: era un bug de SignalR en prerendering
- Si persiste: recarga la página (F5) y vuelve a intentar

### "Veo el error 'Se ha producido un error. Recargar'"
- La conexión Blazor se ha perdido temporalmente
- Haz clic en "Recargar" o recarga la página con F5
- Es normal en conexiones lentas o tras varios minutos de inactividad

---

## API REST

La API está disponible para integraciones externas.

### Autenticación
Usa un API Token generado en `/user/api-tokens` como Bearer token:
```
Authorization: Bearer tu-api-token-aqui
```

### Endpoints principales

```
GET /api/rss/{token}          RSS feed de tu módulo (sin auth, el token es el secreto)
GET /health                   Estado del servidor
```

### Endpoints admin (requieren rol Admin)
```
POST /api/admin/agents/newsscanner/trigger             Forzar ciclo de escáner
POST /api/admin/agents/eventdetector/trigger           Forzar clasificación IA
POST /api/admin/agents/alertgenerator/trigger          Forzar generación de alertas
POST /api/admin/agents/notificationdispatcher/trigger  Forzar envío notificaciones
POST /api/admin/agents/articlesummarizer/trigger       Forzar resumen de artículos
POST /api/admin/agents/digestsender/trigger            Forzar envío de digests
```

### Endpoints usuario (requieren auth)
```
GET /api/user/module-keywords    Lista de keywords activas del usuario (para universe)
```

---

## Repositorio y Despliegue

### Repositorio
- **GitHub:** `https://github.com/felcos/felcosnews`
- **Branch principal:** `main`
- **CI/CD:** `.github/workflows/deploy.yml` — push a `main` → build → SCP → restart VM

### GitHub Personal Access Token (PAT)
Token sin expiración con scopes `repo + workflow`.
El token está guardado en la memoria de Claude Code (MEMORY.md del proyecto) — búscalo ahí para no exponerlo en el repo.

Para configurar el remote y hacer push:
```bash
# Sustituye <TOKEN> por el PAT guardado en MEMORY.md
git remote set-url origin https://felcos:<TOKEN>@github.com/felcos/felcosnews.git
git push origin main
```

### SSH Key para la VM
```bash
# Deploy manual:
ssh -i ssh-key-2026-01-16.key ubuntu@79.72.56.98
# SCP:
scp -i ssh-key-2026-01-16.key -r publish/* ubuntu@79.72.56.98:/opt/anews/
```

### Configurar CI/CD (GitHub Actions)
Añadir en **GitHub → Settings → Secrets → Actions**:
| Secret | Valor |
|---|---|
| `VM_SSH_KEY` | Contenido de `ssh-key-2026-01-16.key` |
| `VM_HOST` | `79.72.56.98` |
| `VM_USER` | `ubuntu` |

### Configurar canales de notificación en producción

Editar en la VM: `/opt/anews/appsettings.Production.json`

**Telegram:**
```json
"Telegram": {
  "BotToken": "XXXXXXXXX:YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY"
}
```
Obtener el token: busca `@BotFather` en Telegram → `/newbot` → copia el token.
Los usuarios ponen su Chat ID (obtenido con `@userinfobot`).

**Email (Gmail):**
```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "User": "tucuenta@gmail.com",
  "Password": "xxxx xxxx xxxx xxxx",
  "From": "AgenteNews <tucuenta@gmail.com>"
}
```
La contraseña debe ser una **App Password** de Google (no la contraseña normal).
Activar en: Cuenta Google → Seguridad → Verificación en 2 pasos → Contraseñas de aplicaciones.

**Discord:** No requiere config en servidor. El usuario introduce directamente la Webhook URL de su servidor Discord.

**WhatsApp (Twilio):**
```json
"Twilio": {
  "AccountSid": "ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "AuthToken": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
  "WhatsAppFrom": "whatsapp:+14155238886"
}
```
Requiere cuenta Twilio + sandbox o número verificado. Sin esto el servicio arranca igual (no crashea).

### Vista Universo — Módulos del usuario
Cuando un usuario logueado abre la vista Universo (`/` → botón Universo), los planetas que coinciden con las keywords de sus módulos activos aparecen con un **anillo verde** pulsante. El callout al hacer hover muestra la etiqueta "tu módulo".

---

## Guía rápida de primer despliegue

```bash
# 1. Desde tu máquina: subir código
git remote add origin https://github.com/felcos/felcosnews.git
git push -u origin main

# 2. En la VM (primera vez): aplicar nueva migración
cd /opt/anews
dotnet ef database update --project ... # o via deploy manual

# 3. La próxima vez: push a main activa CI/CD automáticamente
git push origin main  # → GitHub Actions construye y despliega solo
```
