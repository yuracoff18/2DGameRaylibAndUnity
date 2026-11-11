#include "GameInput.h"
#include "../selector/Selector.h"
#include "../yard/Yard.h"

void handleGameInput(Selector& selector, Yard& yard, Vector2 mousePosition,
    bool leftMousePressed, int& playerCurrency, double now)
{
    if (yard.isGameOver()) return;
    // Una moneda consume el clic y conserva la planta que sigue al cursor.
    if (leftMousePressed && yard.collectCurrency(mousePosition, playerCurrency)) leftMousePressed = false;

    if (leftMousePressed && selector.isShovelSelected() && yard.contains(mousePosition))
    {
        // Quitar una planta no modifica el saldo; una celda vacia conserva la pala.
        if (yard.removePlant(mousePosition)) selector.clearSelection();
        leftMousePressed = false;
    }

    const Plant* selectedPlant = selector.getSelectedPlant();
    if (leftMousePressed && selectedPlant != nullptr && yard.contains(mousePosition))
    {
        if (yard.plantAt(mousePosition, *selectedPlant, now)) selector.clearSelection();
        // Una celda ocupada conserva la seleccion para poder elegir otra.
        leftMousePressed = false;
    }

    // Solo los clics que quedan libres compran o cancelan en el selector.
    selector.handleInput(mousePosition, leftMousePressed, playerCurrency);
}
