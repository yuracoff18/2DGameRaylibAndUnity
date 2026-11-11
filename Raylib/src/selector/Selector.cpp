#include "Selector.h"
#include <algorithm>

namespace
{
    void drawCenteredText(const std::string& text, Rectangle bounds, int fontSize, Color color)
    {
        fontSize = std::max(10, std::min(fontSize, int(bounds.height)));
        while (fontSize > 10 && MeasureText(text.c_str(), fontSize) > bounds.width) --fontSize;
        const int x = int(bounds.x + (bounds.width - MeasureText(text.c_str(), fontSize)) / 2.0f);
        const int y = int(bounds.y + (bounds.height - fontSize) / 2.0f);
        DrawText(text.c_str(), x, y, fontSize, color);
    }
}

Selector::Selector(int w, int h)
{
    updateSelector(w, h);
    // La pala se carga una vez y no depende del saldo del jugador.
    Image image = LoadImage("./assets/Pala.png");
    if (IsImageValid(image))
    {
        shovelSource = GetImageAlphaBorder(image, 0.1f);
        shovelTexture = LoadTextureFromImage(image);
        UnloadImage(image);
        if (IsTextureValid(shovelTexture))
        {
            GenTextureMipmaps(&shovelTexture);
            SetTextureFilter(shovelTexture, TEXTURE_FILTER_TRILINEAR);
        }
    }
}

void Selector::addPlants(std::vector<Plant> p) {
    // Los indices empiezan en 0; cambiar la lista cancela la vista previa.
    plants = p;
    clearSelection();
}

Rectangle Selector::getCellRectangle(int index) const
{
    const float gap = std::clamp(rec.height * 0.05f, 4.0f, 12.0f);
    const float width = std::max(0.0f, (rec.width - gap * (slotCount + 1)) / slotCount);
    return {rec.x + gap + index * (width + gap), rec.y + gap, width, std::max(0.0f, rec.height - 2 * gap)};
}

Rectangle Selector::getPlantRectangle(int index) const
{
    const Rectangle cell = getCellRectangle(index + 1);
    // La forma vertical de la tarjeta conserva la proporcion 2:3 del banner.
    const float height = std::min(cell.height, cell.width * 1.5f);
    const float width = height / 1.5f;
    return {cell.x + (cell.width - width) / 2.0f, cell.y + (cell.height - height) / 2.0f, width, height};
}

Rectangle Selector::getShovelRectangle() const
{
    return getCellRectangle(slotCount - 1);
}

void Selector::drawShovel(Rectangle destination) const
{
    if (!IsTextureValid(shovelTexture) || shovelSource.width <= 0 || shovelSource.height <= 0) return;
    const float scale = std::min(destination.width / shovelSource.width, destination.height / shovelSource.height);
    Rectangle fitted = {0, 0, shovelSource.width * scale, shovelSource.height * scale};
    fitted.x = destination.x + (destination.width - fitted.width) / 2.0f;
    fitted.y = destination.y + (destination.height - fitted.height) / 2.0f;
    DrawTexturePro(shovelTexture, shovelSource, fitted, {0, 0}, 0, WHITE);
}

void Selector::handleInput(Vector2 mousePosition, bool leftMousePressed, int& playerCurrency)
{
    cursorPosition = mousePosition;
    if (!leftMousePressed) return;

    // Pulsar la pala alterna su seleccion, sin cobrar y sin seleccionar una planta.
    if (CheckCollisionPointRec(cursorPosition, getShovelRectangle()))
    {
        if (IsTextureValid(shovelTexture))
        {
            shovelSelected = !shovelSelected;
            selectedPlant = -1;
        }
        return;
    }

    // Los clics de colocacion se procesan antes en main; aqui un clic cancela la vista previa.
    if (selectedPlant >= 0)
    {
        selectedPlant = -1;
        return;
    }

    const int count = std::min(plantSlotCount, int(plants.size()));
    for (int i = 0; i < count; ++i)
    {
        const Rectangle card = getPlantRectangle(i);
        if (card.width <= 0 || card.height <= 0 || !CheckCollisionPointRec(cursorPosition, card)) continue;

        // Cobrar una sola vez y solo si alcanza el saldo y hay un sprite valido.
        if (plants[i].hasSprite() && playerCurrency >= plants[i].getCost())
        {
            playerCurrency -= plants[i].getCost();
            selectedPlant = i;
            shovelSelected = false;
        }
        return;
    }
    shovelSelected = false;
}

