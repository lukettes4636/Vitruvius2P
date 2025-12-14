# ANÁLISIS COMPLETO DEL PROYECTO - Vitruvius2P

## 📋 RESUMEN EJECUTIVO

**Tipo de Juego:** Top-down 3D isométrico cooperativo en Unity 2022.3.5f1  
**Género:** Horror/Survival cooperativo con elementos de puzzle y sigilo  
**Arquitectura:** Sistema de 2 jugadores con mecánicas de cooperación

---

## 🏗️ ARQUITECTURA GENERAL

### Estructura de Directorios Principales
- `Assets/scripts/Players/` - Sistemas de jugador (movimiento, inventario, salud)
- `Assets/scripts/Enemy/` - IA del enemigo (tanto sistema legacy como nuevo modular)
- `Assets/scripts/Puzzle_01/` y `Puzle_02/` - Sistemas de puzzles
- `Assets/scripts/Audio/` - Sistema de audio centralizado
- `Assets/scripts/Menu/` - Menús y navegación
- `Assets/scripts/Checkpoint/` - Sistema de checkpoints y respawn

---

## 👥 SISTEMA DE JUGADORES

### 1. **Movimiento de Jugadores**

#### Clases: `MovJugador1`, `MovJugador2`, `PlayerControllerBase`

**Características principales:**
- **Movimiento basado en CharacterController** con rotación suave
- **Sistema de estamina** con agotamiento progresivo:
  - Velocidades: Caminar (5f), Correr (8f), Agacharse (8.96f)
  - Consumo de estamina al correr, regeneración al parar
  - Cooldown de 4s cuando se agota completamente
- **Aceleración/desaceleración** suave (acceleration: 12f, deceleration: 16f)
- **Alineación con cámara** - el movimiento respeta la dirección de la cámara
- **Sistema de agacharse** con cambio de altura del collider
- **Feedback de fatiga** mediante `StaminaFatigueFeedback` (animación procedural)
- **Sistema de niebla** - VFX que se actualiza con la posición del jugador

**Estados del jugador:**
- Normal
- Corriendo (consume estamina)
- Agachado
- Exhausto (no puede correr tras agotar estamina)
- En UI (deshabilita movimiento)
- Levantando puertas

**Problemas identificados:**
- `MovJugador1` y `MovJugador2` tienen código duplicado significativo
- Existe `PlayerControllerBase` abstracta pero los jugadores no la heredan completamente
- Inconsistencias en nombres de métodos entre P1 y P2

**Recomendación:** Refactorizar para usar completamente `PlayerControllerBase` como clase base.

---

### 2. **Inventario (`PlayerInventory`)**

**Sistema:**
- **KeyCards**: Lista de tarjetas clave coleccionadas
- **Items**: Lista general de objetos coleccionados
- **Items esenciales**: No se pierden al morir (Card, Lever, Key)
- **UI Integration**: Se actualiza automáticamente el hotbar

**Métodos principales:**
- `AddKeyCard(string id)`, `AddItem(string id)`
- `HasKeyCard(string id)`, `HasItem(string id)`
- `UseKeyCard(string id)`, `UseItem(string id)`
- `RemoveNonEssentialItems()` - usado al morir

---

### 3. **Salud (`PlayerHealth`)**

**Características:**
- **Sistema de vida** con máximo configurable (default: 100)
- **Inmunidad temporal** después de recibir daño (0.5s)
- **Daño eléctrico** continuo cuando está en trampas
- **Efectos visuales**: Partículas de sangre en el punto de impacto
- **Shake de cámara y rumble** en gamepad al recibir daño
- **Sistema de muerte**:
  - Animación de muerte
  - Desactivación temporal de renderers/colliders
  - Delay antes de que la cámara deje de seguir (2s)
  - Sistema de respawn con timer

**Estados de salud:**
- Salud crítica (≤50%) - reproduce sonido especial
- Muerto
- Invulnerable (post-damage)

---

### 4. **Emisión de Ruido (`PlayerNoiseEmitter`)**

**Sistema crítico para sigilo:**
- **Radios por estado**:
  - Idle: 1m
  - Caminar: 3m
  - Agacharse: 2m
  - Correr: 6m
