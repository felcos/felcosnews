/* ============================================================
   universe3d.js  v1 — 3D Universe for AgenteNews (Three.js r128)
   Same API as universe.js: init, updateData, destroy
   ============================================================ */
(function () {
    'use strict';

    // ─────────────────────────────────────────────────────────────
    //  ORBITAL CONTROLS (manual — no import needed)
    // ─────────────────────────────────────────────────────────────
    class OrbitalControls {
        constructor(camera, domElement) {
            this.camera = camera;
            this.el = domElement;
            this.theta = 0;
            this.phi = Math.PI / 4;
            this.radius = 1200;
            this.target = new THREE.Vector3(0, 0, 0);
            this._rotating = false;
            this._panning = false;
            this._lastMouse = { x: 0, y: 0 };
            this._pinchDist = null;

            this._onMouseDown = this._onMouseDown.bind(this);
            this._onMouseMove = this._onMouseMove.bind(this);
            this._onMouseUp = this._onMouseUp.bind(this);
            this._onWheel = this._onWheel.bind(this);
            this._onTouchStart = this._onTouchStart.bind(this);
            this._onTouchMove = this._onTouchMove.bind(this);
            this._onTouchEnd = this._onTouchEnd.bind(this);

            this.el.addEventListener('mousedown', this._onMouseDown);
            window.addEventListener('mousemove', this._onMouseMove);
            window.addEventListener('mouseup', this._onMouseUp);
            this.el.addEventListener('wheel', this._onWheel, { passive: false });
            this.el.addEventListener('touchstart', this._onTouchStart, { passive: false });
            this.el.addEventListener('touchmove', this._onTouchMove, { passive: false });
            this.el.addEventListener('touchend', this._onTouchEnd);

            this.update();
        }

        _onMouseDown(e) {
            if (e.button === 0) { this._rotating = true; }
            else if (e.button === 1) { this._panning = true; e.preventDefault(); }
            this._lastMouse = { x: e.clientX, y: e.clientY };
        }
        _onMouseMove(e) {
            const dx = e.clientX - this._lastMouse.x;
            const dy = e.clientY - this._lastMouse.y;
            this._lastMouse = { x: e.clientX, y: e.clientY };
            if (this._rotating) {
                this.theta -= dx * 0.005;
                this.phi = Math.max(0.1, Math.min(Math.PI - 0.1, this.phi - dy * 0.005));
            }
            if (this._panning) {
                const panSpeed = this.radius * 0.001;
                const right = new THREE.Vector3();
                const up = new THREE.Vector3();
                this.camera.getWorldDirection(up);
                right.crossVectors(up, this.camera.up).normalize();
                up.copy(this.camera.up).normalize();
                this.target.add(right.multiplyScalar(-dx * panSpeed));
                this.target.add(up.multiplyScalar(dy * panSpeed));
            }
        }
        _onMouseUp() { this._rotating = false; this._panning = false; }
        _onWheel(e) {
            e.preventDefault();
            this.radius = Math.max(200, Math.min(4000, this.radius + e.deltaY * 0.5));
        }
        _onTouchStart(e) {
            if (e.touches.length === 1) {
                this._rotating = true;
                this._lastMouse = { x: e.touches[0].clientX, y: e.touches[0].clientY };
            } else if (e.touches.length === 2) {
                this._rotating = false;
                const dx = e.touches[0].clientX - e.touches[1].clientX;
                const dy = e.touches[0].clientY - e.touches[1].clientY;
                this._pinchDist = Math.sqrt(dx * dx + dy * dy);
                e.preventDefault();
            }
        }
        _onTouchMove(e) {
            if (e.touches.length === 1 && this._rotating) {
                const dx = e.touches[0].clientX - this._lastMouse.x;
                const dy = e.touches[0].clientY - this._lastMouse.y;
                this._lastMouse = { x: e.touches[0].clientX, y: e.touches[0].clientY };
                this.theta -= dx * 0.005;
                this.phi = Math.max(0.1, Math.min(Math.PI - 0.1, this.phi - dy * 0.005));
            } else if (e.touches.length === 2 && this._pinchDist !== null) {
                e.preventDefault();
                const dx = e.touches[0].clientX - e.touches[1].clientX;
                const dy = e.touches[0].clientY - e.touches[1].clientY;
                const newDist = Math.sqrt(dx * dx + dy * dy);
                const factor = this._pinchDist / newDist;
                this.radius = Math.max(200, Math.min(4000, this.radius * factor));
                this._pinchDist = newDist;
            }
        }
        _onTouchEnd() { this._rotating = false; this._pinchDist = null; }

        update() {
            const sp = Math.sin(this.phi);
            this.camera.position.set(
                this.target.x + this.radius * sp * Math.sin(this.theta),
                this.target.y + this.radius * Math.cos(this.phi),
                this.target.z + this.radius * sp * Math.cos(this.theta)
            );
            this.camera.lookAt(this.target);
        }

        dispose() {
            this.el.removeEventListener('mousedown', this._onMouseDown);
            window.removeEventListener('mousemove', this._onMouseMove);
            window.removeEventListener('mouseup', this._onMouseUp);
            this.el.removeEventListener('wheel', this._onWheel);
            this.el.removeEventListener('touchstart', this._onTouchStart);
            this.el.removeEventListener('touchmove', this._onTouchMove);
            this.el.removeEventListener('touchend', this._onTouchEnd);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  UTILITIES
    // ─────────────────────────────────────────────────────────────
    function hexToColor(hex) {
        if (!hex || typeof hex !== 'string') return new THREE.Color(0x4a90e2);
        return new THREE.Color(hex);
    }

    function planetRadius(ev) {
        const bonus = { Critical: 16, High: 11, Medium: 6, Low: 2 };
        const b = bonus[ev.priority] || 0;
        const impact = Math.min(1, Math.max(0, (ev.impactScore || 0) / 100));
        return Math.min(52, 24 + b + impact * 16);
    }

    function fibonacciSphere(n, radius) {
        const pts = [];
        if (n === 0) return pts;
        const golden = Math.PI * (3 - Math.sqrt(5));
        for (let i = 0; i < n; i++) {
            const y = n === 1 ? 0 : 1 - (i / (n - 1)) * 2;
            const r = Math.sqrt(1 - y * y);
            const theta = golden * i;
            pts.push(new THREE.Vector3(
                Math.cos(theta) * r * radius,
                y * radius,
                Math.sin(theta) * r * radius
            ));
        }
        return pts;
    }

    function resolveCollisions3D(positions, radii, padding, maxIter) {
        maxIter = maxIter || 30;
        for (let iter = 0; iter < maxIter; iter++) {
            let moved = false;
            for (let i = 0; i < positions.length; i++) {
                for (let j = i + 1; j < positions.length; j++) {
                    const diff = positions[i].clone().sub(positions[j]);
                    const dist = diff.length();
                    const minDist = radii[i] + radii[j] + padding;
                    if (dist < minDist && dist > 0.01) {
                        const overlap = (minDist - dist) / 2;
                        diff.normalize().multiplyScalar(overlap);
                        positions[i].add(diff);
                        positions[j].sub(diff);
                        moved = true;
                    }
                }
            }
            if (!moved) break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  ENGINE CLASS
    // ─────────────────────────────────────────────────────────────
    class Universe3DEngine {
        constructor(canvas, events, sections, dotnetRef) {
            this.canvas = canvas;
            this.events = events || [];
            this.sections = sections || [];
            this._dotnet = dotnetRef || null;

            this.systems = [];
            this.planetMeshes = []; // { mesh, node, system }
            this.moonData = [];     // { mesh, parentMesh, orbitR, angleOffset, speed }
            this._hovered = null;
            this._tooltip = null;

            const parent = canvas.parentElement;
            const W = parent.clientWidth || 800;
            const H = parent.clientHeight || 600;

            // Renderer
            this.renderer = new THREE.WebGLRenderer({
                canvas: canvas,
                antialias: true,
                alpha: false
            });
            this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
            this.renderer.setSize(W, H);
            this.renderer.setClearColor(0x000511);

            // Scene
            this.scene = new THREE.Scene();

            // Camera
            this.camera = new THREE.PerspectiveCamera(60, W / H, 1, 20000);
            this.camera.position.set(0, 0, 1200);

            // Lights
            this.scene.add(new THREE.AmbientLight(0x111122, 0.4));
            const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
            dirLight.position.set(500, 800, 600);
            this.scene.add(dirLight);

            // Controls
            this._controls = new OrbitalControls(this.camera, canvas);

            // Raycaster
            this.raycaster = new THREE.Raycaster();
            this.mouse = new THREE.Vector2(-999, -999);

            // Build scene
            this._buildSystems();
            this._buildScene();
            this._buildStarfield();
            this._buildNebulae();
            this._createTooltip();

            // Events
            this._bindEvents();

            // Resize
            this._ro = new ResizeObserver(() => this._onResize());
            this._ro.observe(parent);

            // Animation
            this._raf = null;
            this._clock = new THREE.Clock();
            this._loop();
        }

        _onResize() {
            const parent = this.canvas.parentElement;
            const W = parent.clientWidth || 800;
            const H = parent.clientHeight || 600;
            this.camera.aspect = W / H;
            this.camera.updateProjectionMatrix();
            this.renderer.setSize(W, H);
        }

        // ─────────────────────────────────────────────────────────
        //  DATA BUILD
        // ─────────────────────────────────────────────────────────
        _buildSystems() {
            const slugSet = new Set();
            for (const ev of this.events) {
                if (ev.sectionSlug) slugSet.add(ev.sectionSlug);
            }
            const slugs = [...slugSet];

            this.systems = slugs.map(slug => {
                const secDef = this.sections.find(s => s.slug === slug) || {};
                return {
                    slug,
                    name: secDef.name || slug,
                    color: secDef.color || '#4a90e2',
                    x: 0, y: 0, z: 0,
                    pulsePhase: Math.random() * Math.PI * 2
                };
            });

            // Arrange centers (same logic as 2D, scaled x2.5, with z=0)
            const n = this.systems.length;
            const positions = this._arrangeSystemCenters(n);
            for (let i = 0; i < this.systems.length; i++) {
                this.systems[i].x = positions[i].x * 2.5;
                this.systems[i].y = 0;
                this.systems[i].z = positions[i].y * 2.5;
            }
        }

        _arrangeSystemCenters(n) {
            if (n === 0) return [];
            if (n === 1) return [{ x: 0, y: 0 }];
            if (n === 2) return [{ x: -260, y: 0 }, { x: 260, y: 0 }];
            if (n === 3) return [{ x: 0, y: -230 }, { x: -265, y: 180 }, { x: 265, y: 180 }];
            if (n <= 6) {
                const cols = Math.ceil(n / 2);
                const spacingX = 480, spacingY = 420;
                const result = [];
                for (let i = 0; i < n; i++) {
                    const row = Math.floor(i / cols);
                    const col = i % cols;
                    const rowCount = (row === 0) ? cols : n - cols;
                    const rowOffset = -(rowCount - 1) * spacingX / 2;
                    result.push({ x: rowOffset + col * spacingX, y: (row - 0.5) * spacingY });
                }
                return result;
            }
            const rx = 300, ry = 210;
            const scale = 1 + (n - 7) * 0.08;
            return Array.from({ length: n }, (_, i) => {
                const angle = (i / n) * Math.PI * 2 - Math.PI / 2;
                return { x: Math.cos(angle) * rx * scale, y: Math.sin(angle) * ry * scale };
            });
        }

        // ─────────────────────────────────────────────────────────
        //  SCENE BUILD
        // ─────────────────────────────────────────────────────────
        _buildScene() {
            // Clear existing
            this.planetMeshes = [];
            this.moonData = [];

            const groups = {};
            for (const ev of this.events) {
                const slug = ev.sectionSlug || '__none__';
                if (!groups[slug]) groups[slug] = [];
                groups[slug].push(ev);
            }

            const priorityOrder = { Critical: 0, High: 1, Medium: 2, Low: 3 };
            for (const slug of Object.keys(groups)) {
                groups[slug].sort((a, b) => {
                    const pa = priorityOrder[a.priority] ?? 9;
                    const pb = priorityOrder[b.priority] ?? 9;
                    if (pa !== pb) return pa - pb;
                    return (b.impactScore || 0) - (a.impactScore || 0);
                });
            }

            const pColors = { Critical: '#ff0040', High: '#ff6600', Medium: '#e6b800', Low: '#4a90e2' };

            for (const sys of this.systems) {
                const evList = groups[sys.slug] || [];
                if (evList.length === 0) continue;

                const group = new THREE.Group();
                group.position.set(sys.x, sys.y, sys.z);

                // System point light
                const sysColor = hexToColor(sys.color);
                const pointLight = new THREE.PointLight(sysColor.getHex(), 1.5, 600, 2);
                group.add(pointLight);

                // Sun mesh
                const sunGeo = new THREE.SphereGeometry(28, 32, 32);
                const sunMat = new THREE.MeshBasicMaterial({ color: sysColor });
                const sunMesh = new THREE.Mesh(sunGeo, sunMat);
                group.add(sunMesh);

                // Sun glow sprite
                const spriteMat = new THREE.SpriteMaterial({
                    color: sysColor.getHex(),
                    transparent: true,
                    opacity: 0.3,
                    blending: THREE.AdditiveBlending
                });
                const sprite = new THREE.Sprite(spriteMat);
                sprite.scale.set(160, 160, 1);
                group.add(sprite);

                // Planets
                const sphRadius = Math.max(150, Math.min(280, 100 + evList.length * 18));
                const positions = fibonacciSphere(evList.length, sphRadius);
                const radii = evList.map(ev => planetRadius(ev) * 0.7); // scale down for 3D

                resolveCollisions3D(positions, radii, 20, 30);

                for (let i = 0; i < evList.length; i++) {
                    const ev = evList[i];
                    const r = radii[i];
                    const pos = positions[i];

                    const baseColor = hexToColor(pColors[ev.priority] || '#4a90e2');
                    // HSL shift for variety
                    const hsl = {};
                    baseColor.getHSL(hsl);
                    hsl.h += ((ev.id * 0.037) % 0.08) - 0.04;
                    const planetColor = new THREE.Color().setHSL(hsl.h, hsl.s, hsl.l);

                    const geo = new THREE.SphereGeometry(r, 32, 32);
                    const mat = new THREE.MeshPhongMaterial({
                        color: planetColor,
                        shininess: 60,
                        specular: 0x333333
                    });
                    const mesh = new THREE.Mesh(geo, mat);
                    mesh.position.copy(pos);
                    group.add(mesh);

                    mesh.userData = { event: ev, system: sys };
                    this.planetMeshes.push({ mesh, event: ev, system: sys });

                    // Moons
                    const moons = (ev.articles || []).slice(0, 4);
                    for (let mi = 0; mi < moons.length; mi++) {
                        const moonGeo = new THREE.SphereGeometry(4, 8, 8);
                        const moonMat = new THREE.MeshPhongMaterial({
                            color: planetColor.clone().offsetHSL(0, 0, 0.15),
                            shininess: 30
                        });
                        const moonMesh = new THREE.Mesh(moonGeo, moonMat);
                        group.add(moonMesh);
                        this.moonData.push({
                            mesh: moonMesh,
                            parentPos: pos,
                            parentGroup: group,
                            orbitR: r * 2.2 + mi * 15,
                            angleOffset: (mi / moons.length) * Math.PI * 2,
                            speed: 0.4 + mi * 0.1
                        });
                    }
                }

                // Galaxy border (point cloud sphere)
                let maxDist = 0;
                for (const pos of positions) {
                    const d = pos.length();
                    if (d > maxDist) maxDist = d;
                }
                const borderRadius = maxDist + 60;
                const borderPts = [];
                for (let i = 0; i < 300; i++) {
                    const phi = Math.acos(2 * Math.random() - 1);
                    const theta = Math.random() * Math.PI * 2;
                    borderPts.push(
                        borderRadius * Math.sin(phi) * Math.cos(theta),
                        borderRadius * Math.sin(phi) * Math.sin(theta),
                        borderRadius * Math.cos(phi)
                    );
                }
                const borderGeo = new THREE.BufferGeometry();
                borderGeo.setAttribute('position', new THREE.Float32BufferAttribute(borderPts, 3));
                const borderMat = new THREE.PointsMaterial({
                    color: sysColor.getHex(),
                    size: 1.5,
                    transparent: true,
                    opacity: 0.25,
                    sizeAttenuation: true
                });
                group.add(new THREE.Points(borderGeo, borderMat));

                // System name label (sprite text)
                const labelCanvas = document.createElement('canvas');
                labelCanvas.width = 256;
                labelCanvas.height = 64;
                const lctx = labelCanvas.getContext('2d');
                lctx.font = '600 28px system-ui';
                lctx.textAlign = 'center';
                lctx.textBaseline = 'middle';
                lctx.fillStyle = sys.color;
                lctx.fillText(sys.name, 128, 32);
                const labelTex = new THREE.CanvasTexture(labelCanvas);
                const labelMat = new THREE.SpriteMaterial({
                    map: labelTex,
                    transparent: true,
                    opacity: 0.8
                });
                const labelSprite = new THREE.Sprite(labelMat);
                labelSprite.scale.set(120, 30, 1);
                labelSprite.position.set(0, -sphRadius - 40, 0);
                group.add(labelSprite);

                this.scene.add(group);
            }
        }

        _buildStarfield() {
            const count = 6000;
            const positions = [];
            for (let i = 0; i < count; i++) {
                const phi = Math.acos(2 * Math.random() - 1);
                const theta = Math.random() * Math.PI * 2;
                const r = 8000;
                positions.push(
                    r * Math.sin(phi) * Math.cos(theta),
                    r * Math.sin(phi) * Math.sin(theta),
                    r * Math.cos(phi)
                );
            }
            const geo = new THREE.BufferGeometry();
            geo.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
            const mat = new THREE.PointsMaterial({
                color: 0xffffff,
                size: 1.2,
                transparent: true,
                opacity: 0.7,
                sizeAttenuation: true
            });
            this.scene.add(new THREE.Points(geo, mat));
        }

        _buildNebulae() {
            const nebulae = [
                { pos: [2000, 500, -4000], color: 0x142878, size: 2000 },
                { pos: [-3000, -200, -3500], color: 0x501478, size: 2000 },
                { pos: [0, -1000, -5000], color: 0x14503c, size: 2000 }
            ];
            for (const n of nebulae) {
                const geo = new THREE.PlaneGeometry(n.size, n.size);
                const mat = new THREE.MeshBasicMaterial({
                    color: n.color,
                    transparent: true,
                    opacity: 0.04,
                    blending: THREE.AdditiveBlending,
                    side: THREE.DoubleSide,
                    depthWrite: false
                });
                const mesh = new THREE.Mesh(geo, mat);
                mesh.position.set(n.pos[0], n.pos[1], n.pos[2]);
                mesh.lookAt(0, 0, 0);
                this.scene.add(mesh);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  TOOLTIP
        // ─────────────────────────────────────────────────────────
        _createTooltip() {
            const tip = document.createElement('div');
            tip.id = '_univ3d_tooltip';
            tip.style.cssText = `
                position: absolute; display: none; pointer-events: none;
                background: rgba(0,5,17,0.95); border: 1px solid rgba(74,144,226,0.5);
                border-radius: 8px; padding: 8px 12px; color: #e8eaf0;
                font-size: 12px; max-width: 220px; z-index: 20;
                box-shadow: 0 4px 16px rgba(0,0,0,0.5);
            `;
            this.canvas.parentElement.style.position = 'relative';
            this.canvas.parentElement.appendChild(tip);
            this._tooltip = tip;
        }

        _showTooltip(ev, screenX, screenY) {
            const pColor = { Critical: '#ff0040', High: '#ff6600', Medium: '#ffcc00', Low: '#4a90e2' };
            const pc = pColor[ev.priority] || '#4a90e2';
            this._tooltip.innerHTML = `
                <div style="display:flex;align-items:center;gap:6px;margin-bottom:4px">
                    <span style="background:${pc};color:#fff;font-size:9px;font-weight:700;padding:1px 5px;border-radius:3px">${ev.priority || ''}</span>
                    <span style="color:#8892a4;font-size:10px">${ev.sectionName || ''}</span>
                </div>
                <div style="font-weight:600;line-height:1.3;margin-bottom:3px">${ev.title || ''}</div>
                <div style="color:#8892a4;font-size:11px">${ev.articleCount || 0} articulos · Impacto: ${(ev.impactScore || 0).toFixed(0)}</div>
            `;
            this._tooltip.style.left = (screenX + 16) + 'px';
            this._tooltip.style.top = (screenY - 10) + 'px';
            this._tooltip.style.display = 'block';
        }

        _hideTooltip() {
            this._tooltip.style.display = 'none';
        }

        // ─────────────────────────────────────────────────────────
        //  EVENTS
        // ─────────────────────────────────────────────────────────
        _bindEvents() {
            this.canvas.addEventListener('mousemove', e => {
                const rect = this.canvas.getBoundingClientRect();
                this.mouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
                this.mouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
                this._mouseScreenX = e.clientX - rect.left;
                this._mouseScreenY = e.clientY - rect.top;
            });

            this.canvas.addEventListener('click', e => {
                if (this._hovered) {
                    const ev = this._hovered.event;
                    if (this._dotnet && ev && ev.id) {
                        try { this._dotnet.invokeMethodAsync('OpenEventById', ev.id); }
                        catch (err) { console.warn('[universe3d] OpenEventById failed:', err); }
                    }
                }
            });
        }

        // ─────────────────────────────────────────────────────────
        //  LOOP
        // ─────────────────────────────────────────────────────────
        _loop() {
            this._raf = requestAnimationFrame(() => this._loop());
            if (document.hidden) return;

            const dt = this._clock.getDelta();
            const elapsed = this._clock.getElapsedTime();

            // Animate moons
            for (const md of this.moonData) {
                const angle = elapsed * md.speed + md.angleOffset;
                md.mesh.position.set(
                    md.parentPos.x + Math.cos(angle) * md.orbitR,
                    md.parentPos.y + Math.sin(angle * 0.7) * md.orbitR * 0.3,
                    md.parentPos.z + Math.sin(angle) * md.orbitR
                );
            }

            // Hover detection
            this.raycaster.setFromCamera(this.mouse, this.camera);
            const meshes = this.planetMeshes.map(p => p.mesh);
            const intersects = this.raycaster.intersectObjects(meshes);

            if (intersects.length > 0) {
                const hit = intersects[0].object;
                const entry = this.planetMeshes.find(p => p.mesh === hit);
                if (entry) {
                    if (this._hovered !== entry) {
                        // Unhover previous
                        if (this._hovered) this._hovered.mesh.scale.setScalar(1);
                        this._hovered = entry;
                    }
                    // Scale up with lerp
                    const s = hit.scale.x;
                    hit.scale.setScalar(s + (1.25 - s) * 0.15);
                    this._showTooltip(entry.event, this._mouseScreenX || 0, this._mouseScreenY || 0);
                    this.canvas.style.cursor = 'pointer';
                }
            } else {
                if (this._hovered) {
                    this._hovered.mesh.scale.setScalar(1);
                    this._hovered = null;
                }
                this._hideTooltip();
                this.canvas.style.cursor = 'grab';
            }

            // Update controls & render
            this._controls.update();
            this.renderer.render(this.scene, this.camera);
        }

        // ─────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────
        updateData(events, sections) {
            this.events = events || [];
            this.sections = sections || this.sections;

            // Clear scene objects (keep lights, stars, nebulae)
            const toRemove = [];
            this.scene.traverse(obj => {
                if (obj.isGroup) toRemove.push(obj);
            });
            for (const obj of toRemove) {
                this.scene.remove(obj);
                obj.traverse(child => {
                    if (child.geometry) child.geometry.dispose();
                    if (child.material) {
                        if (child.material.map) child.material.map.dispose();
                        child.material.dispose();
                    }
                });
            }

            this._buildSystems();
            this._buildScene();
        }

        destroy() {
            if (this._raf) cancelAnimationFrame(this._raf);
            if (this._ro) this._ro.disconnect();
            if (this._controls) this._controls.dispose();
            if (this._tooltip) this._tooltip.remove();

            // Dispose all geometries and materials
            this.scene.traverse(obj => {
                if (obj.geometry) obj.geometry.dispose();
                if (obj.material) {
                    if (obj.material.map) obj.material.map.dispose();
                    obj.material.dispose();
                }
            });

            this.renderer.dispose();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  GLOBAL API
    // ─────────────────────────────────────────────────────────────
    let _engine3d = null;

    window.universe3d = {
        init(canvasId, events, sections, dotnetRef) {
            if (typeof THREE === 'undefined') {
                console.error('[universe3d] Three.js not loaded');
                return;
            }
            if (_engine3d) {
                _engine3d.destroy();
                _engine3d = null;
            }
            const canvas = document.getElementById(canvasId);
            if (!canvas) {
                console.warn('[universe3d] canvas not found:', canvasId);
                return;
            }
            console.log(`[universe3d] init: ${(events || []).length} events, ${(sections || []).length} sections`);
            try {
                _engine3d = new Universe3DEngine(canvas, events, sections, dotnetRef);
            } catch (e) {
                console.error('[universe3d] init error:', e);
            }
        },

        updateData(events, sections) {
            if (_engine3d) {
                try { _engine3d.updateData(events, sections); }
                catch (e) { console.error('[universe3d] updateData error:', e); }
            }
        },

        destroy() {
            if (_engine3d) {
                try { _engine3d.destroy(); } catch { }
                _engine3d = null;
            }
        }
    };

})();
