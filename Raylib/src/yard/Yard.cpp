#include "Yard.h"
#include <algorithm>
#include <cmath>

Yard::Yard(Rectangle selectorBounds, int windowWidth, int windowHeight)
{
    updateLayout(selectorBounds, windowWidth, windowHeight);

    // Usar la moneda existente, recortando solo su margen transparente.
    Image image = LoadImage("./assets/currency.png");
    if (IsImageValid(image))
    {
        currencySource = GetImageAlphaBorder(image, 0.1f);
        currencyTexture = LoadTextureFromImage(image);
        UnloadImage(image);
        if (IsTextureValid(currencyTexture))
        {
            GenTextureMipmaps(&currencyTexture);
            SetTextureFilter(currencyTexture, TEXTURE_FILTER_TRILINEAR);
        }
    }
    // La misma textura de cesped se reutiliza en cada celda del terreno.
    groundTexture = LoadTexture("./assets/Cartoon_green_texture_grass.jpg");
    if (IsTextureValid(groundTexture))
    {
        GenTextureMipmaps(&groundTexture);
        SetTextureFilter(groundTexture, TEXTURE_FILTER_TRILINEAR);
    }
    zombieResources = Zombie::loadResources();
}

void Yard::updateLayout(Rectangle selectorBounds, int windowWidth, int windowHeight)
{
    // Conservar la posicion relativa de los guisantes al cambiar la resolucion.
    if (screenSize.x > 0 && windowWidth > 0 && screenSize.x != windowWidth)
    {
        const float scale = windowWidth / screenSize.x;
        for (PlacedPlant& placed : plantedPlants)
            if (placed.peaActive) placed.peaX *= scale;
        for (Zombie& zombie : zombies) zombie.scaleX(scale);
    }
    screenSize = {float(std::max(0, windowWidth)), float(std::max(0, windowHeight))};

    // La cuadricula sigue debajo del selector, pero llega a ambos lados de la ventana.
    const float gap = std::clamp(windowHeight * 0.012f, 8.0f, 16.0f);
    rec.x = 0;
    rec.y = selectorBounds.y + selectorBounds.height + gap;
    rec.width = screenSize.x;
    rec.height = std::max(0.0f, screenSize.y - rec.y);
}

Rectangle Yard::getCellRectangle(int row, int column) const
{
    const float width = rec.width / columnCount;
    const float height = rec.height / rowCount;
    return {rec.x + column * width, rec.y + row * height, width, height};
}

bool Yard::contains(Vector2 position) const
{
    // Excluir el borde derecho e inferior evita crear una fila o columna extra.
    return rec.width > 0 && rec.height > 0 && position.x >= rec.x && position.x < rec.x + rec.width
        && position.y >= rec.y && position.y < rec.y + rec.height;
}

bool Yard::getCellAt(Vector2 position, int& row, int& column) const
{
    if (!contains(position)) return false;
    row = std::min(rowCount - 1, int((position.y - rec.y) / (rec.height / rowCount)));
    column = std::min(columnCount - 1, int((position.x - rec.x) / (rec.width / columnCount)));
    return true;
}

bool Yard::isOccupied(int row, int column) const
{
    return std::any_of(plantedPlants.begin(), plantedPlants.end(), [row, column](const PlacedPlant& placed)
    {
        return placed.row == row && placed.column == column;
    });
}

bool Yard::canPlantAt(Vector2 position) const
{
    int row, column;
    return getCellAt(position, row, column) && !isOccupied(row, column);
}

bool Yard::plantAt(Vector2 position, const Plant& plant, double now)
{
    if (gameOver) return false;
    int row, column;
    if (!plant.hasSprite() || !getCellAt(position, row, column) || isOccupied(row, column)) return false;

    // La moneda se genera a partir del momento de colocacion, no de la compra.
    plantedPlants.push_back({plant, row, column, now + plant.getCurrencyInterval(), false});
    PlacedPlant& placed = plantedPlants.back();
    if (plant.getShotCooldown() > 0.0)
    {
        // Solo disparar si ya hay un zombi vivo delante en su misma fila.
        placed.peaX = getPeaOrigin(placed).x;
        placed.peaActive = hasZombieAhead(row, placed.peaX);
        placed.peaUpdatedAt = now;
        placed.nextPeaTime = now;
    }
    currentTime = now;
    return true;
}

