# 🎮 GUÍA DE CONFIGURACIÓN EN UNITY - NPCs Y OBJETOS

## 📋 PASO A PASO COMPLETO

### PARTE 1: Configurar NPCs con Sistema de Salud

#### 1. Preparar el Prefab del NPC

1. Selecciona tu GameObject del NPC en la escena
2. Agrega los siguientes componentes (en este orden):

```
NPC (GameObject)
├─ NPCIdentifier
├─ NPCHealth  
├─ NPCNoiseEmitter
├─ NPCBehaviorManager (ya existente)
├─ NavMeshAgent (ya existente)
└─ Animator (ya existente)
```

#### 2. Configurar NPCIdentifier

- **NPC ID**: 1 (o el número que quieras)
- **NPC Outline Color**: Color verde (o el que prefieras)
- Los campos de referencias se llenan automáticamente

#### 3. Configurar NPCHealth

**Health Settings:**
- **Max Health**: 100 (ajusta según necesites)
- **Invulnerability Duration**: 0.5

**UI World Space Settings:**
- **Health Canvas Group**: 
  1. Crea un Canvas hijo del NPC
  2. Configúralo como "World Space"
  3. Asigna el CanvasGroup aquí
  4. Agrega un Slider y TextMeshProUGUI como hijos del Canvas
  5. Configura el Slider: Min = 0, Max = 100
  6. El TextMeshProUGUI mostrará "X / Y"

- **Fade Duration**: 0.3
- **Display Time After Damage**: 3.0

**UI References:**
- **Health Slider**: Arrastra el Slider del Canvas
- **Health Text**: Arrastra el TextMeshProUGUI del Canvas

**Audio Settings (Opcional):**
- Asigna clips de audio: damageSound, deathSound, criticalPainSound

**Efectos Visuales (Opcional):**
- **Blood Particles Prefab**: VFX de sangre

#### 4. Configurar NPCNoiseEmitter

**Radios de ruido:**
- **Idle Noise Radius**: 1
- **Walk Noise Radius**: 3
- **Run Noise Radius**: 5
- **Crouch Noise Radius**: 2

**Thresholds:**
- **Walk Speed Threshold**: 0.5
- **Run Speed Threshold**: 4.0

**Visual Feedback (Opcional):**
- **Noise VFX**: VisualEffect para mostrar el radio visualmente

#### 5. Configurar Animator del NPC

Abre el Animator Controller del NPC:

**Parámetros a agregar:**
1. Click en "+" → **Trigger** → nombre: `Hit`
2. Click en "+" → **Bool** → nombre: `IsDead`

**Estados a crear (si no existen):**
- Estado "Hit" (animación de recibir daño)
- Estado "Dead" (animación de muerte)

**Transiciones:**

```
Any State → Hit:
  - Condición: Hit (Trigger)
  - Has Exit Time: ❌
  - Transition Duration: 0.1

Hit → Idle (o estado anterior):
  - Sin condición (solo Exit Time)
  - Has Exit Time: ✅
  - Transition Duration: 0.2

Any State → Dead:
  - Condición: IsDead (Bool) = true
  - Has Exit Time: ❌
  - Transition Duration: 0.2
  - ⚠️ NO crear transición de vuelta desde Dead
```

Ver `GUIA_ANIMATOR_NPC.md` para más detalles.

---

### PARTE 2: Configurar Objetos Grabbables con Ruido

#### 1. Preparar Objetos Grabbables

Para cada objeto que quieras que emita ruido:

1. Selecciona el GameObject del objeto
2. Debe tener ya:
   - `GrabbableObjectController`
   - `Rigidbody`
   - `Collider`
   - `AudioSource`

3. Agrega el componente:
   ```
   ObjectNoiseEmitter
   ```

#### 2. Configurar ObjectNoiseEmitter

**Radios de ruido:**
- **Idle Noise Radius**: 0 (no emite ruido cuando está quieto)
- **Moving Noise Radius**: 4 (ruido al moverse lentamente)
- **Fast Moving Noise Radius**: 8 (ruido al moverse rápido)
- **Collision Noise Radius**: 10 (ruido cuando choca)
- **Collision Noise Duration**: 2 (duración del ruido de colisión)

**Thresholds:**
- **Moving Speed Threshold**: 0.5
- **Fast Moving Speed Threshold**: 3.0

**Nota:** El `GrabbableObjectController` ya está modificado para activar automáticamente el ruido cuando el objeto choca.

---

