using System;
using AlchemyBar.Evaluation;
using AlchemyBar.UI.Menus;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AlchemyBar.Core
{
    public sealed class SessionStats : MonoBehaviour
    {
        public static int PotionsBrewed { get; private set; }
        public static int GoldEarned { get; private set; }
        public static float SessionSeconds { get; private set; }

        public static event Action Changed;

        [SerializeField] private int successfulBaseGold = 20;
        [SerializeField] private int successfulBonusGold = 30;
        [SerializeField] private int perfectBaseGold = 50;
        [SerializeField] private int perfectBonusGold = 50;

        private PotionEvaluator[] evaluators = new PotionEvaluator[0];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<SessionStats>() != null) return;
            var go = new GameObject("Session Stats");
            DontDestroyOnLoad(go);
            go.AddComponent<SessionStats>();
        }

        public static void ResetSession()
        {
            PotionsBrewed = 0;
            GoldEarned = 0;
            SessionSeconds = 0f;
            Changed?.Invoke();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Subscribe();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unsubscribe();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Unsubscribe();
            Subscribe();
        }

        private void Subscribe()
        {
            evaluators = FindObjectsByType<PotionEvaluator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PotionEvaluator e in evaluators) e.PotionEvaluated += OnPotionEvaluated;
        }

        private void Unsubscribe()
        {
            foreach (PotionEvaluator e in evaluators)
                if (e != null) e.PotionEvaluated -= OnPotionEvaluated;
            evaluators = new PotionEvaluator[0];
        }

        private void OnPotionEvaluated(PotionEvaluationResult result)
        {
            if (result == null || result.Grade == PotionQualityGrade.Failed) return;

            float score = Mathf.Clamp01(result.OverallScore);
            int gold = result.Grade == PotionQualityGrade.Perfect
                ? perfectBaseGold + Mathf.RoundToInt(perfectBonusGold * score)
                : successfulBaseGold + Mathf.RoundToInt(successfulBonusGold * score);

            PotionsBrewed++;
            GoldEarned += gold;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (HexHearthMenuRouter.Mood == MusicMood.Gameplay && Time.timeScale > 0f)
                SessionSeconds += Time.unscaledDeltaTime;
        }
    }
}
