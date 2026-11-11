#ifndef GAME_SESSION_H
#define GAME_SESSION_H

#include "../selector/Selector.h"
#include "../yard/Yard.h"

class GameSession
{
public:
    GameSession(int width, int height, double now);
    void update(int width, int height, Vector2 mousePosition, bool leftMousePressed, double now,
        bool spawnPressed = false, bool increasePressed = false, bool decreasePressed = false);
    void draw(Vector2 mousePosition) const;
    void retry(double now);
    bool shouldExit() const;
    int getPlayerCurrency() const;
    double getSpawnInterval() const;
    int getManualZombieCount() const;
    const Yard& getYard() const;
    Rectangle getRetryButtonBounds() const;
    Rectangle getExitButtonBounds() const;

private:
    Selector selector;
    Yard yard;
    Vector2 screenSize;
    int playerCurrency = 1000;
    int manualZombieCount = 5000;
    double startedAt = 0.0;
    double currentTime = 0.0;
    double nextZombieTime = 0.0;
    bool exitRequested = false;

    double spawnIntervalAt(double time) const;
    Rectangle getGameOverPanel() const;
    void drawGameOver(Vector2 mousePosition) const;
};

#endif
