#include "Guisantes.h"

Guisantes::Guisantes()
    // No produce monedas; espera 2 segundos despues de desaparecer su guisante.
    : Plant("./assets/plants/sprites/Guisantes.png", "./assets/plants/banners/guisantesBanner.png", 100, 0.0, 2.0)
{
}

Guisantes::~Guisantes()
{
}
