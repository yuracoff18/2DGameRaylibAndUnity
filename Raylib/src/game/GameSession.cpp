#include "GameSession.h"
#include "GameInput.h"
#include "../plant/girasol/Girasol.h"
#include "../plant/lanza_guisantes/Guisantes.h"
#include <algorithm>
#include <limits>
#include <string>

namespace
{
    void centeredText(const std::string& text, Rectangle bounds, int size, Color color)
    {
        size = std::max(10, std::min(size, int(bounds.height)));
        while (size > 10 && MeasureText(text.c_str(), size) > bounds.width - 12) --size;
        DrawText(text.c_str(), int(bounds.x + (bounds.width - MeasureText(text.c_str(), size)) / 2),
            int(bounds.y + (bounds.height - size) / 2), size, color);
    }
}

GameSession::GameSession(int width, int height, double now)
    : selector(width, height), yard(selector.getBounds(), width, height), screenSize{float(width), float(height)}
{
    Girasol sunflower;
    Guisantes peashooter;
    selector.addPlants({sunflower, peashooter});
    retry(now);
}

double GameSession::spawnIntervalAt(double time) const
{
    // Aumentar la frecuencia de forma gradual, con un minimo de 1.2 segundos entre zombis.
    return std::max(1.2, 7.0 - std::max(0.0, time - startedAt) * 0.02);
}

void GameSession::update(int width, int height, Vector2 mousePosition, bool leftMousePressed, double now,
    bool spawnPressed, bool increasePressed, bool decreasePressed)
{
    screenSize = {float(width), float(height)};
    selector.updateSelector(width, height);
    yard.updateLayout(selector.getBounds(), width, height);

    if (yard.isGameOver())
    {
        // La partida queda detenida; este clic solo puede pulsar los botones del cartel.
        if (leftMousePressed && CheckCollisionPointRec(mousePosition, getRetryButtonBounds())) retry(now);
        else if (leftMousePressed && CheckCollisionPointRec(mousePosition, getExitButtonBounds())) exitRequested = true;
        return;
    }

    currentTime = now;
    while (now >= nextZombieTime)
    {
        yard.spawnZombie(GetRandomValue(0, yard.getRowCount() - 1), nextZombieTime);
        nextZombieTime += spawnIntervalAt(nextZombieTime);
    }
    yard.update(now);
    if (yard.isGameOver())
    {
        selector.clearSelection();
        return;
    }

    // Configurar cuantos zombis crea cada pulsacion, con un minimo de uno.
    if (increasePressed && !decreasePressed && manualZombieCount < std::numeric_limits<int>::max()) ++manualZombieCount;
    if (decreasePressed && !increasePressed && manualZombieCount > 1) --manualZombieCount;
    if (spawnPressed)
    {
        // Cada zombi elige su propia fila; X no tiene cooldown ni consume monedas.
        for (int i = 0; i < manualZombieCount; ++i)
            yard.spawnZombie(GetRandomValue(0, yard.getRowCount() - 1), now);
    }
    handleGameInput(selector, yard, mousePosition, leftMousePressed, playerCurrency, now);
}

void GameSession::retry(double now)
{
    // Limpiar plantas, soles, proyectiles y zombis, sin volver a cargar las texturas.
    yard.reset(now);
    selector.clearSelection();
    playerCurrency = 1000;
    currentTime = startedAt = now;
    nextZombieTime = now + 4.0;
    exitRequested = false;
}

Rectangle GameSession::getGameOverPanel() const
{
    const float width = std::max(0.0f, std::min(540.0f, screenSize.x - 32));
    const float height = std::max(0.0f, std::min(300.0f, screenSize.y - 32));
    return {(screenSize.x - width) / 2, (screenSize.y - height) / 2, width, height};
}

Rectangle GameSession::getRetryButtonBounds() const
{
    const Rectangle panel = getGameOverPanel();
    return {panel.x + panel.width * 0.06f, panel.y + panel.height * 0.66f,
        panel.width * 0.42f, panel.height * 0.20f};
}

Rectangle GameSession::getExitButtonBounds() const
{
    const Rectangle panel = getGameOverPanel();
    return {panel.x + panel.width * 0.52f, panel.y + panel.height * 0.66f,
        panel.width * 0.42f, panel.height * 0.20f};
}

void GameSession::drawGameOver(Vector2 mousePosition) const
{
    DrawRectangle(0, 0, int(screenSize.x), int(screenSize.y), Fade(BLACK, 0.70f));
    const Rectangle panel = getGameOverPanel();
    DrawRectangleRounded(panel, 0.10f, 10, {68, 46, 32, 255});
    DrawRectangleRoundedLinesEx(panel, 0.10f, 10, 3, {218, 177, 103, 255});
    centeredText("GAME OVER", {panel.x + 12, panel.y + panel.height * 0.08f, panel.width - 24, panel.height * 0.27f}, 64, {255, 226, 167, 255});
    centeredText("Zombis derrotados: " + std::to_string(yard.getDefeatedZombieCount()),
        {panel.x + 12, panel.y + panel.height * 0.39f, panel.width - 24, panel.height * 0.13f}, 22, WHITE);

    const Rectangle retryButton = getRetryButtonBounds();
    const Rectangle exitButton = getExitButtonBounds();
    DrawRectangleRounded(retryButton, 0.18f, 8, CheckCollisionPointRec(mousePosition, retryButton) ? Color{123, 175, 74, 255} : Color{88, 139, 52, 255});
    DrawRectangleRounded(exitButton, 0.18f, 8, CheckCollisionPointRec(mousePosition, exitButton) ? Color{177, 85, 60, 255} : Color{139, 63, 45, 255});
    centeredText("Retry", retryButton, 26, WHITE);
    centeredText("Salir", exitButton, 26, WHITE);
}

void GameSession::draw(Vector2 mousePosition) const
{
    yard.drawYard(mousePosition, selector.getSelectedPlant() != nullptr, selector.isShovelSelected());
    selector.drawSelector(playerCurrency, manualZombieCount);
    if (yard.isGameOver()) drawGameOver(mousePosition);
    else selector.drawSelectionPreview();
}

bool GameSession::shouldExit() const { return exitRequested; }
int GameSession::getPlayerCurrency() const { return playerCurrency; }
double GameSession::getSpawnInterval() const { return spawnIntervalAt(currentTime); }
int GameSession::getManualZombieCount() const { return manualZombieCount; }
const Yard& GameSession::getYard() const { return yard; }
