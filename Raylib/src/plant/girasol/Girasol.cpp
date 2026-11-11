#include "Girasol.h"

// El girasol cuesta 50 y produce un sol cada 15 segundos despues de plantarlo.
Girasol::Girasol()
    : Plant("./assets/plants/sprites/girasol.png", "./assets/plants/banners/girasolBanner.png", 50, 15.0)
{
}

Girasol::~Girasol()
{
}
