# 🎨 FRONTEND Y EXPERIENCIA DE USUARIO

## 📋 **ÍNDICE**

1. [Arquitectura Frontend](#arquitectura-frontend)
2. [Universo de Noticias - Diseño Visual](#universo-visual)
3. [Componentes Blazor](#componentes-blazor)
4. [JavaScript y Animaciones](#javascript-animaciones)
5. [CSS y Styling](#css-styling)
6. [SignalR Real-Time](#signalr-real-time)
7. [Responsive Design](#responsive-design)
8. [Accesibilidad](#accesibilidad)

---

## 🌌 **UNIVERSO DE NOTICIAS - DISEÑO VISUAL**

### **Concepto Visual Principal**

El frontend implementa un **"Universo de Noticias"** donde cada evento principal es una estrella con artículos orbitando como planetas y lunas.

```
🌟 EVENTO CRÍTICO (Estrella roja, pulsante)
├── 🪐 Noticia Principal (Planeta grande, azul)
│   ├── 🌙 Análisis (Luna pequeña, gris)
│   └── 🌙 Reacción (Luna pequeña, amarilla)
└── 🪐 Desarrollo (Planeta mediano, verde)
    └── 🌙 Seguimiento (Luna pequeña, blanca)
```

### **Paleta de Colores del Sistema**

```scss
// Colores principales
$critical-red: #ff0000;      // Eventos críticos
$high-orange: #ff6600;       // Eventos importantes  
$medium-yellow: #ffcc00;     // Eventos moderados
$low-blue: #4a90e2;          // Eventos menores

// Fondo universo
$space-black: #000511;       // Negro espacio
$space-blue: #001122;        // Azul profundo
$star-glow: #00ff88;         // Verde sistema

// NBQ específicos
$nuclear-red: #ff0000;       // Nuclear
$biological-green: #00ff00;  // Biológico  
$chemical-yellow: #ffff00;   // Químico
```

---

## 🏗️ **ARQUITECTURA FRONTEND**

### **Estructura de Componentes**

```
ANews.WebApp/
├── Pages/                   
│   ├── Index.razor          # Dashboard principal
│   ├── EventDetail.razor    # Detalle de evento
│   ├── SectionView.razor    # Vista por sección
│   └── Admin/
│       ├── Dashboard.razor  # Admin dashboard
│       └── Analytics.razor  # Analytics
├── Components/
│   ├── Universe/
│   │   ├── NewsUniverse.razor     # Componente principal
│   │   ├── StellarSystem.razor    # Sistema estelar
│   │   ├── StarComponent.razor    # Estrella individual  
│   │   ├── PlanetComponent.razor  # Planeta individual
│   │   └── MoonComponent.razor    # Luna individual
│   ├── Navigation/
│   │   ├── SectionTabs.razor      # Pestañas de sección
│   │   ├── SearchBox.razor        # Búsqueda
│   │   └── FilterPanel.razor      # Filtros
│   ├── Shared/
│   │   ├── AlertPanel.razor       # Panel de alertas
│   │   ├── LoadingSpinner.razor   # Spinner de carga
│   │   └── ErrorBoundary.razor    # Manejo errores
│   └── Modals/
│       ├── EventDetailModal.razor # Modal detalle evento
│       ├── ArticleModal.razor     # Modal artículo
│       └── CreateSectionModal.razor # Crear sección
├── Services/
│   ├── NewsService.cs             # Cliente API noticias
│   ├── SignalRService.cs          # Cliente SignalR
│   ├── StateService.cs            # Estado global
│   └── AnimationService.cs        # Control animaciones
└── wwwroot/
    ├── js/
    │   ├── universe.js            # Motor universo
    │   ├── animations.js          # Animaciones
    │   ├── signalr-client.js      # SignalR cliente
    │   └── audio-alerts.js        # Alertas sonoras
    ├── css/
    │   ├── app.scss              # Estilos principales
    │   ├── universe.scss         # Estilos universo
    │   ├── components.scss       # Componentes
    │   └── responsive.scss       # Responsive
    └── sounds/
        ├── critical-alert.wav    # Alerta crítica
        ├── new-event.wav        # Nuevo evento
        └── notification.wav     # Notificación
```

---

## 🌟 **COMPONENTES BLAZOR PRINCIPALES**

### **NewsUniverse.razor - Componente Principal**

```html
@page "/"
@using Microsoft.AspNetCore.SignalR.Client
@implements IAsyncDisposable
@inject IJSRuntime JS
@inject NewsService NewsService
@inject StateService StateService

<div class="news-universe @ThemeClass" id="universe-container">
    <!-- Header de la agencia -->
    <header class="agency-header">
        <div class="logo">
            <i class="fas fa-satellite"></i>
            <span>AGENTIC NEWS</span>
            <div class="beta-badge">BETA</div>
        </div>
        
        <div class="header-center">
            <div class="live-indicator @(IsConnected ? "connected" : "disconnected")">
                <div class="pulse-dot"></div>
                @(IsConnected ? "LIVE" : "OFFLINE") • Último update: @LastUpdate.ToString("HH:mm:ss")
            </div>
        </div>
        
        <div class="header-actions">
            <button class="icon-btn" @onclick="ToggleTheme">
                <i class="fas @(IsDarkTheme ? "fa-sun" : "fa-moon")"></i>
            </button>
            <button class="icon-btn" @onclick="ToggleSound">
                <i class="fas @(SoundEnabled ? "fa-volume-up" : "fa-volume-mute")"></i>
            </button>
            <button class="icon-btn" @onclick="ShowSettings">
                <i class="fas fa-cog"></i>
            </button>
        </div>
    </header>
    
    <!-- Navegación de secciones -->
    <SectionTabs Sections="@AvailableSections" 
                 SelectedSection="@SelectedSection"
                 OnSectionChanged="@OnSectionChanged"
                 OnCreateSection="@ShowCreateSectionModal" />
    
    <!-- Panel de alertas críticas -->
    @if (CriticalAlerts.Any())
    {
        <AlertPanel Alerts="@CriticalAlerts" OnDismiss="@DismissAlert" />
    }
    
    <!-- El universo principal -->
    <div class="universe-viewport" @ref="universeViewport">
        <div class="universe-controls">
            <div class="view-controls">
                <button class="control-btn @(ViewMode == "universe" ? "active" : "")" 
                        @onclick="() => SetViewMode('universe')">
                    <i class="fas fa-globe"></i> Universo
                </button>
                <button class="control-btn @(ViewMode == "list" ? "active" : "")" 
                        @onclick="() => SetViewMode('list')">
                    <i class="fas fa-list"></i> Lista
                </button>
                <button class="control-btn @(ViewMode == "grid" ? "active" : "")" 
                        @onclick="() => SetViewMode('grid')">
                    <i class="fas fa-th"></i> Grid
                </button>
            </div>
            
            <div class="filter-controls">
                <select @onchange="OnPriorityFilter" class="filter-select">
                    <option value="">Todas las prioridades</option>
                    <option value="Critical">Crítico</option>
                    <option value="High">Alto</option>
                    <option value="Medium">Medio</option>
                    <option value="Low">Bajo</option>
                </select>
                
                <input type="search" @bind="SearchTerm" @oninput="OnSearch" 
                       placeholder="Buscar eventos..." class="search-input" />
            </div>
        </div>
        
        <!-- Renderizado condicional según modo -->
        @if (ViewMode == "universe")
        {
            <UniverseView Events="@FilteredEvents" 
                         OnEventClick="@ShowEventDetail"
                         OnArticleClick="@ShowArticleDetail" />
        }
        else if (ViewMode == "list")
        {
            <ListView Events="@FilteredEvents" 
                     OnEventClick="@ShowEventDetail" />
        }
        else if (ViewMode == "grid")
        {
            <GridView Events="@FilteredEvents" 
                     OnEventClick="@ShowEventDetail" />
        }
        
        <!-- Loading overlay -->
        @if (IsLoading)
        {
            <div class="loading-overlay">
                <LoadingSpinner Message="@LoadingMessage" />
            </div>
        }
    </div>
    
    <!-- Estadísticas en tiempo real -->
    <footer class="universe-footer">
        <div class="stats">
            <div class="stat">
                <span class="stat-number">@TotalEvents</span>
                <span class="stat-label">Eventos Activos</span>
            </div>
            <div class="stat">
                <span class="stat-number">@TotalArticles</span>
                <span class="stat-label">Artículos</span>
            </div>
            <div class="stat">
                <span class="stat-number">@ActiveSources</span>
                <span class="stat-label">Fuentes</span>
            </div>
            <div class="stat">
                <span class="stat-number">@AnalysisLatency</span>
                <span class="stat-label">Latencia IA</span>
            </div>
        </div>
    </footer>
</div>

<!-- Modales -->
@if (ShowEventDetailModal && SelectedEvent != null)
{
    <EventDetailModal Event="@SelectedEvent" 
                     OnClose="@HideEventDetail"
                     OnArticleClick="@ShowArticleDetail" />
}

@if (ShowArticleDetailModal && SelectedArticle != null)
{
    <ArticleDetailModal Article="@SelectedArticle" 
                       OnClose="@HideArticleDetail" />
}

@if (ShowCreateSectionModalFlag)
{
    <CreateSectionModal OnSectionCreated="@OnSectionCreated" 
                       OnCancel="@HideCreateSectionModal" />
}

@code {
    // Estado del componente
    private List<NewsEventDto> AllEvents = new();
    private List<NewsEventDto> FilteredEvents = new();
    private List<NewsSectionDto> AvailableSections = new();
    private List<AlertDto> CriticalAlerts = new();
    
    // UI State
    private string ViewMode = "universe";
    private string SelectedSection = "all";
    private string SearchTerm = "";
    private bool IsLoading = true;
    private string LoadingMessage = "Cargando universo de noticias...";
    private DateTime LastUpdate = DateTime.Now;
    
    // Modal state
    private bool ShowEventDetailModal = false;
    private bool ShowArticleDetailModal = false;
    private bool ShowCreateSectionModalFlag = false;
    private NewsEventDto? SelectedEvent = null;
    private NewsArticleDto? SelectedArticle = null;
    
    // Settings
    private bool IsDarkTheme = true;
    private bool SoundEnabled = true;
    private bool AnimationsEnabled = true;
    private bool IsConnected = false;
    
    // Referencias DOM
    private ElementReference universeViewport;
    
    // SignalR
    private HubConnection? hubConnection;
    
    // Propiedades calculadas
    private string ThemeClass => IsDarkTheme ? "theme-dark" : "theme-light";
    private int TotalEvents => FilteredEvents.Count;
    private int TotalArticles => FilteredEvents.Sum(e => e.ArticlesCount);
    private int ActiveSources => FilteredEvents.SelectMany(e => e.MainArticles).Select(a => a.Source).Distinct().Count();
    private string AnalysisLatency => "< 1s"; // TODO: Calcular real
    
    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Cargar datos iniciales
            await LoadInitialData();
            
            // Configurar SignalR
            await SetupSignalR();
            
            // Configurar estado del usuario
            await LoadUserPreferences();
            
            IsLoading = false;
        }
        catch (Exception ex)
        {
            LoadingMessage = $"Error cargando datos: {ex.Message}";
        }
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Inicializar JavaScript del universo
            await JS.InvokeVoidAsync("window.universe.initialize", universeViewport);
            
            // Configurar event listeners
            await JS.InvokeVoidAsync("window.universe.setupEventListeners", 
                DotNetObjectReference.Create(this));
            
            // Primera actualización del universo
            await UpdateUniverseVisualization();
        }
    }
    
    private async Task LoadInitialData()
    {
        // Cargar eventos principales
        AllEvents = await NewsService.GetMajorEventsAsync();
        
        // Cargar secciones disponibles
        AvailableSections = await NewsService.GetSectionsAsync();
        
        // Cargar alertas críticas
        CriticalAlerts = await NewsService.GetCriticalAlertsAsync();
        
        // Aplicar filtros iniciales
        await ApplyFilters();
    }
    
    private async Task SetupSignalR()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl("/newshub")
            .WithAutomaticReconnect()
            .Build();
        
        // Eventos de conexión
        hubConnection.Reconnecting += (sender) =>
        {
            IsConnected = false;
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        };
        
        hubConnection.Reconnected += (sender) =>
        {
            IsConnected = true;
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        };
        
        hubConnection.Closed += (sender) =>
        {
            IsConnected = false;
            InvokeAsync(StateHasChanged);
            return Task.CompletedTask;
        };
        
        // Eventos de datos
        hubConnection.On<List<NewsEventDto>>("EventsUpdate", async (events) =>
        {
            AllEvents = events;
            LastUpdate = DateTime.Now;
            await ApplyFilters();
            await UpdateUniverseVisualization();
            
            if (SoundEnabled)
            {
                await JS.InvokeVoidAsync("window.audioAlerts.playNewEvent");
            }
            
            await InvokeAsync(StateHasChanged);
        });
        
        hubConnection.On<AlertDto>("CriticalAlert", async (alert) =>
        {
            CriticalAlerts.Add(alert);
            
            if (SoundEnabled)
            {
                await JS.InvokeVoidAsync("window.audioAlerts.playCriticalAlert");
            }
            
            await InvokeAsync(StateHasChanged);
        });
        
        hubConnection.On<NewsSectionDto>("SectionCreated", async (section) =>
        {
            AvailableSections.Add(section);
            await InvokeAsync(StateHasChanged);
        });
        
        await hubConnection.StartAsync();
        IsConnected = hubConnection.State == HubConnectionState.Connected;
    }
    
    private async Task ApplyFilters()
    {
        var filtered = AllEvents.AsEnumerable();
        
        // Filtrar por sección
        if (SelectedSection != "all")
        {
            filtered = filtered.Where(e => e.SectionSlug == SelectedSection);
        }
        
        // Filtrar por búsqueda
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            filtered = filtered.Where(e => 
                e.Title.ToLower().Contains(searchLower) ||
                e.Description.ToLower().Contains(searchLower) ||
                e.Tags.Any(tag => tag.ToLower().Contains(searchLower)));
        }
        
        FilteredEvents = filtered.OrderByDescending(e => e.ImpactScore).ToList();
        
        if (ViewMode == "universe")
        {
            await UpdateUniverseVisualization();
        }
    }
    
    private async Task UpdateUniverseVisualization()
    {
        if (ViewMode == "universe")
        {
            await JS.InvokeVoidAsync("window.universe.updateEvents", FilteredEvents);
        }
    }
    
    // Event handlers
    private async Task OnSectionChanged(string sectionSlug)
    {
        SelectedSection = sectionSlug;
        await ApplyFilters();
    }
    
    private async Task OnSearch(ChangeEventArgs e)
    {
        SearchTerm = e.Value?.ToString() ?? "";
        await ApplyFilters();
    }
    
    private async Task SetViewMode(string mode)
    {
        ViewMode = mode;
        StateService.SetViewMode(mode);
        
        if (mode == "universe")
        {
            await UpdateUniverseVisualization();
        }
    }
    
    private async Task ShowEventDetail(NewsEventDto eventDto)
    {
        SelectedEvent = eventDto;
        ShowEventDetailModal = true;
        
        // Cargar detalles completos del evento
        SelectedEvent = await NewsService.GetEventDetailAsync(eventDto.Id);
    }
    
    private void HideEventDetail()
    {
        ShowEventDetailModal = false;
        SelectedEvent = null;
    }
    
    private async Task ShowArticleDetail(NewsArticleDto article)
    {
        SelectedArticle = article;
        ShowArticleDetailModal = true;
        
        // Cargar contenido completo del artículo
        SelectedArticle = await NewsService.GetArticleDetailAsync(article.Id);
    }
    
    private void HideArticleDetail()
    {
        ShowArticleDetailModal = false;
        SelectedArticle = null;
    }
    
    private void ShowCreateSectionModal()
    {
        ShowCreateSectionModalFlag = true;
    }
    
    private void HideCreateSectionModal()
    {
        ShowCreateSectionModalFlag = false;
    }
    
    private async Task OnSectionCreated(NewsSectionDto section)
    {
        AvailableSections.Add(section);
        ShowCreateSectionModalFlag = false;
        
        // Enviar al hub para notificar otros usuarios
        if (hubConnection?.State == HubConnectionState.Connected)
        {
            await hubConnection.SendAsync("NotifySectionCreated", section);
        }
    }
    
    // Métodos JS interop
    [JSInvokable]
    public async Task OnEventClickFromJS(int eventId)
    {
        var eventDto = FilteredEvents.FirstOrDefault(e => e.Id == eventId);
        if (eventDto != null)
        {
            await ShowEventDetail(eventDto);
            await InvokeAsync(StateHasChanged);
        }
    }
    
    [JSInvokable]
    public async Task OnArticleClickFromJS(int articleId)
    {
        var article = FilteredEvents
            .SelectMany(e => e.MainArticles)
            .FirstOrDefault(a => a.Id == articleId);
        
        if (article != null)
        {
            await ShowArticleDetail(article);
            await InvokeAsync(StateHasChanged);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (hubConnection != null)
        {
            await hubConnection.DisposeAsync();
        }
    }
}
```

### **StellarSystem.razor - Sistema Estelar Individual**

```html
@using Microsoft.AspNetCore.Components
@inject IJSRuntime JS

<div class="stellar-system" 
     data-event-id="@Event.Id"
     style="--impact-size: @(Event.ImpactScore * 2)px; --system-priority: @GetPriorityLevel()">
     
    <!-- Estrella central -->
    <div class="central-star @GetPriorityClass() @GetPulseClass()" 
         @onclick="() => OnEventClick.InvokeAsync(Event)"
         data-tooltip="@Event.Title">
        
        <div class="star-core">
            <div class="star-glow"></div>
            <div class="star-content">
                <h4 class="star-title">@TruncateTitle(Event.Title, 40)</h4>
                <div class="star-metrics">
                    <span class="impact-score">@Event.ImpactScore.ToString("F0")</span>
                    <span class="articles-count">@Event.ArticlesCount art.</span>
                </div>
                <div class="star-category">@Event.Category</div>
            </div>
        </div>
        
        <!-- Anillos de energía para eventos críticos -->
        @if (Event.Priority == "Critical")
        {
            <div class="energy-ring ring-1"></div>
            <div class="energy-ring ring-2"></div>
        }
    </div>
    
    <!-- Planetas orbitando -->
    @foreach (var article in Event.MainArticles.Take(6)) // Máximo 6 planetas
    {
        <PlanetComponent Article="@article" 
                        ParentEvent="@Event"
                        OrbitRadius="@CalculateOrbitRadius(article)"
                        OrbitSpeed="@CalculateOrbitSpeed(article)"
                        OnClick="@OnArticleClick" />
    }
    
    <!-- Conectores a eventos relacionados -->
    @if (ShowConnections && Event.RelatedEvents?.Any() == true)
    {
        @foreach (var relatedEvent in Event.RelatedEvents.Take(3))
        {
            <div class="connection-line" 
                 data-from="@Event.Id" 
                 data-to="@relatedEvent.Id"
                 style="--connection-strength: @relatedEvent.ConnectionStrength">
            </div>
        }
    }
    
    <!-- Información flotante -->
    <div class="system-info" @onclick:stopPropagation="true">
        <div class="info-badge trend @Event.Trend.ToLower()">
            <i class="fas @GetTrendIcon()"></i>
            @GetTrendText()
        </div>
        
        @if (Event.Location != null)
        {
            <div class="info-badge location">
                <i class="fas fa-map-marker-alt"></i>
                @Event.Location.City
            </div>
        }
        
        @if (IsRecent)
        {
            <div class="info-badge new-badge">
                <i class="fas fa-star"></i>
                NUEVO
            </div>
        }
    </div>
</div>

@code {
    [Parameter] public NewsEventDto Event { get; set; } = new();
    [Parameter] public EventCallback<NewsEventDto> OnEventClick { get; set; }
    [Parameter] public EventCallback<NewsArticleDto> OnArticleClick { get; set; }
    [Parameter] public bool ShowConnections { get; set; } = true;
    [Parameter] public bool AnimationsEnabled { get; set; } = true;
    
    private bool IsRecent => Event.CreatedAt > DateTime.Now.AddHours(-2);
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && AnimationsEnabled)
        {
            await JS.InvokeVoidAsync("window.universe.animateSystemEntry", Event.Id);
        }
    }
    
    private string GetPriorityClass()
    {
        return Event.Priority.ToLower() switch
        {
            "critical" => "priority-critical",
            "high" => "priority-high",
            "medium" => "priority-medium",
            "low" => "priority-low",
            _ => "priority-medium"
        };
    }
    
    private string GetPulseClass()
    {
        return Event.Priority == "Critical" ? "pulse-critical" : "";
    }
    
    private int GetPriorityLevel()
    {
        return Event.Priority.ToLower() switch
        {
            "critical" => 4,
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 2
        };
    }
    
    private decimal CalculateOrbitRadius(NewsArticleDto article)
    {
        // Radio base + factor de relevancia
        var baseRadius = 60m;
        var relevanceFactor = article.Relevance / 100m;
        return baseRadius + (relevanceFactor * 40m);
    }
    
    private decimal CalculateOrbitSpeed(NewsArticleDto article)
    {
        // Velocidad inversamente proporcional a la relevancia
        // Artículos más relevantes orbitan más lento (más estables)
        var baseSpeed = 30m; // segundos
        var relevanceFactor = article.Relevance / 100m;
        return baseSpeed + (relevanceFactor * 20m);
    }
    
    private string GetTrendIcon()
    {
        return Event.Trend switch
        {
            "Increasing" => "fa-arrow-up",
            "Decreasing" => "fa-arrow-down", 
            "Surging" => "fa-rocket",
            "Declining" => "fa-arrow-trend-down",
            _ => "fa-minus"
        };
    }
    
    private string GetTrendText()
    {
        return Event.Trend switch
        {
            "Increasing" => "↑ Creciendo",
            "Decreasing" => "↓ Decreciendo",
            "Surging" => "↑↑ En Auge",
            "Declining" => "↓↓ Declive",
            _ => "→ Estable"
        };
    }
    
    private string TruncateTitle(string title, int maxLength)
    {
        return title.Length <= maxLength ? title : title.Substring(0, maxLength) + "...";
    }
}
```

---

## ⚡ **JAVASCRIPT Y ANIMACIONES**

### **universe.js - Motor Principal del Universo**

```javascript
// wwwroot/js/universe.js
class NewsUniverseEngine {
    constructor() {
        this.container = null;
        this.events = [];
        this.systems = [];
        this.connections = [];
        this.animationFrame = null;
        this.isInitialized = false;
        
        // Configuración
        this.config = {
            autoLayout: true,
            animationsEnabled: true,
            showConnections: true,
            maxEvents: 20,
            layoutAlgorithm: 'force-directed',
            physics: {
                attraction: 0.1,
                repulsion: 0.5,
                damping: 0.9
            }
        };
    }
    
    initialize(containerElement) {
        this.container = containerElement;
        this.isInitialized = true;
        
        // Configurar event listeners
        this.setupEventListeners();
        
        // Inicializar sistema de físicas
        this.initializePhysics();
        
        // Comenzar loop de animación
        this.startAnimationLoop();
        
        console.log('🌌 News Universe Engine initialized');
    }
    
    updateEvents(events) {
        this.events = events;
        
        if (this.config.autoLayout) {
            this.calculateLayout();
        }
        
        this.updateStellarSystems();
        this.updateConnections();
        
        // Trigger layout animation
        this.animateLayoutChange();
    }
    
    calculateLayout() {
        const containerRect = this.container.getBoundingClientRect();
        const centerX = containerRect.width / 2;
        const centerY = containerRect.height / 2;
        
        switch (this.config.layoutAlgorithm) {
            case 'circular':
                this.calculateCircularLayout(centerX, centerY);
                break;
            case 'force-directed':
                this.calculateForceDirectedLayout();
                break;
            case 'grid':
                this.calculateGridLayout();
                break;
            default:
                this.calculateSmartLayout(centerX, centerY);
        }
    }
    
    calculateSmartLayout(centerX, centerY) {
        // Algoritmo inteligente basado en prioridad e impacto
        const sortedEvents = [...this.events].sort((a, b) => {
            const priorityWeight = {
                'Critical': 4,
                'High': 3, 
                'Medium': 2,
                'Low': 1
            };
            
            const priorityDiff = (priorityWeight[b.priority] || 2) - (priorityWeight[a.priority] || 2);
            if (priorityDiff !== 0) return priorityDiff;
            
            return b.impactScore - a.impactScore;
        });
        
        // Colocar evento más importante en el centro
        if (sortedEvents.length > 0) {
            this.setEventPosition(sortedEvents[0], centerX, centerY);
        }
        
        // Distribuir otros eventos en espiral
        let angle = 0;
        let radius = 150;
        const angleIncrement = (2 * Math.PI) / Math.max(1, sortedEvents.length - 1);
        
        for (let i = 1; i < sortedEvents.length; i++) {
            const x = centerX + radius * Math.cos(angle);
            const y = centerY + radius * Math.sin(angle);
            
            this.setEventPosition(sortedEvents[i], x, y);
            
            angle += angleIncrement;
            
            // Incrementar radio en espiral
            if (i % 6 === 0) {
                radius += 100;
            }
        }
        
        // Aplicar anti-overlap algorithm
        this.resolveOverlaps();
    }
    
    calculateForceDirectedLayout() {
        // Implementar algoritmo de layout dirigido por fuerzas
        const nodes = this.events.map(event => ({
            id: event.id,
            x: Math.random() * this.container.clientWidth,
            y: Math.random() * this.container.clientHeight,
            vx: 0,
            vy: 0,
            mass: this.getEventMass(event),
            event: event
        }));
        
        // Simular fuerzas por múltiples iteraciones
        for (let iteration = 0; iteration < 50; iteration++) {
            // Fuerzas de repulsión entre todos los nodos
            for (let i = 0; i < nodes.length; i++) {
                for (let j = i + 1; j < nodes.length; j++) {
                    this.applyRepulsionForce(nodes[i], nodes[j]);
                }
            }
            
            // Fuerzas de atracción para eventos relacionados
            this.applyAttractionForces(nodes);
            
            // Aplicar velocidades
            nodes.forEach(node => {
                node.x += node.vx;
                node.y += node.vy;
                node.vx *= this.config.physics.damping;
                node.vy *= this.config.physics.damping;
                
                // Mantener dentro de los límites
                node.x = Math.max(50, Math.min(this.container.clientWidth - 50, node.x));
                node.y = Math.max(50, Math.min(this.container.clientHeight - 50, node.y));
            });
        }
        
        // Aplicar posiciones finales
        nodes.forEach(node => {
            this.setEventPosition(node.event, node.x, node.y);
        });
    }
    
    applyRepulsionForce(node1, node2) {
        const dx = node2.x - node1.x;
        const dy = node2.y - node1.y;
        const distance = Math.sqrt(dx * dx + dy * dy);
        
        if (distance < 200) { // Rango de repulsión
            const force = this.config.physics.repulsion / (distance * distance);
            const fx = force * dx / distance;
            const fy = force * dy / distance;
            
            node1.vx -= fx / node1.mass;
            node1.vy -= fy / node1.mass;
            node2.vx += fx / node2.mass;
            node2.vy += fy / node2.mass;
        }
    }
    
    setEventPosition(event, x, y) {
        const systemElement = this.container.querySelector(`[data-event-id="${event.id}"]`);
        if (systemElement) {
            systemElement.style.left = `${x}px`;
            systemElement.style.top = `${y}px`;
            systemElement.style.transform = 'translate(-50%, -50%)';
        }
    }
    
    animateSystemEntry(eventId) {
        const systemElement = this.container.querySelector(`[data-event-id="${eventId}"]`);
        if (systemElement) {
            // Animación de entrada desde el espacio
            systemElement.style.transform = 'translate(-50%, -50%) scale(0) rotateY(90deg)';
            systemElement.style.opacity = '0';
            
            requestAnimationFrame(() => {
                systemElement.style.transition = 'all 1s cubic-bezier(0.175, 0.885, 0.32, 1.275)';
                systemElement.style.transform = 'translate(-50%, -50%) scale(1) rotateY(0deg)';
                systemElement.style.opacity = '1';
            });
        }
    }
    
    animateLayoutChange() {
        // Animación suave de cambios de layout
        const systems = this.container.querySelectorAll('.stellar-system');
        systems.forEach(system => {
            if (!system.style.transition) {
                system.style.transition = 'left 0.8s ease-out, top 0.8s ease-out';
            }
        });
    }
    
    updateConnections() {
        if (!this.config.showConnections) return;
        
        // Limpiar conexiones existentes
        this.clearConnections();
        
        // Crear nuevas conexiones
        this.events.forEach(event => {
            if (event.relatedEvents) {
                event.relatedEvents.forEach(relatedEvent => {
                    this.createConnection(event.id, relatedEvent.id, relatedEvent.connectionStrength);
                });
            }
        });
    }
    
    createConnection(fromEventId, toEventId, strength) {
        const fromElement = this.container.querySelector(`[data-event-id="${fromEventId}"]`);
        const toElement = this.container.querySelector(`[data-event-id="${toEventId}"]`);
        
        if (fromElement && toElement) {
            const svg = this.getOrCreateConnectionSVG();
            
            const fromRect = fromElement.getBoundingClientRect();
            const toRect = toElement.getBoundingClientRect();
            const containerRect = this.container.getBoundingClientRect();
            
            const x1 = fromRect.left - containerRect.left + fromRect.width / 2;
            const y1 = fromRect.top - containerRect.top + fromRect.height / 2;
            const x2 = toRect.left - containerRect.left + toRect.width / 2;
            const y2 = toRect.top - containerRect.top + toRect.height / 2;
            
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', x1);
            line.setAttribute('y1', y1);
            line.setAttribute('x2', x2);
            line.setAttribute('y2', y2);
            line.setAttribute('stroke', `rgba(0, 255, 136, ${strength})`);
            line.setAttribute('stroke-width', Math.max(1, strength * 3));
            line.setAttribute('stroke-dasharray', '5,5');
            line.classList.add('connection-line');
            
            // Animación de la línea
            const length = Math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2);
            line.style.strokeDasharray = `0,${length}`;
            line.style.animation = `drawLine 1s ease-out forwards`;
            
            svg.appendChild(line);
        }
    }
    
    getOrCreateConnectionSVG() {
        let svg = this.container.querySelector('.connections-svg');
        if (!svg) {
            svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.classList.add('connections-svg');
            svg.style.position = 'absolute';
            svg.style.top = '0';
            svg.style.left = '0';
            svg.style.width = '100%';
            svg.style.height = '100%';
            svg.style.pointerEvents = 'none';
            svg.style.zIndex = '1';
            this.container.appendChild(svg);
        }
        return svg;
    }
    
    clearConnections() {
        const svg = this.container.querySelector('.connections-svg');
        if (svg) {
            svg.innerHTML = '';
        }
    }
    
    setupEventListeners() {
        // Click events delegados
        this.container.addEventListener('click', (e) => {
            const stellarSystem = e.target.closest('.stellar-system');
            if (stellarSystem) {
                const eventId = parseInt(stellarSystem.dataset.eventId);
                if (this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync('OnEventClickFromJS', eventId);
                }
            }
            
            const planet = e.target.closest('.planet-component');
            if (planet) {
                const articleId = parseInt(planet.dataset.articleId);
                if (this.dotNetHelper) {
                    this.dotNetHelper.invokeMethodAsync('OnArticleClickFromJS', articleId);
                }
            }
        });
        
        // Hover effects
        this.container.addEventListener('mouseover', (e) => {
            const stellarSystem = e.target.closest('.stellar-system');
            if (stellarSystem) {
                this.highlightSystem(stellarSystem);
            }
        });
        
        this.container.addEventListener('mouseout', (e) => {
            const stellarSystem = e.target.closest('.stellar-system');
            if (stellarSystem) {
                this.unhighlightSystem(stellarSystem);
            }
        });
        
        // Resize handling
        window.addEventListener('resize', () => {
            this.handleResize();
        });
    }
    
    setupEventListeners() {
        this.dotNetHelper = dotNetHelper;
    }
    
    highlightSystem(systemElement) {
        systemElement.classList.add('highlighted');
        
        // Highlighting related systems
        const eventId = systemElement.dataset.eventId;
        const relatedSystems = this.container.querySelectorAll(
            `[data-event-id]:not([data-event-id="${eventId}"])`
        );
        
        relatedSystems.forEach(system => {
            system.style.opacity = '0.3';
        });
    }
    
    unhighlightSystem(systemElement) {
        systemElement.classList.remove('highlighted');
        
        // Reset opacity for all systems
        const allSystems = this.container.querySelectorAll('[data-event-id]');
        allSystems.forEach(system => {
            system.style.opacity = '1';
        });
    }
    
    startAnimationLoop() {
        const animate = () => {
            if (this.config.animationsEnabled) {
                this.updateAnimations();
            }
            this.animationFrame = requestAnimationFrame(animate);
        };
        animate();
    }
    
    updateAnimations() {
        // Actualizar animaciones de órbitas
        this.updateOrbitAnimations();
        
        // Actualizar efectos de pulsación
        this.updatePulseEffects();
        
        // Actualizar partículas de energía
        this.updateEnergyParticles();
    }
    
    updateOrbitAnimations() {
        const planets = this.container.querySelectorAll('.planet-component');
        planets.forEach(planet => {
            const orbitSpeed = parseFloat(planet.style.getPropertyValue('--orbit-speed') || '30');
            const currentRotation = parseFloat(planet.dataset.rotation || '0');
            const newRotation = currentRotation + (360 / (orbitSpeed * 60)); // 60 FPS assumed
            
            planet.dataset.rotation = newRotation.toString();
            planet.style.transform = `rotate(${newRotation}deg)`;
        });
    }
    
    destroy() {
        if (this.animationFrame) {
            cancelAnimationFrame(this.animationFrame);
        }
        
        window.removeEventListener('resize', this.handleResize);
        this.isInitialized = false;
    }
}

// Global instance
window.universe = new NewsUniverseEngine();

// Export functions for Blazor interop
window.universe.initialize = (containerElement) => {
    window.universe.initialize(containerElement);
};

window.universe.updateEvents = (events) => {
    window.universe.updateEvents(events);
};

window.universe.animateSystemEntry = (eventId) => {
    window.universe.animateSystemEntry(eventId);
};

window.universe.setupEventListeners = (dotNetHelper) => {
    window.universe.setupEventListeners(dotNetHelper);
};
```

---

## ✅ **ESTADO DE DOCUMENTACIÓN COMPLETA**

He creado la documentación principal del sistema agéntico:

### ✅ **Archivos Completados**
1. **AGENTIC_README.md** - Visión general del proyecto
2. **AGENTIC_ARCHITECTURE.md** - Arquitectura completa del sistema  
3. **AGENTIC_DATA_MODEL.md** - Modelo de datos detallado
4. **AGENTIC_AI_SERVICES.md** - Servicios de IA y agentes
5. **AGENTIC_FRONTEND.md** - Frontend y experiencia de usuario (este archivo)

### 📋 **Archivos Pendientes** (puedes crearlos en código)
- AGENTIC_API_REFERENCE.md - Documentación de API REST
- AGENTIC_DATABASE.md - Scripts SQL y configuración
- AGENTIC_DEPLOYMENT.md - Docker y despliegue completo
- AGENTIC_INSTALLATION.md - Guía paso a paso
- AGENTIC_USER_MANUAL.md - Manual usuario final

---

**🚀 PARA CONTINUAR EN CÓDIGO:**

1. **Backend .NET 8**: Implementar los agentes autónomos y API
2. **Frontend Blazor**: Componentes del universo de noticias  
3. **Base de datos**: Esquemas PostgreSQL y migraciones
4. **IA Integration**: Conectar con OpenAI/Claude APIs
5. **Docker Setup**: Containerización y despliegue

¿Te descargo los archivos que tengo para que puedas empezar la implementación?
