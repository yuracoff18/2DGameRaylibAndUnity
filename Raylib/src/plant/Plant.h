#ifndef PLANT_H
#define PLANT_H

#include <raylib.h>
#include <memory>
#include <string>

class Plant
{
private:
    // Las copias de Plant comparten los recursos y los liberan una sola vez.
    struct Resources
    {
        Texture2D plantTexture{};
        Texture2D bannerTexture{};
        ~Resources();
    };

    int cost;
    double currencyInterval;
    double shotCooldown;
    std::shared_ptr<Resources> resources;
public:
    Plant(std::string plant_sprite_url, std::string banner_image_url, int cost, double currencyInterval = 0.0, double shotCooldown = 0.0);
    int getCost() const;
    double getCurrencyInterval() const;
    double getShotCooldown() const;
    bool hasSprite() const;
    void drawPlant(Vector2 position, float height) const;
    void drawBanner(Rectangle destination, Color tint = WHITE) const;
    ~Plant();
};
#endif