bool Yard::removePlant(Vector2 position)
{
    if (gameOver) return false;
    int row, column;
    if (!getCellAt(position, row, column)) return false;
    const auto placed = std::find_if(plantedPlants.begin(), plantedPlants.end(), [row, column](const PlacedPlant& plant)
    {
        return plant.row == row && plant.column == column;
    });
    if (placed == plantedPlants.end()) return false;

    // Retirar no cobra ni devuelve monedas; los soles ya generados siguen su vida normal.
    plantedPlants.erase(placed);
    return true;
}

std::size_t Yard::countCurrencyAt(int row, int column) const
{
    return std::count_if(currencyDrops.begin(), currencyDrops.end(), [row, column](const CurrencyDrop& drop)
    {
        return drop.row == row && drop.column == column;
    });
}

unsigned int Yard::getFreeCurrencyOffset(int row, int column) const
{
    // Reutilizar uno de los tres lugares libres, sin superponer soles de la misma celda.
    for (unsigned int offset = 0; offset < maxCurrencyPerCell; ++offset)
    {
        const bool occupied = std::any_of(currencyDrops.begin(), currencyDrops.end(), [row, column, offset](const CurrencyDrop& drop)
        {
            return drop.row == row && drop.column == column && drop.offset == offset;
        });
        if (!occupied) return offset;
    }
    return 0;
}

void Yard::resumeCurrencyAt(int row, int column)
{
    if (countCurrencyAt(row, column) >= maxCurrencyPerCell) return;
    for (PlacedPlant& placed : plantedPlants)
    {
        if (placed.row == row && placed.column == column && placed.currencyPaused)
        {
            // Volver a esperar el intervalo completo: no hay generaciones acumuladas.
            placed.currencyPaused = false;
            placed.nextCurrencyTime = currentTime + placed.plant.getCurrencyInterval();
        }
    }
}

void Yard::update(double now)
{
    if (gameOver) return;
    currentTime = now;
    updateZombies(now);
    if (gameOver) return;
    // Un sol desaparece a los 10 segundos aunque no haya sido recogido.
    currencyDrops.erase(std::remove_if(currencyDrops.begin(), currencyDrops.end(), [now](const CurrencyDrop& drop)
    {
        return now - drop.bornAt >= currencyLifetime;
    }), currencyDrops.end());

    for (PlacedPlant& placed : plantedPlants)
    {
        const double interval = placed.plant.getCurrencyInterval();
        if (interval > 0.0)
        {
            if (countCurrencyAt(placed.row, placed.column) >= maxCurrencyPerCell)
            {
                placed.currencyPaused = true;
                placed.nextCurrencyTime = now + interval;
            }
            else if (placed.currencyPaused)
            {
                placed.currencyPaused = false;
                placed.nextCurrencyTime = now + interval;
            }
            else if (now >= placed.nextCurrencyTime)
            {
                const unsigned int offset = getFreeCurrencyOffset(placed.row, placed.column);
                currencyDrops.push_back({placed.row, placed.column, now, offset});
                placed.nextCurrencyTime = now + interval;
                placed.currencyPaused = countCurrencyAt(placed.row, placed.column) >= maxCurrencyPerCell;
            }
        }
        updatePea(placed, now);
    }
    // Resolver primero los impactos: un zombi abatido no puede causar la derrota.
    for (const Zombie& zombie : zombies)
    {
        const Rectangle bounds = getZombieBounds(zombie);
        if (zombie.isAlive() && bounds.x + bounds.width < 0) gameOver = true;
    }
    zombies.erase(std::remove_if(zombies.begin(), zombies.end(), [now](const Zombie& zombie)
    {
        return zombie.finishedFalling(now);
    }), zombies.end());
}

float Yard::getZombieHeight() const { return rec.height / rowCount * 0.95f; }

Vector2 Yard::getZombieFeet(const Zombie& zombie) const
{
    const Rectangle cell = getCellRectangle(zombie.getRow(), 0);
    return {zombie.getX(), cell.y + cell.height * 0.93f};
}

Rectangle Yard::getZombieBounds(const Zombie& zombie) const
{
    const float height = getZombieHeight();
    const Vector2 feet = getZombieFeet(zombie);
    return {feet.x - height * 0.23f, feet.y - height, height * 0.46f, height};
}