### PARTE 3: Configurar el Enemigo para Detectar NPCs y Objetos

#### Opción A: Sistema Nuevo (EnemyBrain + EnemySenses)

1. Selecciona el GameObject del Enemigo

2. En el componente **EnemySenses**:
   - En el array `npcTargets[]`:
     - Tamaño: Número de NPCs en la escena
     - Arrastra los NPCs a cada slot
   
   - Agrega el componente **ObjectNoiseDetection** al enemigo
   - En EnemySenses, asigna `ObjectNoiseDetection` a la referencia `objectNoiseDetection`

3. En el componente **ObjectNoiseDetection**:
   - **Auto Find Objects**: ✅ (buscará automáticamente todos los objetos)
   - O asigna manualmente en `objectNoiseTargets[]`
   - **Max Detection Distance**: 20
   - **Detection Threshold**: 2

#### Opción B: Sistema Legacy (EnemyMonsterAI)

1. Selecciona el GameObject del Enemigo

2. En el componente **EnemyMonsterAI**:
   - En el array `npcTargets[]`:
     - Tamaño: Número de NPCs en la escena
     - Arrastra los NPCs a cada slot

**Nota:** El sistema legacy detecta NPCs pero no tiene soporte directo para objetos ruidosos. Considera migrar al sistema nuevo para esa funcionalidad.

---

## 🎯 VERIFICACIÓN EN PLAY MODE

### Verificar NPCs:

1. **Ruido del NPC:**
   - Selecciona el NPC
   - Observa el gizmo (esfera naranja) que muestra el radio de ruido
   - Debe cambiar según la velocidad del NPC

2. **Salud del NPC:**
   - Haz que el enemigo ataque al NPC
   - Debe aparecer la barra de vida con fade in
   - Debe desaparecer después de 3 segundos con fade out

3. **Muerte del NPC:**
   - Cuando el NPC muera:
     - La animación "Dead" debe activarse
     - El NPC debe dejar de moverse
     - El enemigo debe dejar de perseguirlo

### Verificar Objetos:

1. **Ruido de Objetos:**
   - Lanza o mueve un objeto con `ObjectNoiseEmitter`
   - Observa el gizmo (esfera rosa) que muestra el radio
   - Al chocar, el radio debe aumentar temporalmente

2. **Detección por Enemigo:**
   - El enemigo debe dirigirse a investigar objetos ruidosos
   - No debe atacar el objeto, solo investigar

---

## 🔧 TROUBLESHOOTING

### NPC no emite ruido:
- ✅ Verifica que `NPCNoiseEmitter` esté agregado
- ✅ Verifica que el NavMeshAgent esté configurado
- ✅ Verifica que el NPC no esté muerto

### NPC no muestra barra de vida:
- ✅ Verifica que el Canvas esté configurado como "World Space"
- ✅ Verifica que el CanvasGroup esté asignado
- ✅ Verifica que el Slider y Text estén asignados
- ✅ Verifica que el Canvas esté visible en la jerarquía

### Enemigo no detecta NPCs:
- ✅ Verifica que los NPCs estén en el array `npcTargets[]`
- ✅ Verifica que el NPC tenga `NPCNoiseEmitter`
- ✅ Verifica que el NPC no esté muerto
- ✅ Verifica el `audioDetectionRadius` del enemigo

### Enemigo no detecta objetos:
- ✅ Verifica que `ObjectNoiseDetection` esté agregado al enemigo
- ✅ Verifica que esté asignado en `EnemySenses.objectNoiseDetection`
- ✅ Verifica que los objetos tengan `ObjectNoiseEmitter`
- ✅ Verifica que `autoFindObjects` esté activado o que los objetos estén asignados manualmente

### Animator no funciona:
- ✅ Verifica que los parámetros `Hit` y `IsDead` existan
- ✅ Verifica que las transiciones estén configuradas correctamente
- ✅ Verifica que las animaciones existan y estén asignadas a los estados

---

## 📊 RESUMEN DE CONFIGURACIÓN

### NPCs:
✅ NPCIdentifier  
✅ NPCHealth (con Canvas World Space)  
✅ NPCNoiseEmitter  
✅ Animator con parámetros Hit e IsDead  

### Objetos:
✅ ObjectNoiseEmitter en objetos grabbables  

### Enemigo:
✅ NPCs en array `npcTargets[]`  
✅ ObjectNoiseDetection agregado y asignado  

---

¡Listo! El sistema debería estar funcionando completamente. 🎉