- **VFX visual** que muestra el radio de ruido actual
- **Reflexión dinámica** para acceder a estados de movimiento (MovJugador1/2)
- **Pulso visual** ajustable según velocidad de movimiento

**Uso:** Los enemigos detectan jugadores principalmente por audio/ruido.

---

### 5. **Linterna (`FlashlightController_Enhanced`, `PlayerFlashlightHandler`)**

**Características:**
- **Control de linterna** con Volumetric Light Beam (VLB)
- **Prevención de clipping** - detecta paredes y ajusta posición del brazo
- **Auto-lower arm** - baja el brazo automáticamente cerca de paredes
- **Sincronización con animaciones** del jugador
- **Optimizaciones VLB** - segmentos, noise, intensidad configurables
- **Solo disponible si el jugador tiene el item "Flashlight"**

---

## 👹 SISTEMA DE ENEMIGOS

### Arquitectura Dual: Legacy vs Modular

#### **Sistema Legacy (`EnemyMonsterAI`)**
IA monolítica con todos los sistemas en una clase:
- Estados: Sleeping, Patrol, Chasing, Attacking, Rising, Roaring, Dead, ReturningToCrawl, Investigating
- Detección de jugadores por visión y audio
- Sistema de patrulla con puntos
- Ataque a paredes destructibles
- Sistema de pisadas programático
- Efectos visuales de rugido (shader distortion)

#### **Sistema Nuevo Modular (`EnemyBrain`, `EnemySenses`, `EnemyMotor`, `EnemyVisuals`)**

**Separación de responsabilidades:**

1. **`EnemyBrain`** - Máquina de estados y lógica principal
   - Estados: Sleeping, Eating, Patrol, Investigating, Chasing, Attacking, Transitioning, Dead
   - Orquesta las otras partes del sistema
   - Maneja rutinas de ataque, persecución, investigación

2. **`EnemySenses`** - Sistema de detección
   - **Detección por audio** avanzada:
     - Atenuación por paredes (`soundAttenuationPerWall`)
     - Memoria de posición (persistencia de 3s)
     - Sensibilidad configurable
   - **Detección de paredes** destructibles
   - Retorna `HasTargetOfInterest`, `CurrentPlayer`, `TargetPositionOfInterest`

3. **`EnemyMotor`** - Control de movimiento
   - Wrapper del NavMeshAgent
   - Manejo de rotación manual
   - Estados: IsMoving
   - Métodos: `MoveTo()`, `Stop()`, `RotateTowards()`

4. **`EnemyVisuals`** - Animaciones y efectos
   - Control de animaciones
   - Sistema de pisadas
   - Hitboxes de combate
   - Efectos de rugido (shader)
   - **Sistema de mirada IK** - barre con la cabeza durante investigación

**Recomendación:** El sistema modular es más mantenible. Considerar migrar completamente al nuevo sistema.

---

### Comportamiento del Enemigo

**Flujo típico:**
1. **Sleeping/Eating** → Detecta ruido/jugador → **Rise & Roar** → **Chasing**
2. Durante persecución, si hay pared → **Attack Wall**
3. Si pierde de vista → **Investigating** → si no encuentra → **Return to Patrol**
4. Durante persecución, si está en rango → **Attack Player**

**Características avanzadas:**
- **Destrucción de paredes** - puede atravesar paredes destructibles
- **Sistema de alerta** con niveles de alerta
- **Detección por audio** más sofisticada que por visión
- **Investigation mode** con movimiento de cabeza (IK)

---

## 🧩 SISTEMAS DE PUZZLES E INTERACCIONES

### 1. **Puertas de Doble Acción (`PuertaDobleAccion`)**

**Mecánica cooperativa:**
- Requiere **2 jugadores** presionando simultáneamente (ventana de tiempo: 0.3s)
- Sistema de buffer: si un jugador presiona, espera 0.5s al otro
- **Múltiples golpes necesarios** para abrir (configurable, default: 3)
- Efectos cooperativos:
  - Shake de cámara para ambos jugadores
  - Rumble en gamepad
  - Shader de stress en pantalla completa
  - Sonidos de éxito/error
