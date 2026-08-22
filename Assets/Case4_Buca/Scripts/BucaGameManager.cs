using System;
using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace Buca
{
    public class BucaGameManager : MonoBehaviour
    {
        public static BucaGameManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private BucaDiscLauncher discLauncher;
        [SerializeField] private List<BucaBlock> targetBlocks = new List<BucaBlock>();

        [Header("Gameplay State")]
        [SerializeField] private int totalBlocks = 0;
        [SerializeField] private int knockedBlocks = 0;
        [SerializeField] private bool isLevelCleared = false;
        [SerializeField] private float autoRestartDelay = 1.85f;

        private Tween autoRestartTween;

        public int TotalBlocks => totalBlocks;
        public int KnockedBlocks => knockedBlocks;
        public bool IsLevelCleared => isLevelCleared;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            FindBlocksAndLauncher();
            SetupTrackColliders();
        }

        private void Start()
        {
            CountTotalBlocks();
        }

        private void FindBlocksAndLauncher()
        {
            if (discLauncher == null)
            {
                discLauncher = FindFirstObjectByType<BucaDiscLauncher>();
            }

            if (targetBlocks == null || targetBlocks.Count == 0)
            {
                targetBlocks.Clear();
                BucaBlock[] blocks = FindObjectsByType<BucaBlock>(FindObjectsSortMode.None);
                targetBlocks.AddRange(blocks);
            }
        }

        private void CountTotalBlocks()
        {
            if (targetBlocks != null && targetBlocks.Count > 0)
            {
                totalBlocks = targetBlocks.Count;
            }
            else
            {
                FindBlocksAndLauncher();
                totalBlocks = targetBlocks != null ? targetBlocks.Count : 0;
            }
            knockedBlocks = 0;
            isLevelCleared = false;
        }

        private void SetupTrackColliders()
        {
            // Find level_frame
            GameObject levelFrame = GameObject.Find("level_frame");
            if (levelFrame != null)
            {
                MeshCollider mc = levelFrame.GetComponent<MeshCollider>();
                if (mc == null) mc = levelFrame.AddComponent<MeshCollider>();

                PhysicsMaterial trackMat = new PhysicsMaterial("TrackPhysMat")
                {
                    dynamicFriction = 0.0f,
                    staticFriction = 0.0f,
                    bounciness = 0.85f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
                mc.material = trackMat;
            }

            // Find obstacle
            GameObject obstacle = GameObject.Find("obstacle");
            if (obstacle != null)
            {
                if (obstacle.GetComponent<BucaObstacle>() == null)
                {
                    obstacle.AddComponent<BucaObstacle>();
                }
            }
        }

        private void Update()
        {
            if (!isLevelCleared)
            {
                CheckBlockProgress();
            }
        }

        public void NotifyBlockHit(BucaBlock hitBlock)
        {
            if (!isLevelCleared)
            {
                CheckBlockProgress();
            }
        }

        private void CheckBlockProgress()
        {
            int count = 0;
            foreach (var block in targetBlocks)
            {
                if (block != null && block.IsHit)
                {
                    count++;
                }
            }

            knockedBlocks = count;

            if (totalBlocks > 0 && knockedBlocks >= totalBlocks * 0.75f && !isLevelCleared)
            {
                isLevelCleared = true;
                Debug.Log($"[BucaGameManager] LEVEL CLEARED! Knocked {knockedBlocks}/{totalBlocks} blocks!");

                // Play victory fanfare with a micro 0.18s delay so final block shatter crunch plays cleanly first!
                DOVirtual.DelayedCall(0.18f, () =>
                {
                    if (BucaAudioManager.Instance != null)
                    {
                        BucaAudioManager.Instance.PlayVictorySound();
                    }
                });

                // Automatically restart the level after the victory celebration
                if (autoRestartTween != null && autoRestartTween.IsActive()) autoRestartTween.Kill();
                autoRestartTween = DOVirtual.DelayedCall(autoRestartDelay, RestartLevel);
            }
        }

        public void RestartLevel()
        {
            if (autoRestartTween != null && autoRestartTween.IsActive())
            {
                autoRestartTween.Kill();
            }

            foreach (var block in targetBlocks)
            {
                if (block != null) block.ResetBlock();
            }

            if (discLauncher != null)
            {
                discLauncher.ResetDisc();
            }

            knockedBlocks = 0;
            isLevelCleared = false;
        }

        private void OnDestroy()
        {
            if (autoRestartTween != null && autoRestartTween.IsActive())
            {
                autoRestartTween.Kill();
            }
        }
    }
}
