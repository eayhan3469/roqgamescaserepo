using System.Collections.Generic;
using System.Linq;
using _Efsun.Scripts.EfsunCommon.Factories.Helpers;
using _Efsun.Scripts.EfsunCommon.Models.Interfaces;
using _HoleBlock.Scripts.Contexts.Gameplay.Contexts.MovableBlockContext.Models;
using _HoleBlock.Scripts.Contexts.Gameplay.Controllers;
using _HoleBlockGame.Scripts.Contexts.Gameplay.Contexts.BlockContext.Settings;
using MoreMountains.Feedbacks;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

namespace _HoleBlockGame.Scripts.Contexts.Gameplay.Contexts.BlockContext.Controllers
{
    public class BlockAnimationRayFireShatterController : MonoBehaviour, IContextPreparerMono, IContextInitializable
    {
        [SerializeField] private BlockAnimationModelParent _parentTransform;

        [Inject] private BaseBlockSettings _baseBlockSettings;
        [Inject] private BlockVisual _blockVisual;

        private MMF_Player _mmfBombManual;


        public List<BlockVisualFracture> GameObjects { get; private set; } = new();
        public MMF_Player MmfBombManual => _mmfBombManual;
        public BlockAnimationModelParent ParentTransform => _parentTransform;

        public void ShatterObject()
        {
            GameObjects.Clear();
            foreach (Transform fragment in _parentTransform.transform)
            {
                var fracture = fragment.GetComponent<BlockVisualFracture>();
                if (fracture == null)
                {
                    fracture = fragment.gameObject.AddComponent<BlockVisualFracture>();
                }
                GameObjects.Add(fracture);
            }
        }

        public void Prepare(DiContainer diContainer, InitializableContextBinder contextBinder)
        {
            diContainer.Bind<BlockAnimationRayFireShatterController>().FromInstance(this).AsSingle();
            contextBinder.RegisterInjectableInstance(this);
            contextBinder.RegisterContextInitializableInstance(this);
        }

        public int Order => 0;

        public void Initialize()
        {
            _mmfBombManual = Object.Instantiate(_baseBlockSettings.MmfBombManualPrefab, transform);
            var bombManual = (MMF_BombManual)_mmfBombManual.FeedbacksList[0];
            bombManual.Parent = _parentTransform;
            bombManual.DisableRenderers.Add(_blockVisual.BlockMeshRenderer.gameObject);

            ((MMF_SetActive)_mmfBombManual.FeedbacksList[1]).TargetGameObject = _parentTransform.gameObject;
        }
    }
}