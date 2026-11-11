# Compilar el proyecto

Requisitos: CMake 3.28 o superior y MSYS2 UCRT64 con GCC y Ninja instalados en `C:/msys64/ucrt64/bin`.

En VS Code, abre la carpeta `programa` completa. La configuracion de `.vscode`
identifica `CMakeLists.txt` y utiliza CMake Tools con GCC UCRT64. Usa
`Ctrl+Shift+B` para configurar y compilar, y `F5` para ejecutar con el depurador.
Si el editor ya estaba abierto al cambiar esta configuracion, ejecuta
`Developer: Reload Window` desde la paleta de comandos (`Ctrl+Shift+P`).

Desde la carpeta del proyecto:

```powershell
cmake --preset "GCC 16.1.0 x86_64-w64-mingw32 (ucrt64)"
cmake --build --preset debug
& ".\out\build\GCC 16.1.0 x86_64-w64-mingw32 (ucrt64)\io.exe"
```

La primera configuracion descarga raylib 6.0 si no esta instalado.
Al copiar el proyecto a otra ubicacion, omite la carpeta generada `out`, ya que
contiene rutas absolutas de la compilacion anterior. CMake la creara de nuevo.

## Agregar clases en src

Guarda las declaraciones en `src/MiClase.h` (o `.hpp`) y la implementacion en
`src/MiClase.cpp`. En `main.cpp` puedes utilizarlas asi:

```cpp
#include "MiClase.h"
```

Los archivos de `src` se compilan y enlazan automaticamente al volver a compilar;
no necesitas editar `CMakeLists.txt` para cada clase. Tambien se detectan las
subcarpetas: para `src/modelo/MiClase.h`, usa `#include "modelo/MiClase.h"`.
Incluye las cabeceras, no los archivos `.cpp`, y conserva un solo `main()` en el proyecto.

## Cargar imagenes y otros recursos

Guarda los recursos en `assets/`. Cada compilacion copia esta carpeta junto a
`io.exe`, y el programa busca los archivos desde esa ubicacion, independientemente
de la carpeta desde la que se ejecute. Vuelve a compilar despues de agregar o
modificar recursos. CMake habilita el soporte JPG al compilar raylib.

## Selector de plantas

La barra muestra el saldo y los tipos de plantas disponibles; la pala ocupa el ultimo espacio.
Haz clic izquierdo en una tarjeta para descontar su coste y mostrar la planta
siguiendo el cursor. Haz clic en una celda vacia del patio para colocarla, sin
volver a cobrar. Una celda ocupada conserva la seleccion para elegir otra;
un clic fuera del patio cancela la vista previa. Si el saldo es insuficiente,
la tarjeta se atenua y no se puede comprar.

## Patio y monedas

El patio conserva 9 columnas y 5 filas invisibles debajo del selector, pero ahora
se extiende de izquierda a derecha hasta los bordes de la ventana. Cada celda repite
el patron de `assets/Cartoon_green_texture_grass.jpg`; solo se resalta una celda
al colocar o retirar una planta. Las plantas conservan su celda al cambiar el tamano
de la ventana. Cada girasol colocado genera un sol cada 15 segundos, contando
desde su colocacion. Haz clic en una moneda para recogerla y sumar 50 al saldo;
ese clic no compra, coloca ni retira otra planta. Cada sol caduca tras 10 segundos
sin recogerlo. Puede haber hasta 3 soles pendientes por celda: si se llena, se
pausa la produccion. Al quedar espacio, comienza otro intervalo completo de 15
segundos, sin guardar generaciones atrasadas.

## Pala

Selecciona la pala al final de la barra y haz clic en una planta para quitarla.
No cuesta monedas ni devuelve el coste de compra. La celda queda libre y la pala
se desactiva al retirar la planta. Si haces clic en una celda vacia, la pala sigue
seleccionada; pulsa su boton otra vez para cancelarla. Los soles ya generados
permanecen hasta recogerlos o hasta cumplir sus 10 segundos de vida.

## Lanzaguisantes

La segunda tarjeta compra un lanzaguisantes por 100. Solo dispara si hay un zombi
vivo delante, en su misma fila. Cada lanzador mantiene un solo guisante activo:
cuando golpea a un zombi o sale completamente de la ventana, empieza una espera
de 2 segundos antes del siguiente disparo. El movimiento depende del tiempo
transcurrido, no de los FPS.

## Zombis y derrota

Los zombis aparecen por la derecha en una fila aleatoria y avanzan a la izquierda.
Se animan usando los fotogramas de `assets/Zombies`. Al encontrar una planta se
detienen a comerla; tras tres mordiscos la retiran y siguen avanzando. Cada zombi
recibe un destello breve al ser golpeado y cae con el cuarto impacto.

El primero aparece a los 4 segundos. El intervalo entre apariciones se reduce
gradualmente de 7 hasta un minimo de 1.2 segundos segun el tiempo de partida.
Si un zombi vivo sale por la izquierda, se detiene el juego y aparece `GAME OVER`.
`Retry` limpia plantas, soles, proyectiles y zombis, restaura el saldo de 1000 y
reinicia la dificultad. `Salir` cierra el juego. Reintentar reutiliza las texturas.

## Aparicion manual de zombis

Pulsa `X` para crear zombis adicionales por la derecha, cada uno en una fila
aleatoria. `+` aumenta en uno la cantidad por pulsacion y `-` la reduce, con un
minimo de uno. Tambien funcionan los signos del teclado numerico. La cantidad
actual se muestra en el selector y se conserva al pulsar `Retry`. Puedes pulsar
`X` repetidamente, sin coste ni tiempo de espera; mantenerla pulsada no crea nuevas
tandas. Estos controles se desactivan durante `GAME OVER`.
