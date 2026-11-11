#include "Zombie.h"
#include <algorithm>
#include <cmath>
#include <utility>
#include <vector>

namespace
{
    struct Frame
    {
        Texture2D texture{};
        Rectangle source{};
    };

    Frame loadFrame(const char* path)
    {
        Frame frame;
        Image image = LoadImage(path);
        if (IsImageValid(image))
        {
            frame.source = GetImageAlphaBorder(image, 0.1f);
            frame.texture = LoadTextureFromImage(image);
            UnloadImage(image);
            if (IsTextureValid(frame.texture))
            {
                GenTextureMipmaps(&frame.texture);
                SetTextureFilter(frame.texture, TEXTURE_FILTER_TRILINEAR);
            }
        }
        return frame;
    }
}

struct Zombie::Resources
{
    std::vector<Frame> walking;
    std::vector<Frame> eating;
    std::vector<Frame> falling;
    float referenceHeight = 1.0f;

    ~Resources()
    {
        for (const auto* frames : {&walking, &eating, &falling})
            for (const Frame& frame : *frames)
                if (IsTextureValid(frame.texture)) UnloadTexture(frame.texture);
    }
};

Zombie::ResourcesPtr Zombie::loadResources()
{
    // Todos los zombis comparten los fotogramas; no se recargan al aparecer ni al reintentar.
    auto resources = std::make_shared<Resources>();
    for (const char* path : {"assets/Zombies/zombieCaminando.png", "assets/Zombies/zombieCaminando1.png",
        "assets/Zombies/zombieCaminando2.png", "assets/Zombies/zombieCaminando3.png", "assets/Zombies/zombieCaminando4.png"})
    {
        resources->walking.push_back(loadFrame(path));
        resources->referenceHeight = std::max(resources->referenceHeight, resources->walking.back().source.height);
    }
    for (const char* path : {"assets/Zombies/zombieComiendo.png", "assets/Zombies/zombieComiendo1.png", "assets/Zombies/zombieComiendo2.png"})
        resources->eating.push_back(loadFrame(path));
    for (const char* path : {"assets/Zombies/zombieAgachandose.png", "assets/Zombies/zombieAgachandose1.png"})
        resources->falling.push_back(loadFrame(path));
    return resources;
}

Zombie::Zombie(ResourcesPtr resources, int row, float x, double now)
    : resources(std::move(resources)), row(row), x(x), previousX(x), updatedAt(now), spawnedAt(now)
{
}

int Zombie::getRow() const { return row; }
int Zombie::getHealth() const { return health; }
float Zombie::getX() const { return x; }
float Zombie::getPreviousX() const { return previousX; }
double Zombie::getUpdatedAt() const { return updatedAt; }
bool Zombie::isAlive() const { return health > 0; }
bool Zombie::finishedFalling(double now) const { return !isAlive() && now - fellAt >= 0.8; }

bool Zombie::takeHit(double now)
{
    if (!isAlive()) return false;
    --health;
    hitFlashUntil = now + 0.16;
    if (health == 0) fellAt = now; // El cuarto impacto inicia la caida y deja de ser un objetivo.
    return health == 0;
}

void Zombie::moveTo(float position, double now)
{
    previousX = x;
    x = position;
    updatedAt = now;
}

void Zombie::scaleX(float scale)
{
    x *= scale;
    previousX *= scale;
}

void Zombie::startEating(int column, double now)
{
    if (eatingColumn == column) return;
    eatingColumn = column;
    eatingSince = now;
    nextBiteTime = now + 1.1;
}

void Zombie::stopEating() { eatingColumn = -1; }
double Zombie::getNextBiteTime() const { return nextBiteTime; }
void Zombie::advanceBite() { nextBiteTime += 1.1; }

void Zombie::draw(Vector2 feet, float height, double now) const
{
    const float fall = isAlive() ? 0.0f : std::clamp(float((now - fellAt) / 0.8), 0.0f, 1.0f);
    const auto& frames = !isAlive() ? resources->falling : (eatingColumn >= 0 ? resources->eating : resources->walking);
    if (frames.empty()) return;
    const double animationStart = !isAlive() ? fellAt : (eatingColumn >= 0 ? eatingSince : spawnedAt);
    const std::size_t index = !isAlive() ? std::min(std::size_t(fall * frames.size()), frames.size() - 1)
        : std::size_t(std::max(0.0, now - animationStart) * 7.0) % frames.size();
    const Frame& frame = frames[index];
    if (!IsTextureValid(frame.texture)) return;

    // Una escala comun y los pies anclados evitan saltos al cambiar de fotograma.
    const float scale = height / resources->referenceHeight;
    const float width = frame.source.width * scale;
    const float frameHeight = frame.source.height * scale;
    Rectangle destination = {feet.x + (frame.source.x - frame.texture.width / 2.0f) * scale,
        feet.y - frameHeight, width, frameHeight};
    Vector2 origin = {0, 0};
    float rotation = 0;
    if (!isAlive())
    {
        destination.x = feet.x;
        destination.y = feet.y;
        origin = {width / 2.0f, frameHeight};
        rotation = -80.0f * fall;
    }
    const Color tint = Fade(WHITE, 1.0f - fall);
    DrawTexturePro(frame.texture, frame.source, destination, origin, rotation, tint);
    if (now < hitFlashUntil)
    {
        // Un brillo aditivo breve afecta al sprite, sin iluminar su fondo transparente.
        BeginBlendMode(BLEND_ADDITIVE);
        DrawTexturePro(frame.texture, frame.source, destination, origin, rotation,
            Fade(WHITE, float((hitFlashUntil - now) / 0.16) * 0.45f * (1.0f - fall)));
        EndBlendMode();
    }
}