bool Yard::hasZombieAhead(int row, float x) const
{
    return std::any_of(zombies.begin(), zombies.end(), [this, row, x](const Zombie& zombie)
    {
        const Rectangle bounds = getZombieBounds(zombie);
        return zombie.isAlive() && zombie.getRow() == row && bounds.x + bounds.width >= x;
    });
}

void Yard::updateZombies(double now)
{
    for (Zombie& zombie : zombies)
    {
        if (!zombie.isAlive()) { zombie.moveTo(zombie.getX(), now); continue; }
        const double previousTime = zombie.getUpdatedAt();
        const float previousX = zombie.getX();
        float nextX = previousX - zombieSpeed * float(std::max(0.0, now - previousTime));
        std::size_t blockingPlant = plantedPlants.size();
        float stopX = nextX;
        for (std::size_t i = 0; i < plantedPlants.size(); ++i)
        {
            const PlacedPlant& placed = plantedPlants[i];
            if (placed.row != zombie.getRow()) continue;
            const Rectangle cell = getCellRectangle(placed.row, placed.column);
            const float centerX = cell.x + cell.width / 2.0f;
            const float plantHeight = std::min(cell.height * 0.82f, cell.width * 1.3f);
            const float contactX = centerX + plantHeight / 3.0f + getZombieHeight() * 0.23f;
            if (previousX < centerX - plantHeight / 3.0f || contactX < nextX) continue;
            const float candidate = std::min(previousX, contactX);
            if (blockingPlant == plantedPlants.size() || candidate > stopX)
            {
                blockingPlant = i;
                stopX = candidate;
            }
        }
        if (blockingPlant < plantedPlants.size()) nextX = stopX;
        zombie.moveTo(nextX, now);

        if (blockingPlant < plantedPlants.size())
        {
            PlacedPlant& placed = plantedPlants[blockingPlant];
            const double arrival = previousTime + (previousX - nextX) / zombieSpeed;
            zombie.startEating(placed.column, arrival);
            while (now >= zombie.getNextBiteTime())
            {
                --placed.health;
                zombie.advanceBite();
                if (placed.health <= 0)
                {
                    plantedPlants.erase(plantedPlants.begin() + blockingPlant);
                    zombie.stopEating();
                    break;
                }
            }
        }
        else zombie.stopEating();

    }
}

Vector2 Yard::getPeaOrigin(const PlacedPlant& placed) const
{
    const Rectangle cell = getCellRectangle(placed.row, placed.column);
    const float height = std::min(cell.height * 0.82f, cell.width * 1.3f);
    // Posicion de la boca en el sprite vertical del lanzaguisantes.
    return {cell.x + cell.width / 2.0f + height * 0.28f, cell.y + cell.height / 2.0f - height * 0.07f};
}

float Yard::getPeaRadius() const
{
    return std::clamp(rec.height / rowCount * 0.05f, 6.0f, 11.0f);
}

void Yard::updatePea(PlacedPlant& placed, double now)
{
    const double cooldown = placed.plant.getShotCooldown();
    if (cooldown <= 0.0) return;

    if (placed.peaActive)
    {
        // Velocidad por segundo: el movimiento no depende de los FPS.
        const double elapsed = std::max(0.0, now - placed.peaUpdatedAt);
        const float previousX = placed.peaX;
        const float nextX = previousX + float(elapsed) * peaSpeed;
        Zombie* target = nullptr;
        float firstImpact = 2.0f;
        for (Zombie& zombie : zombies)
        {
            if (!zombie.isAlive() || zombie.getRow() != placed.row) continue;
            const Rectangle bounds = getZombieBounds(zombie);
            const float oldLeft = bounds.x + zombie.getPreviousX() - zombie.getX();
            const float radius = getPeaRadius();
            if (previousX > oldLeft + bounds.width + radius) continue;
            // Barrido relativo: evita atravesar zombis incluso con fotogramas lentos.
            const float advance = nextX - previousX - (zombie.getX() - zombie.getPreviousX());
            const float distance = oldLeft - radius - previousX;
            if (distance > 0 && advance <= 0) continue;
            const float impact = distance <= 0 ? 0.0f : distance / advance;
            if (impact >= 0 && impact <= 1 && impact < firstImpact)
            {
                firstImpact = impact;
                target = &zombie;
            }
        }
        placed.peaX = nextX;
        if (target != nullptr)
        {
            if (target->takeHit(now)) ++defeatedZombies;
            placed.peaActive = false; // Un guisante solo puede dar un golpe.
            placed.nextPeaTime = now + cooldown;
        }
        else if (placed.peaX - getPeaRadius() >= screenSize.x)
        {
            placed.peaActive = false;
            // El descanso empieza al desaparecer, no al lanzar el proyectil.
            placed.nextPeaTime = now + cooldown;
        }
    }
    else if (now >= placed.nextPeaTime && hasZombieAhead(placed.row, getPeaOrigin(placed).x))
    {
        placed.peaActive = true;
        placed.peaX = getPeaOrigin(placed).x;
    }
    placed.peaUpdatedAt = now;
}

