# Journal · Semana 10 — El arte de la iluminación

Rama: `feature/Visual-Atmosphere` · Escena: `Assets/Scenes/SampleScene.unity`

## Antes y después (mismo encuadre)

Capturas generadas con **Echoes > Capturas antes y después (S10)**: la cámara se coloca siempre en el mismo punto, detrás del inicio del camino y mirando hacia delante.

| Antes: sin luces, Volume desactivado | Después: tres capas, cámara y Volume |
|---|---|
| ![Antes](Capturas/S10_antes.png) | ![Después](Capturas/S10_despues.png) |

En el "antes" el camino no se distingue del resto del bosque. En el "después" es lo más iluminado del nivel: una línea de farolillos cálidos lo marca hasta el horizonte.

## Checklist de narrativa visual

- [x] **Tres capas de luz agrupadas en `-- LIGHTING --`**
  - **Key Light**: luz direccional de luna, fría (azul), intensidad 0,35 y sombras suaves. Recorta las siluetas del relieve sin competir con el camino.
  - **Fill Light**: luz direccional violácea muy tenue (0,12) y sin sombras, desde el lado opuesto, más el ambiente Trilight y la niebla azul. Fuera del camino se ve poco, pero nunca negro.
  - **Accent Light**: 29 farolillos cálidos a lo largo del camino, cada 24 m y alternando lados, más el **Goal Light**, un foco cian de intensidad 400 al final del camino con su baliza.
- [x] **El camino crítico es la zona más iluminada**: la luna está baja a propósito para que los farolillos ganen el contraste. El cálido sobre frío dirige la mirada.
  - El camino mide 7,5 m de ancho (3 m × el `playerScale` 2,5 del Player), está despejado de árboles y hierba, y los farolillos quedan en sus bordes.
- [x] **CinemachineCamera siguiendo al Player, con Dead Zone y Damping**
  - `-- CAMERAS -- / CM_PlayerCam` sigue al `CameraPivot` con un **Position Composer**: Dead Zone de 0,06 × 0,06 y Damping de (0,35, 0,25, 0,35).
  - La rotación usa *Rotate With Follow Target* sin retraso, para que el ratón responda al instante.
  - Al tener `CinemachineBrain` en la `PlayerCamera`, el FOV de sprint pasa a la cámara virtual a través de `PlayerCameraLens`.
- [x] **Segunda cámara activada por Trigger en una zona de tensión**
  - `Zona de Tensión (Trigger)` está al 65 % del recorrido. Al entrar, `CameraZoneTrigger` sube la prioridad de `CM_TensionCam`.
  - Esa cámara cambia la sensación: FOV 52, horizonte inclinado 8°, cámara en mano (ruido *Handheld_normal_strong*) y más damping.
  - Los farolillos de esa zona son rojos y más débiles.
  - Un Volume local cierra la viñeta y desatura el color.
- [x] **Global Volume con Tonemapping, Bloom, Vignette y Color Adjustments** (`Assets/Echoes/Lighting/Echoes_Atmosphere.asset`):
  - Tonemapping en ACES.
  - Bloom con intensidad 0,9, umbral 1 y tinte cálido.
  - Vignette de 0,32.
  - Color Adjustments: exposición +0,8, contraste +15 y saturación −5.

## ¿Qué emoción buscamos y con qué parámetros?

**Inquietud con esperanza**: un bosque nocturno que da un poco de miedo, pero con un camino que te ofrece refugio.

- **Miedo / soledad**: luna fría y baja, ambiente casi negro, niebla azul y viñeta.
- **Esperanza / guía**: farolillos cálidos (color 1, 0,68, 0,35) con bloom. Es el único color cálido del nivel.
- **Tensión**: en la zona de tensión los farolillos se vuelven rojos, la cámara se inclina y tiembla, y el Volume local quita saturación.
- **Meta**: la única luz cian del nivel, para que se lea como "llegada" y no como "peligro".

## Playtesting mudo

> Pendiente de rellenar en clase, con un tester de otro equipo.

| Pregunta | Resultado | Ajuste hecho |
|---|---|---|
| ¿Sabe a dónde ir? | _ | _ (p. ej. subir la intensidad del Goal Light) |
| ¿La cámara atraviesa paredes? | _ | _ (añadir Cinemachine Deoccluder si pasa) |
| ¿Se entiende el tono? | _ | _ |

**¿Dónde se perdió el tester y qué luz cambiamos por eso?** _Por rellenar tras la prueba._

## ¿Por qué el Tonemapping es obligatorio en URP?

Con HDR activo, la escena se calcula con valores de luz mayores que 1: el centro de un farolillo, la emisión de las bombillas o el foco del objetivo. La pantalla solo muestra de 0 a 1. Sin tonemapping, todo lo que pasa de 1 se recorta y queda blanco plano, los colores se "queman" y el bloom no tiene de dónde sacar brillo. El tonemapping (aquí ACES) comprime ese rango alto a la pantalla de forma suave, conserva el detalle en las luces y mantiene el color de los farolillos. Además, el resto del post-procesado (Bloom, Color Adjustments) está pensado para trabajar antes de esa compresión, así que sin tonemapping la imagen final no se corresponde con lo que ajustas.

## Dev Kata S10 · La Habitación Emocional

Escena: `Assets/Echoes/Scenes/Kata_HabitacionEmocional.unity`. Es un cubo en una habitación, y la geometría no cambia nunca. En Play, las teclas **1 / 2 / 3** (o los botones) cambian de perfil.

| Terror | Calma | Acción |
|---|---|---|
| ![Terror](Capturas/Kata_1_Terror.png) | ![Calma](Capturas/Kata_2_Calma.png) | ![Acción](Capturas/Kata_3_Acción.png) |

- **Terror**:
  - Luz fría desde abajo, que deforma la cara del objeto, y contraluz roja.
  - Ambiente casi negro.
  - Volume: exposición −0,4, contraste +45, saturación −60, viñeta 0,55 y grano.
- **Calma**:
  - Luz cálida amplia desde arriba, como una ventana, con relleno azul suave.
  - Ambiente alto.
  - Volume: contraste −12 y viñeta ligera.
- **Acción**:
  - Dos luces duras de colores opuestos, naranja y cian.
  - Volume: contraste +40, saturación +35, bloom fuerte y aberración cromática.

## Reflexión: ¿cómo cambió la iluminación la ruta que toman los jugadores en mi nivel?

_Borrador, por completar con lo que observes en el playtesting._

Antes de iluminar, el terreno era una masa uniforme: nada indicaba hacia dónde ir y el jugador podía vagar por cualquier ladera. Ahora la ruta se lee sin palabras. La luz de la luna oscurece todo menos el camino, los farolillos crean un ritmo que invita a avanzar de uno a otro, y el foco cian del final se ve desde lejos como meta. La zona de tensión aprovecha el mismo lenguaje al revés: cuando la luz se vuelve roja y la cámara tiembla, el jugador entiende que pasa algo sin que nadie se lo diga.

## Cómo regenerarlo

- **Echoes > Crear camino**: traza o retoca el camino en el terreno.
- **Echoes > Iluminación del camino (S10)**: vuelve a montar las luces, las cámaras, la zona de tensión y el Volume a partir del camino.
- **Echoes > Capturas antes y después (S10)**: rehace las dos capturas.
- **Echoes > Kata S10: Habitación emocional**: rehace la escena de la kata y sus capturas.
