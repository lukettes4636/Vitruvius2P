# 📋 IMPLEMENTACIÓN DEL SISTEMA DE NPCs CON VIDA Y DETECCIÓN

## ✅ Componentes Creados

### 1. **NPCIdentifier.cs**
- Componente identificador similar a `PlayerIdentifier`
- Almacena ID único del NPC
- Color de outline para interacciones
- Referencias a `NPCHealth` y `NPCNoiseEmitter`

### 2. **NPCHealth.cs**
- Sistema de salud completo similar a `PlayerHealth`
- **Canvas World Space** con `CanvasGroup` para fade
- **Slider de vida** y texto
- **Fade in/out** automático al recibir daño (3 segundos de visualización)
- Efectos visuales: partículas de sangre
- Sonidos: daño, muerte, dolor crítico
- Animaciones: trigger "Hit", bool "IsDead"
- Sistema de inmunidad temporal (0.5s)
- Al morir: desactiva comportamiento y NavMeshAgent

### 3. **NPCNoiseEmitter.cs**
- Sistema de emisión de ruido basado en velocidad del NavMeshAgent
- Radios configurables: idle (1m), walk (3m), run (5m), crouch (2m)
- VFX visual del radio de ruido (igual que jugadores)
- Thresholds de velocidad para diferenciar estados
- Se desactiva automáticamente si el NPC está muerto

## 🔧 Modificaciones a Sistemas Existentes

### EnemySenses (Sistema Nuevo Modular)
✅ Agregado array `npcTargets[]` para detectar NPCs  
✅ Propiedad `CurrentNPCTarget` para tracking  
✅ Propiedad `CurrentTarget` que retorna jugador o NPC (prioridad: jugador)  
✅ Detección por audio de NPCs con `NPCNoiseEmitter`  
✅ Verifica `NPCHealth.IsDead` antes de detectar  

### EnemyBrain (Sistema Nuevo Modular)
✅ `HandleChasing()` verifica muerte de NPCs además de jugadores  
✅ `AttackTargetRoutine()` renombrado (antes `AttackPlayerRoutine`)  
✅ Maneja tanto jugadores como NPCs de forma transparente  

### EnemyMonsterAI (Sistema Legacy)
✅ Agregado array `npcTargets[]`  
✅ `DetectPlayers()` ahora detecta NPCs también  
✅ Verifica `NPCNoiseEmitter.currentNoiseRadius` para detección por audio  
✅ Verifica `NPCHealth.IsDead` antes de detectar  

### DamageDealer
✅ Detecta y daña NPCs además de jugadores  
✅ Verifica `NPCHealth.IsDead` antes de aplicar daño  
✅ Aplica el mismo daño configurado a NPCs  

## 📝 Configuración Necesaria en Unity

### Para cada NPC:

1. **Agregar Componentes:**
   - `NPCIdentifier` (RequireComponent de NPCHealth)
   - `NPCHealth`
   - `NPCNoiseEmitter` (RequireComponent de NavMeshAgent y NPCHealth)

2. **NPCHealth - Configurar:**
   - `maxHealth`: Vida máxima (default: 100)
   - `healthCanvasGroup`: CanvasGroup del UI world space
   - `healthSlider`: Slider de la barra de vida
   - `healthText`: TextMeshProUGUI para mostrar "X / Y"
   - Audio clips: damageSound, deathSound, criticalPainSound
   - `bloodParticlesPrefab`: VFX de sangre
   - `chestImpactPoint`: Transform donde aparecen las partículas (se crea automáticamente si no existe)

3. **NPCNoiseEmitter - Configurar:**
   - `noiseVFX`: VisualEffect que muestra el radio (opcional)
   - Radios: idleNoiseRadius, walkNoiseRadius, runNoiseRadius, crouchNoiseRadius
   - Thresholds: walkSpeedThreshold, runSpeedThreshold

4. **NPCIdentifier - Configurar:**
   - `npcID`: ID único
   - `npcOutlineColor`: Color para outlines interactivos

### Para el Enemigo:

1. **EnemySenses (Sistema Nuevo):**
   - Agregar NPCs al array `npcTargets[]`

2. **EnemyMonsterAI (Sistema Legacy):**
   - Agregar NPCs al array `npcTargets[]`

3. **DamageDealer:**
   - No requiere configuración adicional

## 🎮 Funcionamiento

### Flujo de Detección:
1. NPC emite ruido según su velocidad (`NPCNoiseEmitter`)
2. Enemigo detecta el ruido dentro de su `audioDetectionRadius`
3. Enemigo persigue al NPC igual que a un jugador
4. Al estar en rango de ataque, el enemigo ataca
5. `DamageDealer` aplica daño al `NPCHealth`
6. NPC muestra barra de vida con fade
7. Si vida llega a 0, NPC muere:
   - Animación de muerte
   - Se desactiva comportamiento
   - Se desactiva NavMeshAgent
   - Enemigo deja de perseguirlo

### Características:
- ✅ NPCs pueden ser detectados por ruido igual que jugadores
- ✅ NPCs pueden ser atacados y muertos
- ✅ Barra de vida con fade igual que jugadores
- ✅ Sistema funciona tanto con sistema nuevo (modular) como legacy
- ✅ Compatible con el sistema de diálogos existente

## 🔍 Verificación de Profesionalidad

### ✅ Código Limpio:
- Componentes bien separados
- RequireComponent para dependencias
- Documentación XML completa
- Nombres descriptivos

### ✅ Integración:
- Compatible con sistemas existentes
- No rompe funcionalidad existente
- Usa patrones similares a PlayerHealth/PlayerNoiseEmitter

### ✅ Performance:
- Verificaciones de null apropiadas
- No hay búsquedas costosas en Update
- Uso eficiente de eventos

### ✅ Mantenibilidad:
- Código modular y reutilizable
- Fácil de extender
- Consistente con arquitectura existente

## 🚨 Notas Importantes

1. **Canvas World Space**: El NPC necesita un Canvas configurado como World Space con:
   - CanvasGroup para fade
   - Slider para barra de vida
   - TextMeshProUGUI para texto

2. **Animator**: El NPC debe tener parámetros:
   - Trigger "Hit"
   - Bool "IsDead"

3. **VFX**: El `noiseVFX` es opcional, pero recomendado para debug visual

4. **Sistema Dual**: El juego tiene dos sistemas de enemigos (legacy y modular). Ambos fueron modificados para soportar NPCs.