Vector2 Yard::getCurrencyPosition(const CurrencyDrop& drop) const
{
    const Rectangle cell = getCellRectangle(drop.row, drop.column);
    const float offsets[] = {0.23f, 0.5f, 0.77f};
    const float progress = std::clamp(float((currentTime - drop.bornAt) / 0.6), 0.0f, 1.0f);
    const float startX = cell.x + cell.width * 0.5f;
    const float targetX = cell.x + cell.width * offsets[drop.offset % 3];
    const float startY = cell.y + cell.height * 0.28f;
    const float targetY = cell.y + cell.height * 0.8f;
    return {startX + (targetX - startX) * progress, startY + (targetY - startY) * progress * progress};
}

float Yard::getCurrencyRadius() const
{
    return std::clamp(std::min(rec.width / columnCount, rec.height / rowCount) * 0.16f, 10.0f, 24.0f);
}

bool Yard::collectCurrency(Vector2 position, int& playerCurrency)
{
    if (gameOver) return false;
    // Revisar desde la moneda visible de arriba y recoger solo una por clic.
    for (std::size_t i = currencyDrops.size(); i > 0; --i)
    {
        if (CheckCollisionPointCircle(position, getCurrencyPosition(currencyDrops[i - 1]), getCurrencyRadius()))
        {
            playerCurrency += currencyValue;
            const int row = currencyDrops[i - 1].row;
            const int column = currencyDrops[i - 1].column;
            currencyDrops.erase(currencyDrops.begin() + (i - 1));
            resumeCurrencyAt(row, column);
            return true;
        }
    }
    return false;
}

