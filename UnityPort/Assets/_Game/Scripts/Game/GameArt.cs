using TMPro;
using UnityEngine;

namespace Garden.Game
{
    /// <summary>Referencias compartidas; no contiene vida ni temporizadores de las instancias.</summary>
    [CreateAssetMenu(menuName = "Jardin/Recursos del juego")]
    public sealed class GameArt : ScriptableObject
    {
        [Header("Plantas y selector")]
        public Sprite sunflower;
        public Sprite peashooter;
        public Sprite sunflowerCard;
        public Sprite peashooterCard;
        public Sprite shovel;

        [Header("Patio y soles")]
        public Sprite grass;
        public Sprite sun;
        public Sprite white;
        public Sprite circle;

        [Header("Fotogramas, en orden")]
        public Sprite[] zombieWalk;
        public Sprite[] zombieEat;
        public Sprite[] zombieDie;

        [Header("Materiales y tipografia")]
        public Material spriteMaterial;
        public Material flashMaterial;
        public Material groundMaterial;
        public TMP_FontAsset font;

        [Header("Moldes visuales")]
        public GameObject plantViewPrefab;
        public GameObject zombieViewPrefab;
        public GameObject peaViewPrefab;
        public GameObject sunViewPrefab;
    }
}