- **Indicadores visuales** de botones sobre cada jugador
- **Outline multiplayer** - cambia de color según cuántos jugadores están cerca

**Estados:**
- Cerrada (esperando jugadores)
- Esperando segundo jugador
- Ambos listos (pueden golpear)
- Abriéndose

---

### 2. **Keypad Door (`KeypadDoorController`)**

**Mecánica:**
- Interacción individual
- Abre UI de keypad (`KeypadUIManager`)
- Cambia InputMap a "UI" mientras está activo
- Feedback visual con luz (rojo → verde)
- Sistema de outline para multiplayer (aunque solo uno puede usar el keypad)

---

### 3. **Electric Box (`ElectricBox`)**

**Mecánica:**
- Requiere item específico (`requiredItemID`, default: "PalancaParte")
- Al desactivar:
  - Consume el item
  - Desactiva partículas eléctricas
  - Activa palanca visual
  - Anima rotación de palanca
  - Desactiva barrera de puerta (`WarningDoor`)
- Sonidos especiales (2 tipos: normal y linear rolloff)

---

### 4. **Puertas con Llave (`PuertaDobleConLlave`)**

Interacción simple que verifica inventario del jugador.

---

### 5. **Puertas Caídas (`FallenDoor`)**

**Mecánica de levantamiento:**
- Requiere **mantener botón presionado** por tiempo mínimo (0.15s)
- Animación de levantamiento
- Puede cancelarse si el jugador se aleja
- Estados de animación sincronizados con script

---

## 🔊 SISTEMA DE AUDIO

### `AudioManager` (Singleton)

**Características:**
- **Pool de AudioSources** (10 iniciales, crece dinámicamente)
- **Mezcla con AudioMixer** - canales separados: Master, Music, SFX, Ambient, Voice
- **Volúmenes persistentes** en PlayerPrefs
- **Sistema de pisadas** diferenciado por tipo de jugador
- **Fade entre tracks** de música

**Métodos principales:**
- `PlayMusic(AudioClip, fadeDuration)`
- `PlaySFX(AudioClip, position, spatialBlend, volume, pitch)`
- `PlayFootstep(FootstepType, position, volume)`
- `SetMasterVolume()`, `SetMusicVolume()`, `SetSFXVolume()`, etc.

---

### Otros Sistemas de Audio

- **`AmbientSoundManager`** - Sonidos ambientales por zona
- **`ProximityAudioZone_Coop_Advanced`** - Zonas de audio con detección de jugadores
- **`ParticleSoundController`** - Audio sincronizado con partículas
- **`AbilityCooldownSystem`** - Sonidos de habilidades

---

## 🎮 SISTEMA DE UI

### Jugador (`PlayerUIController`, `PlayerStaminaUI`)

**Elementos:**
- **Barra de estamina** - se muestra al correr, se oculta al parar
- **Barra de salud** - world space, aparece al recibir daño
- **Notifications** - mensajes temporales
- **Respawn panel** - aparece tras morir
- **Popup billboard** - mensajes flotantes sobre la cabeza

### Menús

- **`PauseManager`** - Pausa con Time.timeScale
- **`MenuNavigation`**, **`JoystickMenuNavigation`** - Navegación de menús
- **`SceneLoadManager`** - Carga de escenas con fade

---

## 🎯 GESTIÓN DEL JUEGO

### `GameManager` (Singleton)

**Responsabilidades:**
- **Gestión de respawn** de jugadores
- **Tracking de checkpoints** por jugador
- **Coordinación** entre sistemas

**Sistema de respawn:**
- Guarda posición de checkpoint por jugador
- Restaura estado del jugador al respawnear
- Coordina con `PlayerHealth` y scripts de movimiento

---

### `Checkpoint`

**Mecánica:**
- **Activable por ambos jugadores** independientemente
- Se desactiva cuando ambos lo han usado
- Evento estático: `OnCheckpointReached(playerID, position)`
- Feedback visual cuando ambos lo activan

---

## 🎨 SISTEMAS VISUALES

### Outline Multiplayer

**Sistema común en múltiples objetos:**
- Usa MaterialPropertyBlock para modificar shader
- Colores:
  - Negro: inactivo
  - Color del jugador: 1 jugador cerca
  - Amarillo: 2+ jugadores cerca (cooperativo)

