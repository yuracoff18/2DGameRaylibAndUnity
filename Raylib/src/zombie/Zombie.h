#ifndef ZOMBIE_H
#define ZOMBIE_H

#include <raylib.h>
#include <memory>

class Zombie
{
public:
    struct Resources;
    using ResourcesPtr = std::shared_ptr<const Resources>;
    static ResourcesPtr loadResources();

    Zombie(ResourcesPtr resources, int row, float x, double now);
    int getRow() const;
    int getHealth() const;
    float getX() const;
    float getPreviousX() const;
    double getUpdatedAt() const;
    bool isAlive() const;
    bool finishedFalling(double now) const;
    bool takeHit(double now);
    void moveTo(float x, double now);
    void scaleX(float scale);
    void startEating(int column, double now);
    void stopEating();
    double getNextBiteTime() const;
    void advanceBite();
    void draw(Vector2 feet, float height, double now) const;

private:
    ResourcesPtr resources;
    int row;
    int health = 4;
    float x;
    float previousX;
    double updatedAt;
    double spawnedAt;
    double hitFlashUntil = -1.0;
    double fellAt = -1.0;
    int eatingColumn = -1;
    double eatingSince = 0.0;
    double nextBiteTime = 0.0;
};

#endif
