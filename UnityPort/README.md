# JardinUnity: migración del juego a Universal 2D

Proyecto para **Unity 6000.6.8f1**, la versión instalada en este equipo. Parte de la plantilla **Universal 2D**, conserva su Renderer 2D de URP y utiliza C# y el nuevo Input System. El programa C++ original permanece en la carpeta superior.

## Abrir y jugar

1. En Unity Hub, usa **Add / Add project from disk** y selecciona esta carpeta `UnityPort`.
2. Ábrela con Unity **6000.6.8f1**. Deja que termine de importar y compilar.
3. Abre [Assets/_Game/Scenes/Game.unity](Assets/_Game/Scenes/Game.unity).
4. Pulsa **Play**. La cámara, el patio y la interfaz se ajustan a la resolución de Game View.

La escena contiene el punto de entrada `GameRoot` y el asset `GameArt` ya conectado. Sus vistas crean los objetos de la partida al comenzar; no necesitas arrastrar decenas de plantas ni configurar manualmente los fotogramas.

El ejecutable compilado se encuentra en `Build/Windows/Jardin.exe`. Para compartirlo, copia **toda** la carpeta `Build/Windows`, no solamente el archivo EXE.

## Controles y reglas conservadas

- Clic en tarjeta: compra girasol por 50 o lanzaguisantes por 100. El cobro ocurre al seleccionar.
- Clic en celda vacía: coloca la compra; una celda ocupada conserva la selección.
- Con una planta pendiente, pulsar otra tarjeta cancela primero; otro clic puede comprarla. Cancelar no reembolsa.
- Pala al final: gratis, retira una planta sin reembolso; si la celda está vacía sigue activa.
- Clic en un sol: suma 50 y consume ese clic, conservando la herramienta.
- Girasoles: producen cada 15 s, soles caducan a los 10 s, máximo 3 por celda sin acumulación de generaciones.
- Lanzaguisantes: un proyectil por planta, solo ante un zombi vivo en su fila; espera 2 s desde la desaparición del proyectil.
- Zombis: cuatro impactos, destello, caída y mordiscos cada 1.1 s; tres mordiscos retiran una planta.
- `X`: crea una tanda de zombis, cada uno en una fila aleatoria. `+`/`-` ajustan su cantidad, mínimo 1, incluido teclado numérico.
- `F`: alterna pantalla completa y ventana.
- `Esc`: cierra el juego; en el Editor detiene Play.
- `GAME OVER`: Retry restaura mundo, saldo 1000 y dificultad; conserva la cantidad de X. Salir termina el juego.

Con producción cada 15 s y vida del sol de 10 s, un girasol normalmente no tendrá tres soles simultáneos. El límite sigue existiendo como protección por celda.

## Estructura del código

| Carpeta | Responsabilidad |
|---|---|
| `Assets/_Game/Scripts/Model` | Simulación C# sin dependencias de Unity: geometría, economía, entrada lógica, disparos y enemigos. |
| `Assets/_Game/Scripts/Game` | Ciclo Unity, lectura Input System y referencias compartidas. |
| `Assets/_Game/Scripts/Presentation` | SpriteRenderer, suelo por celdas y HUD nativo uGUI/TMP. |
| `Assets/_Game/Shaders` | Material URP del césped y destello individual de zombis que conserva alfa. |
| `Assets/_Game/Editor` | Importación, recursos, prefabs, escena y compilación reproducible. |
| `Assets/_Game/Art` | Imágenes copiadas del original; `Prepared` contiene recortes de transparencia generados. |
| `Assets/_Game/Data/GameArt.asset` | Conexiones editables de imágenes, fuentes, materiales y prefabs. |
| `Tests` | Pruebas del modelo fuera de Unity. |
| `Validation` | Informes y capturas de la verificación ejecutada. |

La simulación conserva coordenadas en píxeles con Y hacia abajo para reproducir las fórmulas originales. `BoardView` las convierte al mundo Unity con 100 píxeles/unidad e invierte Y una sola vez. Todos los temporizadores usan el mismo reloj de partida; al perder se congelan también las animaciones.