void Selector::drawSelector(int playerSuns, int manualZombieCount) const
{
    const Color wood = {111, 76, 43, 255};
    const Color trim = {196, 149, 83, 255};
    Rectangle shadow = {rec.x + 3, rec.y + 5, rec.width, rec.height};
    DrawRectangleRounded(shadow, 0.12f, 8, Fade(BLACK, 0.25f));
    DrawRectangleRounded(rec, 0.12f, 8, wood);
    DrawRectangleRoundedLinesEx(rec, 0.12f, 8, 2.0f, trim);

    // El saldo tiene su propio espacio; su valor no mueve las tarjetas.
    const Rectangle balance = getCellRectangle(0);
    DrawRectangleRounded(balance, 0.16f, 8, {78, 52, 31, 255});
    DrawRectangleRoundedLinesEx(balance, 0.16f, 8, 1.5f, trim);
    Rectangle label = {balance.x + 4, balance.y + 5, std::max(0.0f, balance.width - 8), balance.height * 0.16f};
    drawCenteredText("SOLES", label, 18, {242, 216, 164, 255});
    const Vector2 coin = {balance.x + balance.width / 2.0f, balance.y + balance.height * 0.38f};
    const float radius = std::max(0.0f, std::min(balance.width * 0.17f, balance.height * 0.15f));
    DrawCircleV({coin.x + 2, coin.y + 2}, radius, {159, 104, 21, 255});
    DrawCircleV(coin, radius, GOLD);
    DrawCircleLinesV(coin, radius, {255, 240, 135, 255});
    Rectangle amount = {balance.x + 6, balance.y + balance.height * 0.60f, std::max(0.0f, balance.width - 12), balance.height * 0.32f};
    drawCenteredText(std::to_string(playerSuns), amount, 48, WHITE);

    const int count = std::min(plantSlotCount, int(plants.size()));
    for (int i = 0; i < count; ++i)
    {
        const Rectangle card = getPlantRectangle(i);
        if (card.width <= 0 || card.height <= 0) continue;
        const bool selected = selectedPlant == i;
        const bool affordable = plants[i].hasSprite() && playerSuns >= plants[i].getCost();
        const bool hovered = CheckCollisionPointRec(cursorPosition, card);
        const Color border = selected ? GOLD : (hovered && affordable ? Color{255, 228, 153, 255} : trim);

        Rectangle cardShadow = {card.x + 2, card.y + 3, card.width, card.height};
        DrawRectangleRounded(cardShadow, 0.08f, 6, Fade(BLACK, 0.28f));
        DrawRectangleRounded(card, 0.08f, 6, affordable || selected ? Color{245, 224, 173, 255} : Color{145, 126, 100, 255});
        const float padding = std::min(4.0f, card.width * 0.08f);
        Rectangle image = {card.x + padding, card.y + padding, card.width - 2 * padding, card.height - 2 * padding};
        plants[i].drawBanner(image, affordable || selected ? WHITE : Color{125, 125, 125, 255});
        DrawRectangleRoundedLinesEx(card, 0.08f, 6, selected || (hovered && affordable) ? 3.0f : 1.5f, border);
    }
    // Mostrar la configuracion de X en los espacios sin plantas, sin tapar la pala.
    if (manualZombieCount > 0 && count < plantSlotCount)
    {
        const Rectangle first = getCellRectangle(count + 1);
        const Rectangle last = getCellRectangle(slotCount - 2);
        const float width = last.x + last.width - first.x;
        Rectangle amount = {first.x, first.y + first.height * 0.20f, width, first.height * 0.30f};
        Rectangle controls = {first.x, first.y + first.height * 0.52f, width, first.height * 0.22f};
        drawCenteredText("X: " + std::to_string(manualZombieCount) + " zombis", amount, 28, {255, 226, 167, 255});
        drawCenteredText("+ / -  cantidad", controls, 18, {242, 216, 164, 255});
    }

    // La ultima celda contiene la herramienta gratuita, separada de las compras.
    const Rectangle shovel = getShovelRectangle();
    const bool hovered = CheckCollisionPointRec(cursorPosition, shovel);
    DrawRectangleRounded(shovel, 0.12f, 8, {236, 216, 177, 255});
    DrawRectangleRoundedLinesEx(shovel, 0.12f, 8, shovelSelected || hovered ? 3.0f : 1.5f,
        shovelSelected ? GOLD : (hovered ? Color{255, 228, 153, 255} : trim));
    Rectangle icon = {shovel.x + 10, shovel.y + 8, std::max(0.0f, shovel.width - 20), shovel.height * 0.73f};
    drawShovel(icon);
    Rectangle shovelLabel = {shovel.x + 4, shovel.y + shovel.height * 0.8f, std::max(0.0f, shovel.width - 8), shovel.height * 0.16f};
    drawCenteredText("PALA", shovelLabel, 18, wood);
}

void Selector::drawSelectionPreview() const
{
    if (shovelSelected)
    {
        const float height = std::clamp(rec.height * 0.75f, 72.0f, 160.0f);
        drawShovel({cursorPosition.x - height * 0.35f, cursorPosition.y - height / 2, height * 0.7f, height});
        return;
    }
    if (selectedPlant < 0 || selectedPlant >= int(plants.size())) return;
    const float height = std::clamp(rec.height, 96.0f, 220.0f);
    plants[selectedPlant].drawPlant(cursorPosition, height);
}

bool Selector::isShovelSelected() const
{
    return shovelSelected;
}

Rectangle Selector::getBounds() const
{
    return rec;
}

const Plant* Selector::getSelectedPlant() const
{
    if (selectedPlant < 0 || selectedPlant >= int(plants.size())) return nullptr;
    return &plants[selectedPlant];
}

void Selector::clearSelection()
{
    selectedPlant = -1;
    shovelSelected = false;
}

void Selector::updateSelector(int w, int h)
{
    rec.width = w * 0.9f;
    rec.height = h * 0.2f; // Mas altura para mostrar los banners completos y legibles.
    rec.x = (w - rec.width) / 2.0f;
    rec.y = 10;
}

Selector::~Selector()
{
    if (IsTextureValid(shovelTexture)) UnloadTexture(shovelTexture);
}
