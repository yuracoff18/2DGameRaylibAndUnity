#include "Plant.h"
#include <algorithm>

namespace
{
    void prepareTexture(Texture2D& texture)
    {
        if (!IsTextureValid(texture)) return;
        GenTextureMipmaps(&texture);
        SetTextureFilter(texture, TEXTURE_FILTER_TRILINEAR);
    }
}

Plant::Resources::~Resources()
{
    if (IsTextureValid(bannerTexture)) UnloadTexture(bannerTexture);
    if (IsTextureValid(plantTexture)) UnloadTexture(plantTexture);
}

Plant::Plant(std::string Plant_sprite_url, std::string banner_image_url, int cost, double currencyInterval, double shotCooldown)
    : cost(cost), currencyInterval(std::max(0.0, currencyInterval)), shotCooldown(std::max(0.0, shotCooldown)), resources(std::make_shared<Resources>())
{
    // Cargar ambas texturas una vez y compartirlas entre las copias de la planta.
    resources->plantTexture = LoadTexture(Plant_sprite_url.c_str());
    resources->bannerTexture = LoadTexture(banner_image_url.c_str());
    prepareTexture(resources->plantTexture);
    prepareTexture(resources->bannerTexture);
}

void Plant::drawBanner(Rectangle destination, Color tint) const {
    const Texture2D& banner = resources->bannerTexture;
    if (!IsTextureValid(banner) || destination.width <= 0 || destination.height <= 0) return;

    // Usar la misma escala en ambos ejes para conservar la proporcion original.
    const float scale = std::min(destination.width / banner.width, destination.height / banner.height);
    Rectangle fitted = {0.0f, 0.0f, banner.width * scale, banner.height * scale};

    // Centrar la imagen dentro de la celda aprovechando el espacio disponible.
    fitted.x = destination.x + (destination.width - fitted.width) / 2.0f;
    fitted.y = destination.y + (destination.height - fitted.height) / 2.0f;

    Rectangle source = {0.0f, 0.0f, float(banner.width), float(banner.height)};
    DrawTexturePro(banner, source, fitted, {0.0f, 0.0f}, 0.0f, tint);
}

int Plant::getCost() const {
    return cost;
}

double Plant::getCurrencyInterval() const {
    return currencyInterval;
}

double Plant::getShotCooldown() const {
    return shotCooldown;
}

bool Plant::hasSprite() const {
    return IsTextureValid(resources->plantTexture);
}

void Plant::drawPlant(Vector2 position, float height) const {
    if (!hasSprite() || height <= 0) return;

    // El sprite conserva su proporcion y queda centrado en el cursor.
    const Texture2D& texture = resources->plantTexture;
    const float width = height * texture.width / texture.height;
    Rectangle source = {0.0f, 0.0f, float(texture.width), float(texture.height)};
    Rectangle destination = {position.x - width / 2.0f, position.y - height / 2.0f, width, height};
    DrawTexturePro(texture, source, destination, {0.0f, 0.0f}, 0.0f, WHITE);
}

Plant::~Plant()
{
}
