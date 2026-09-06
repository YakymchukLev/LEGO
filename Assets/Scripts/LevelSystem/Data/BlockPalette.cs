using UnityEngine;

namespace LegoPuzzle.Data
{
    [CreateAssetMenu(fileName = "BlockPalette", menuName = "LEGO Puzzle/Block Palette")]
    public class BlockPalette : ScriptableObject
    {
        [Header("Префаби елементів сітки (Опціонально)")]
        [Tooltip("Префаб звичайного ігрового тайлу підлоги")]
        public GameObject walkableTilePrefab;

        [Tooltip("Префаб дерев'яного блоку-перешкоди")]
        public GameObject obstacleTilePrefab;

        [Tooltip("Префаб воріт виходу (зі стрілочкою)")]
        public GameObject exitGatePrefab;

        [Header("Матеріали елементів сітки (з текстурами)")]
        [Tooltip("Матеріал з текстурою для звичайного тайлу підлоги (наприклад, дерево M_Wood3)")]
        public Material walkableTileMaterial;

        [Tooltip("Матеріал з текстурою для перешкод (наприклад, M_Wood1)")]
        public Material obstacleTileMaterial;

        [Tooltip("Матеріал для воріт виходу")]
        public Material exitGateMaterial;

        [Header("Префаби фігур LEGO")]
        [Tooltip("Базовий префаб для блоку LEGO (LegoPieceView)")]
        public GameObject defaultLegoPiecePrefab;

        [Tooltip("Префаб одиничної секції з шипом (1x1 Lego Stud) для процедурної збірки форми")]
        public GameObject legoStudUnitPrefab;

        [Header("Матеріали / Спрайти блоків")]
        [Tooltip("Базовий матеріал для деталей LEGO (якому можна міняти tint color)")]
        public Material legoBaseMaterial;

        [Tooltip("Спрайт стрілочок обмеження <->")]
        public Sprite horizontalArrowSprite;

        [Tooltip("Спрайт стрілочок обмеження ↕")]
        public Sprite verticalArrowSprite;
    }
}