El césped usa una malla de quads: cada quad corresponde visualmente a una celda y repite el recorte central del JPG. Esta representación permite celdas rectangulares y redimensionado sin deformar el patrón. La ocupación sigue siendo lógica de 9 × 5; las filas decorativas tras el selector no son jugables.

El HUD está hecho con componentes nativos `Canvas`, `Image` y `TextMeshProUGUI`. El clic se enruta una sola vez por el modelo, utilizando los mismos rectángulos que dibuja la interfaz; no hay listeners adicionales que cobren o coloquen dos veces.

## Reconstruir y compilar

Desde el Editor están disponibles:

- **Jardin → Reconstruir escena y recursos**: vuelve a generar los recursos preparados, GameArt, prefabs y la escena inicial. Úsalo con cuidado si has personalizado esos archivos generados.
- **Jardin → Verificar recursos y escena**: comprueba referencias, fotogramas, shaders y Renderer 2D.
- **Jardin → Compilar Windows**: genera `Build/Windows/Jardin.exe`.

La reconstrucción conserva las imágenes originales de `Art`, pero reemplaza la escena y los recursos generados. Para conservar cambios propios en ellos, haz una copia o guárdalos con otros nombres antes de reconstruir.

También puedes compilar con Unity cerrado, desde PowerShell en esta carpeta:

```powershell
.\Tools\Build.ps1
```

Para regenerar previamente escena y recursos:

```powershell
.\Tools\Build.ps1 -RebuildScene
```

El script usa el Editor 6000.6.8f1 instalado en la ruta habitual. Puedes pasar `-UnityEditorPath` si tu instalación está en otra ubicación. Unity necesita una licencia activa y acceso a sus paquetes; CMake y raylib no intervienen en este proyecto.

## Ejecutar pruebas

Las pruebas del modelo requieren el SDK .NET 10, instalado en este equipo; esa dependencia es solo del ejecutor de pruebas externo, no de jugar o abrir Unity.

```powershell
dotnet run --project .\Tests\ModelTests.csproj
```

El resultado se guarda en `Validation/model-tests.json`. Las pruebas cubren compra/cancelación, ocupación, soles, prioridades del clic, combate, temporizadores, dificultad, derrota y reinicio.

Para comprobar también la escena compilada y generar capturas:

```powershell
.\Tools\Validate.ps1
```

El ejecutable admite `-gardenSmoke -gardenSmokeOutput <carpeta>` exclusivamente como ruta de comprobación automática: construye una partida de prueba, verifica UI y actores, captura juego/colocación/derrota con la GPU, comprueba Retry y termina con código 0 si pasa. Las capturas usan un RenderTexture para funcionar también con la ventana oculta. Se comprueban render y ciclo de vida con entradas lógicas; esta ruta no sustituye la prueba manual del ratón y teclado físicos. Sin esos argumentos se abre el juego normal.

La guía de migración general está en [readmeToUnity.md](../readmeToUnity.md). Esa guía usa 6.3 LTS como referencia; este proyecto se generó y se valida con **6000.6.8f1**, aprovechando la instalación disponible.

## Validación realizada

- **Modelo:** 25 casos, 101 aserciones y 0 fallos. [Informe](Validation/model-tests.json).
- **Editor:** recursos, prefabs, escena, materiales y Universal 2D correctos. [Informe](Validation/editor-validation.json).
- **Windows:** compilación correcta, 0 errores y 0 advertencias. [Informe](Validation/build-validation.json).
- **Ejecutable con GPU:** partida, zombis, guisantes, soles, vista previa, derrota y Retry comprobados a 1920 × 1080 y 1280 × 720. [Informe 1080p](Validation/Player/player-validation.json), [informe 720p](Validation/Player720/player-validation.json).
- **Recursos:** las 17 imágenes originales copiadas conservan exactamente sus bytes; los recortes se guardan aparte.

Capturas del ejecutable: [partida](Validation/Player/gameplay.png), [colocación](Validation/Player/placement.png) y [derrota](Validation/Player/game-over.png).
