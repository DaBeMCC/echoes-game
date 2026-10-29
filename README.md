# Echoes

Juego en primera persona hecho en **Unity 6** (Universal Render Pipeline) con **C#**. **En desarrollo:** por ahora incluye un terreno, el controlador del jugador y herramientas de editor para construir el escenario.

<!-- Agrega aquí una captura: ![Echoes](docs/captura.png) -->

## Qué incluye

- **Controlador en primera persona propio** (`PlayerController.cs`):
  - caminar, correr con cambio de campo de visión, agacharse y saltar;
  - control limitado en el aire y balanceo de cámara al caminar;
  - soporte para teclado, ratón y mando.
- **Audio del jugador** (`PlayerAudio.cs`): pasos que siguen la velocidad de movimiento, sonido ambiente, música y rugido de dinosaurio.
- **Herramientas de editor** (menú **Echoes**):
  - **Crear Player en la escena:** construye el jugador, lo guarda como prefab y lo coloca sobre el terreno.
  - **Crear camino:** traza un camino de tierra sobre el terreno siguiendo puntos de control, suaviza el relieve y quita la hierba encima.

## Controles

| Acción | Teclado y ratón | Mando |
|---|---|---|
| Moverse | `W` `A` `S` `D` | Stick izquierdo |
| Mirar | Ratón | Stick derecho |
| Correr | `Shift` izquierdo | Pulsar stick izquierdo |
| Agacharse | `Ctrl` izquierdo o `C` | Botón este |
| Saltar | `Espacio` | Botón sur |
| Liberar o capturar el cursor | `Esc` / clic izquierdo | — |

## Cómo ejecutarlo

1. Instala **Unity 6000.6.3f1** (o una versión 6000.6 compatible) desde Unity Hub.
2. Clona el repositorio:
   ```bash
   git clone https://github.com/DaBeMCC/echoes-game.git
   ```
3. Abre la carpeta en Unity Hub y carga `Assets/Scenes/SampleScene.unity`.
4. Pulsa **Play**.

## Estructura

```
Assets/Echoes/
├── Scripts/Player/   Controlador y audio del jugador
├── Editor/           Herramientas para crear el jugador y el camino
└── Prefabs/          Prefab del jugador
```

## Créditos

Desarrollado por **David Machaca**.

Paquetes de terceros incluidos en el proyecto:

- **AllSky Free:** cielos.
- **Polytope Studio – Lowpoly Environments:** entorno de baja poligonización.
- **Modular First Person Controller:** paquete incluido en el proyecto; el jugador de `Assets/Echoes` usa su propio controlador.

<!-- Revisa la licencia de cada paquete y de los audios en Assets/Audio antes de publicar. -->
