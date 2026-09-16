using UnityEngine;

namespace LegoPuzzle.Data
{
    [CreateAssetMenu(fileName = "BlockPalette", menuName = "LEGO Puzzle/Block Palette")]
    public class BlockPalette : ScriptableObject
    {
        [Header("Префаби елементів сітки (Опціонально)")]
        [Tooltip("Префаб звичайного ігрового тайлу підлоги")]
        public GameObject walkableTilePrefab;

        [Tooltip("Префаб дерев'яного блоку-перешкоди (повна клітинка 1x1)")]
        public GameObject obstacleTilePrefab;

        [Tooltip("Префаб перешкоди в половину ширини (Half Obstacle, опціонально)")]
        public GameObject halfObstaclePrefab;

        [Tooltip("Префаб четвертинки перешкоди для кутків (Quarter Obstacle, опціонально)")]
        public GameObject quarterObstaclePrefab;

        [Tooltip("Префаб воріт виходу (зі стрілочкою, повна клітинка 1x1)")]
        public GameObject exitGatePrefab;

        [Tooltip("Префаб напів-воріт виходу (половина ширини, опціонально)")]
        public GameObject halfExitGatePrefab;

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

        [Tooltip("Спрайт стрілочки для воріт виходу (якщо не вказано, генерується автоматично)")]
        public Sprite exitGateArrowSprite;

        [Header("Звукові ефекти (SFX)")]
        [Tooltip("Перший звук переміщення деталі при утримуванні (чергується)")]
        public AudioClip pieceMoveSound1;

        [Tooltip("Другий звук переміщення деталі при утримуванні (чергується)")]
        public AudioClip pieceMoveSound2;

        [Tooltip("Звук кроку/зміни клітинки блоком (клацання/шарудіння)")]
        public AudioClip pieceStepSound;

        [Tooltip("Звук вильоту блоку у ворота")]
        public AudioClip pieceExitSound;

        [Tooltip("Звук перемоги на рівні")]
        public AudioClip levelWonSound;
    }
}
