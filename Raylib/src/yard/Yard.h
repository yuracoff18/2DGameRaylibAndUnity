#ifndef YARD_H
#define YARD_H

#include <raylib.h>
#include <vector>
#include "../plant/Plant.h"
#include "../zombie/Zombie.h"

class Yard
{
private:
    struct PlacedPlant
    {
        Plant plant;
        int row;
        int column;
        double nextCurrencyTime;
        bool currencyPaused = false;
        // Cada lanzador tiene como maximo un proyectil y su propio tiempo de espera.
        bool peaActive = false;
        float peaX = 0.0f;
        double peaUpdatedAt = 0.0;
        double nextPeaTime = 0.0;
        int health = 3;
    };

    struct CurrencyDrop
    {
        int row;
        int column;
        double bornAt;
        unsigned int offset;
    };

    static constexpr int rowCount = 5;
    static constexpr int columnCount = 9;
    static constexpr int currencyValue = 50;
    static constexpr std::size_t maxCurrencyPerCell = 3;
    static constexpr double currencyLifetime = 10.0;
    static constexpr float peaSpeed = 420.0f;
    static constexpr float zombieSpeed = 28.0f;
    Rectangle rec{};
    Vector2 screenSize{};
    std::vector<PlacedPlant> plantedPlants;
    std::vector<CurrencyDrop> currencyDrops;
    Zombie::ResourcesPtr zombieResources;
    std::vector<Zombie> zombies;
    bool gameOver = false;
    int defeatedZombies = 0;
    Texture2D currencyTexture{};
    Texture2D groundTexture{};
    Rectangle currencySource{};
    double currentTime = 0.0;

    Rectangle getCellRectangle(int row, int column) const;
    bool getCellAt(Vector2 position, int& row, int& column) const;
    bool isOccupied(int row, int column) const;
    std::size_t countCurrencyAt(int row, int column) const;
    unsigned int getFreeCurrencyOffset(int row, int column) const;
    void resumeCurrencyAt(int row, int column);
    Vector2 getCurrencyPosition(const CurrencyDrop& drop) const;
    float getCurrencyRadius() const;
    Vector2 getPeaOrigin(const PlacedPlant& placed) const;
    float getPeaRadius() const;
    void updatePea(PlacedPlant& placed, double now);
    float getZombieHeight() const;
    Vector2 getZombieFeet(const Zombie& zombie) const;
    Rectangle getZombieBounds(const Zombie& zombie) const;
    bool hasZombieAhead(int row, float x) const;
    void updateZombies(double now);
public:
    Yard(Rectangle selectorBounds, int windowWidth, int windowHeight);
    Yard(const Yard&) = delete;
    Yard& operator=(const Yard&) = delete;
    void updateLayout(Rectangle selectorBounds, int windowWidth, int windowHeight);
    void update(double now);
    void reset(double now);
    bool spawnZombie(int row, double now);
    bool isGameOver() const;
    int getDefeatedZombieCount() const;
    int getRowCount() const;
    const std::vector<Zombie>& getZombies() const;
    bool contains(Vector2 position) const;
    bool canPlantAt(Vector2 position) const;
    bool plantAt(Vector2 position, const Plant& plant, double now);
    bool removePlant(Vector2 position);
    bool collectCurrency(Vector2 position, int& playerCurrency);
    void drawYard(Vector2 mousePosition, bool placingPlant, bool removingPlant = false) const;
    Rectangle getBounds() const;
    std::size_t getPlantCount() const;
    std::size_t getCurrencyCount() const;
    std::size_t getPeaCount() const;
    std::vector<Vector2> getPeaPositions() const;
    ~Yard();
};

#endif