### Efectos Shader

- **Roar Effect** - distorsión de pantalla cuando el enemigo ruge
- **Stress Effect** - en puertas de doble acción durante los golpes

### VFX

- **Niebla** - esfera VFX que sigue al jugador
- **Ruido** - VFX visual del radio de ruido
- **Sangre** - partículas al recibir daño

---

## 🔄 PATRONES Y ARQUITECTURA

### Patrones Identificados

1. **Singleton**: `GameManager`, `AudioManager`
2. **Observer/Events**: Checkpoints, diálogos, muerte de jugadores
3. **State Machine**: Enemigos (explicito en `EnemyBrain`)
4. **Component-based**: Separación clara de responsabilidades
5. **Object Pooling**: AudioSources en AudioManager

### Puntos Fuertes

✅ Sistema modular de enemigos (nuevo)  
✅ Sistema de audio robusto y centralizado  
✅ Mecánicas cooperativas bien implementadas  
✅ Sistema de sigilo con emisión de ruido  
✅ Feedback visual y auditivo extenso  

### Áreas de Mejora

⚠️ **Duplicación de código** entre MovJugador1 y MovJugador2  
⚠️ **Sistema dual de enemigos** - legacy y nuevo coexistiendo  
⚠️ **Algunos nombres inconsistentes** (Puzzle_01 vs Puzle_02)  
⚠️ **Uso extensivo de reflexión** en `PlayerNoiseEmitter` (podría optimizarse)  
⚠️ **Manejo de Input** - hay múltiples formas de manejar input (InputActionReference, InputValue, eventos directos)  

---

## 📊 FLUJOS PRINCIPALES

### Flujo de Juego Principal

1. Jugadores spawn en checkpoints
2. Movimiento y exploración
3. Encuentro con puzzles/interacciones
4. Encuentro con enemigo (detección por ruido/visión)
5. Persecución → escape o muerte
6. Respawn en checkpoint
7. Repetición hasta completar nivel

### Flujo de Detección de Enemigo

1. Enemigo en estado pasivo (Sleeping/Eating/Patrol)
2. Jugador emite ruido dentro del radio de detección
3. Enemigo detecta por audio (o visión si está despierto)
4. Transición: Rise & Roar
5. Estado Chasing - persigue al jugador
6. Si hay pared → Attack Wall
7. Si está en rango → Attack Player
8. Si pierde de vista → Investigating → Return to Patrol

---

## 🛠️ TECNOLOGÍAS Y DEPENDENCIAS

- **Unity Input System** - Nuevo sistema de input
- **NavMesh** - Pathfinding de enemigos
- **Volumetric Light Beam (VLB)** - Efectos de linterna
- **VFX Graph** - Efectos visuales (niebla, sangre)
- **Animator** - Animaciones de personajes
- **Unity Shader Graph** - Efectos visuales (outline, roar, stress)

---

## 💡 RECOMENDACIONES

### Corto Plazo

1. **Unificar sistema de jugadores** - Hacer que MovJugador1 y MovJugador2 hereden completamente de PlayerControllerBase
2. **Migrar completamente al sistema modular de enemigos**
3. **Estandarizar nombres** (Puzzle vs Puzle)

### Mediano Plazo

1. **Sistema de diálogos más robusto** - Ya existe `NPCEnhancedDialogueSystem`, integrar mejor
2. **Sistema de guardado** - No se encontró sistema de guardado de progreso
3. **Optimización** - Revisar uso de reflexión en tiempo de ejecución

### Largo Plazo

1. **Sistema de logros/logros cooperativos**
2. **Replay system** para debugging
3. **Analytics** para balanceo

---

## 📝 NOTAS ADICIONALES

- El juego tiene un enfoque muy fuerte en **cooperación local**
- Sistema de **sigilo bien implementado** con emisión de ruido
- **Feedback háptico y visual** muy presente
- **Variedad de puzzles** bien integrados con la mecánica principal

---

*Análisis generado el: $(date)*
*Unity Version: 2022.3.5f1*
*Total de scripts analizados: ~50+ scripts principales*

