# 🎬 GUÍA PARA CONFIGURAR EL ANIMATOR DEL NPC

## 📋 Parámetros Necesarios en el Animator

El NPC necesita los siguientes parámetros para funcionar correctamente con `NPCHealth`:

### Parámetros Requeridos:

1. **Trigger: "Hit"**
   - Tipo: Trigger
   - Uso: Se activa cuando el NPC recibe daño
   - Activado por: `npcAnimator.SetTrigger("Hit")` en `NPCHealth.ApplyDamageEffects()`

2. **Bool: "IsDead"**
   - Tipo: Bool
   - Uso: Indica si el NPC está muerto
   - Activado por: `npcAnimator.SetBool("IsDead", true)` en `NPCHealth.Die()`

### Parámetros Existentes (del NPCBehaviorManager):

Estos ya deberían estar configurados para el sistema de seguimiento:

3. **Float: "Speed"**
   - Tipo: Float
   - Uso: Controla la velocidad de animación (0 = idle, 1 = walk, 2 = run)
   - Controlado por: `NPCBehaviorManager.UpdateAnimation()`

4. **Bool: "IsCrouching"**
   - Tipo: Bool
   - Uso: Indica si el NPC está agachado
   - Controlado por: `NPCBehaviorManager.UpdateAnimation()`

5. **Bool: "IsRunning"**
   - Tipo: Bool
   - Uso: Indica si el NPC está corriendo
   - Controlado por: `NPCBehaviorManager.UpdateAnimation()`

6. **Bool: "IsFollowing"**
   - Tipo: Bool
   - Uso: Indica si el NPC está siguiendo a un jugador
   - Controlado por: `NPCBehaviorManager.UpdateAnimation()`

## 🎭 Estados y Transiciones Recomendados

### Estructura de Estados Base:

```
[Any State]
  ├─> Idle (Default)
  ├─> Walk
  ├─> Run
  ├─> Crouch
  ├─> Hit (Trigger)
  └─> Dead (Bool)
```

### Transiciones Específicas:

1. **Hit (Trigger)**:
   - Desde: Any State (excepto Dead)
   - Hacia: Estado "Hit" o "TakeDamage"
   - Condición: `Hit` trigger
   - Exit Time: false
   - Transition Duration: 0.1s
   - **Importante**: Después del estado Hit, debe volver al estado anterior o a Idle

2. **Dead (Bool)**:
   - Desde: Any State
   - Hacia: Estado "Dead" o "Death"
   - Condición: `IsDead` = true
   - Exit Time: false
   - Transition Duration: 0.2s
   - **Importante**: No debe haber transición de vuelta desde Dead

3. **Speed (Float)**:
   - Idle → Walk: `Speed > 0.1` y `Speed < 1.5`
   - Walk → Run: `Speed >= 1.5`
   - Run → Walk: `Speed < 1.5` y `Speed > 0.1`
   - Walk/Run → Idle: `Speed <= 0.1`

4. **IsCrouching (Bool)**:
   - Any State → Crouch: `IsCrouching = true` (excepto Dead)
   - Crouch → Idle/Walk: `IsCrouching = false`

5. **IsRunning (Bool)**:
   - Se usa junto con Speed para diferenciar walk/run

## 🔧 Configuración Paso a Paso

### 1. Crear los Parámetros:

1. Abre el Animator Controller del NPC
2. Ve a la pestaña "Parameters"
3. Haz clic en el "+" y agrega:
   - **Trigger** llamado "Hit"
   - **Bool** llamado "IsDead"

### 2. Crear Estados de Animación:

1. Si no existe, crea estados para:
   - **Hit** (animación de recibir daño)
   - **Dead** (animación de muerte)

### 3. Configurar Transiciones:

#### Transición para Hit:
```
[Any State] → [Hit]
- Condición: Hit (Trigger)
- Has Exit Time: false
- Transition Duration: 0.1
- Interruption Source: None

[Hit] → [Idle] (o estado anterior)
- Sin condición (Exit Time: true)
- Transition Duration: 0.2
```

#### Transición para Dead:
```
[Any State] → [Dead]
- Condición: IsDead (Bool) = true
- Has Exit Time: false
- Transition Duration: 0.2
- Interruption Source: None
```

#### Estados con Exit Time deshabilitado:
Para los estados de movimiento (Idle, Walk, Run, Crouch), asegúrate de que las transiciones entre ellos tengan:
- **Has Exit Time: false** (para transiciones inmediatas)
- **Transition Duration: 0.15-0.25** (para suavidad)

### 4. Configurar Blend Tree (si usas Speed):

Si tienes un Blend Tree para caminar/correr basado en Speed:

1. Crea un Blend Tree llamado "Movement"
2. Agrega animaciones:
   - Idle (Speed = 0)
   - Walk (Speed = 1)
   - Run (Speed = 2)
3. Parámetro: "Speed"
4. Thresholds: 0, 1, 2

## ⚠️ Notas Importantes

1. **Dead State es Absoluto**: Una vez que el NPC está muerto (`IsDead = true`), NO debe haber transiciones que lo saquen del estado Dead.

2. **Hit no interrumpe Dead**: El estado Hit no debería activarse si el NPC ya está muerto. El código ya lo previene, pero asegúrate de que la animación también lo respete.

3. **Sincronización**: Los estados de movimiento (Walk/Run) deben estar sincronizados con el `NPCBehaviorManager` que controla la velocidad del NavMeshAgent.

4. **Exit Time para Hit**: El estado Hit puede usar Exit Time para volver automáticamente al estado anterior, o puedes usar un Animation Event.

## 🎯 Ejemplo de Estructura Completa:

```
Animator Controller: NPC_Controller
│
├─ Parameters:
│  ├─ Speed (Float)
│  ├─ IsCrouching (Bool)
│  ├─ IsRunning (Bool)
│  ├─ IsFollowing (Bool)
│  ├─ Hit (Trigger) ⭐ NUEVO
│  └─ IsDead (Bool) ⭐ NUEVO
│
├─ States:
│  ├─ Idle (Default)
│  ├─ Walk
│  ├─ Run
│  ├─ Crouch
│  ├─ Hit ⭐ NUEVO
│  └─ Dead ⭐ NUEVO
│
└─ Transitions:
   ├─ Any State → Hit: [Hit trigger]
   ├─ Hit → Idle: [Exit Time]
   ├─ Any State → Dead: [IsDead = true]
   └─ (Ninguna transición desde Dead)
```

## 🔍 Verificación

Para verificar que está funcionando correctamente:

1. En Play Mode, selecciona el NPC
2. Ve al Animator Window
3. Observa los parámetros mientras:
   - El NPC recibe daño → "Hit" debe activarse brevemente
   - El NPC muere → "IsDead" debe cambiar a true y mantenerse
   - El NPC se mueve → "Speed" debe cambiar según la velocidad

## 📝 Animaciones Recomendadas

- **Hit**: Animación corta de reacción al daño (0.3-0.5 segundos)
- **Dead**: Animación de muerte (puede ser un loop o una animación final)
- Asegúrate de que las animaciones tengan la misma velocidad y escala que el modelo

