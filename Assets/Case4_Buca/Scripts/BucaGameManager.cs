using System;
using UnityEngine;
using System.Collections.Generic;

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

        public int TotalBlocks => totalBlocks;
        public int KnockedBlocks => knockedBlocks;
        public bool IsLevelCleared => isLevelCleared;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            InitializeLevel();
        }

        public void InitializeLevel()
        {
            // Find all blocks if not assigned
            if (targetBlocks == null || targetBlocks.Count == 0)
            {
                targetBlocks = new List<BucaBlock>(FindObjectsOfType<BucaBlock>());
            }

            totalBlocks = targetBlocks.Count;
            knockedBlocks = 0;
            isLevelCleared = false;

            // Setup track colliders dynamically if not present
            SetupTrackColliders();
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

                if (BucaAudioManager.Instance != null)
                {
                    BucaAudioManager.Instance.PlayVictorySound();
                }
            }
        }

        public void RestartLevel()
        {
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
    }
}