void Yard::drawYard(Vector2 mousePosition, bool placingPlant, bool removingPlant) const
{
    Rectangle viewport = {0.0f, 0.0f, screenSize.x, screenSize.y};
    if (IsTextureValid(groundTexture) && rec.width > 0 && rec.height > 0)
    {
        // Cada celda repite el patron completo a la misma escala, tambien alrededor del selector.
        const Rectangle tile = getCellRectangle(0, 0);
        const float scale = std::max(tile.width / groundTexture.width, tile.height / groundTexture.height);
        const float width = tile.width / scale;
        const float height = tile.height / scale;
        Rectangle source = {(groundTexture.width - width) / 2.0f, (groundTexture.height - height) / 2.0f, width, height};
        const int firstRow = -int(std::ceil(rec.y / tile.height));
        for (int row = firstRow; row < rowCount; ++row)
            for (int column = 0; column < columnCount; ++column)
                DrawTexturePro(groundTexture, source, getCellRectangle(row, column), {0, 0}, 0, WHITE);
    }
    else DrawRectangleRec(viewport, {108, 165, 76, 255});
    if (rec.width <= 0 || rec.height <= 0) return;

    // Guardar fila y columna mantiene las plantas en su celda al cambiar el tamano.
    for (const PlacedPlant& placed : plantedPlants)
    {
        const Rectangle cell = getCellRectangle(placed.row, placed.column);
        const Vector2 center = {cell.x + cell.width / 2.0f, cell.y + cell.height / 2.0f};
        DrawEllipseV({center.x, cell.y + cell.height * 0.91f}, cell.width * 0.16f, cell.height * 0.035f, Fade(BLACK, 0.18f));
        placed.plant.drawPlant(center, std::min(cell.height * 0.82f, cell.width * 1.3f));
    }

    for (const Zombie& zombie : zombies)
    {
        const Vector2 feet = getZombieFeet(zombie);
        DrawEllipseV(feet, getZombieHeight() * 0.20f, getZombieHeight() * 0.035f, Fade(BLACK, 0.18f));
        zombie.draw(feet, getZombieHeight(), currentTime);
    }

    for (const PlacedPlant& placed : plantedPlants)
    {
        if (!placed.peaActive) continue;
        const Vector2 position = {placed.peaX, getPeaOrigin(placed).y};
        const float radius = getPeaRadius();
        DrawCircleV(position, radius, {78, 209, 43, 255});
        DrawCircleLinesV(position, radius, {31, 100, 27, 255});
        DrawCircleV({position.x - radius * 0.3f, position.y - radius * 0.3f}, radius * 0.25f, {180, 243, 123, 255});
    }

    int hoveredRow, hoveredColumn;
    if ((placingPlant || removingPlant) && getCellAt(mousePosition, hoveredRow, hoveredColumn))
    {
        const Rectangle cell = getCellRectangle(hoveredRow, hoveredColumn);
        const bool occupied = isOccupied(hoveredRow, hoveredColumn);
        const Color highlight = removingPlant ? (occupied ? RED : GRAY) : (occupied ? RED : Color{223, 255, 126, 255});
        // La cuadricula es invisible; solo resaltar la celda al colocar una planta.
        DrawRectangleRec(cell, Fade(highlight, 0.08f));
        DrawRectangleLinesEx(cell, 2.0f, Fade(highlight, 0.65f));
    }

    // Dibujar las monedas sobre las plantas para que sean visibles y recogibles.
    const float radius = getCurrencyRadius();
    for (const CurrencyDrop& drop : currencyDrops)
    {
        const Vector2 position = getCurrencyPosition(drop);
        DrawCircleV(position, radius + 3, Fade(GOLD, 0.3f));
        if (IsTextureValid(currencyTexture) && currencySource.width > 0 && currencySource.height > 0)
        {
            const float scale = std::min(2 * radius / currencySource.width, 2 * radius / currencySource.height);
            const float width = currencySource.width * scale;
            const float height = currencySource.height * scale;
            Rectangle destination = {position.x - width / 2, position.y - height / 2, width, height};
            DrawTexturePro(currencyTexture, currencySource, destination, {0, 0}, 0, WHITE);
        }
        else DrawCircleV(position, radius, GOLD);

        const int fontSize = std::max(10, int(radius * 0.8f));
        DrawText("50", int(position.x - MeasureText("50", fontSize) / 2.0f), int(position.y - fontSize / 2.0f), fontSize, {95, 63, 15, 255});
    }
}

Rectangle Yard::getBounds() const
{
    return rec;
}

void Yard::reset(double now)
{
    // Reiniciar la partida conserva las texturas ya cargadas.
    plantedPlants.clear();
    currencyDrops.clear();
    zombies.clear();
    currentTime = now;
    defeatedZombies = 0;
    gameOver = false;
}

bool Yard::spawnZombie(int row, double now)
{
    if (gameOver || row < 0 || row >= rowCount) return false;
    zombies.emplace_back(zombieResources, row, screenSize.x + getZombieHeight() * 0.5f + 12.0f, now);
    return true;
}

bool Yard::isGameOver() const { return gameOver; }
int Yard::getDefeatedZombieCount() const { return defeatedZombies; }
int Yard::getRowCount() const { return rowCount; }
const std::vector<Zombie>& Yard::getZombies() const { return zombies; }

std::size_t Yard::getPlantCount() const
{
    return plantedPlants.size();
}

std::size_t Yard::getCurrencyCount() const
{
    return currencyDrops.size();
}

std::size_t Yard::getPeaCount() const
{
    return std::count_if(plantedPlants.begin(), plantedPlants.end(), [](const PlacedPlant& placed) { return placed.peaActive; });
}

std::vector<Vector2> Yard::getPeaPositions() const
{
    std::vector<Vector2> positions;
    for (const PlacedPlant& placed : plantedPlants)
        if (placed.peaActive) positions.push_back({placed.peaX, getPeaOrigin(placed).y});
    return positions;
}

Yard::~Yard()
{
    if (IsTextureValid(currencyTexture)) UnloadTexture(currencyTexture);
    if (IsTextureValid(groundTexture)) UnloadTexture(groundTexture);
}
