#ifndef SELECTOR_H
#define SELECTOR_H

#include<vector>
#include <raylib.h>
#include "../plant/Plant.h"

class Selector
{
private:
    Rectangle rec;
    std::vector<Plant> plants;
    Vector2 cursorPosition{};
    int selectedPlant = -1;
    bool shovelSelected = false;
    Texture2D shovelTexture{};
    Rectangle shovelSource{};

    // Saldo + nueve tarjetas + el ultimo espacio reservado para la pala.
    static constexpr int slotCount = 11;
    static constexpr int plantSlotCount = 9;
    Rectangle getCellRectangle(int index) const;
    Rectangle getPlantRectangle(int index) const;
    Rectangle getShovelRectangle() const;
    void drawShovel(Rectangle destination) const;

public:
    Selector(int w, int h);
    Selector(const Selector&) = delete;
    Selector& operator=(const Selector&) = delete;
    void drawSelector(int playerSuns, int manualZombieCount = 0) const;
    void updateSelector(int w, int h);
    void addPlants(std::vector<Plant>);
    void handleInput(Vector2 mousePosition, bool leftMousePressed, int& playerCurrency);
    void drawSelectionPreview() const;
    bool isShovelSelected() const;
    Rectangle getBounds() const;
    const Plant* getSelectedPlant() const;
    void clearSelection();
    ~Selector();
};


#endif
