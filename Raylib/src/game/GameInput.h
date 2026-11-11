#ifndef GAME_INPUT_H
#define GAME_INPUT_H

#include <raylib.h>

class Selector;
class Yard;

void handleGameInput(Selector& selector, Yard& yard, Vector2 mousePosition,
    bool leftMousePressed, int& playerCurrency, double now);

#endif
