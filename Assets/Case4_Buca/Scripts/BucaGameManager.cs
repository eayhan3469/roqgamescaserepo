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
                    bounciness = 0.15f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
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

                // 1. Calculate arena center of target blocks for celebration VFX
                Vector3 arenaCenter = Vector3.zero;
                int validCount = 0;
                foreach (var b in targetBlocks)
                {
                    if (b != null)
                    {
                        arenaCenter += b.transform.position;
                        validCount++;
                    }
                }
                if (validCount > 0) arenaCenter /= validCount;
                else arenaCenter = new Vector3(-28.5f, 0.47f, -15.5f);

                // 2. Allow blocks 0.95s to naturally tumble, bounce, and settle completely on the floor!
                float settleDelay = 0.95f;
                float interval = 0.045f;
                int blockIndex = 0;

                foreach (var block in targetBlocks)
                {
                    if (block != null && block.IsHit)
                    {
                        float warpDelay = settleDelay + (blockIndex * interval);
                        block.TriggerWarpDissolve(warpDelay);
                        blockIndex++;
                    }
                }

                // If non-hit blocks remain, also warp them at the end
                foreach (var block in targetBlocks)
                {
                    if (block != null && !block.IsHit)
                    {
                        float warpDelay = settleDelay + (blockIndex * interval);
                        block.TriggerWarpDissolve(warpDelay);
                        blockIndex++;
                    }
                }

                float celebrationDelay = settleDelay + (blockIndex * interval) + 0.12f;

                // 3. Right as the final block warps out: FIRE DUAL CONFETTI & FIREWORK CANNONS FROM LEFT & RIGHT!
                DOVirtual.DelayedCall(celebrationDelay, () =>
                {
                    // Dual Confetti Cannons from left and right screen borders
                    if (BucaJuiceManager.Instance != null)
                    {
                        BucaJuiceManager.Instance.SpawnDualVictoryCannons(arenaCenter);
                    }

                    // Loud, clean Victory Fanfare
                    if (BucaAudioManager.Instance != null)
                    {
                        BucaAudioManager.Instance.PlayVictorySound();
                    }
                });

                // 4. Automatically restart the level after the full celebration sequence
                float totalRestartDelay = celebrationDelay + 2.45f;
                if (autoRestartTween != null && autoRestartTween.IsActive()) autoRestartTween.Kill();
                autoRestartTween = DOVirtual.DelayedCall(totalRestartDelay, RestartLevel);
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
