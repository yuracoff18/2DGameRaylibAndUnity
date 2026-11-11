#include <raylib.h>
#include "src/game/GameSession.h"

// Constantes
int WINDOW_HEIGHT = 1080;
int WINDOW_WIDTH = 1920;

int main()
{

    // Inicializacion de ventana
    InitWindow(WINDOW_WIDTH, WINDOW_HEIGHT, "2D game");
    SetTargetFPS(0);

    // Buscar assets desde la carpeta del ejecutable.
    if (!ChangeDirectory(GetApplicationDirectory()))
    {
        CloseWindow();
        return 1;
    }

    // Destruir las plantas y liberar sus texturas antes de CloseWindow.
    {
        // La sesion conserva los recursos al reintentar y reinicia toda la partida.
        GameSession game(WINDOW_WIDTH, WINDOW_HEIGHT, GetTime());

        // Main loop
        while (!WindowShouldClose())
        {
            if (IsWindowFullscreen() && IsKeyPressed(KEY_F))
                ToggleFullscreen();
            else if (!IsWindowFullscreen() && IsKeyPressed(KEY_F))
                ToggleFullscreen();

            int w = GetScreenWidth();
            int h = GetScreenHeight();

            const Vector2 mousePosition = GetMousePosition();
            const bool leftMousePressed = IsMouseButtonPressed(MOUSE_BUTTON_LEFT);
            const double now = GetTime();

            // Leer los signos escritos respeta la distribucion del teclado; el numerico tambien funciona.
            bool increasePressed = IsKeyPressed(KEY_KP_ADD);
            bool decreasePressed = IsKeyPressed(KEY_KP_SUBTRACT);
            for (int character = GetCharPressed(); character > 0; character = GetCharPressed())
            {
                if (character == '+') increasePressed = true;
                if (character == '-') decreasePressed = true;
            }
            game.update(w, h, mousePosition, leftMousePressed, now,
                IsKeyPressed(KEY_X), increasePressed, decreasePressed);
            if (game.shouldExit()) break;

            BeginDrawing();
            ClearBackground(RAYWHITE);

            game.draw(mousePosition);

            EndDrawing();
        }
    }

    CloseWindow();
    return 0;
}
