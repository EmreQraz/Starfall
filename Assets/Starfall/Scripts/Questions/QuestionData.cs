using System.Collections.Generic;
using UnityEngine;

namespace Starfall.Questions
{
    /// <summary>
    /// A single multiple-choice question. Create one via Assets > Create > Starfall > Question
    /// and drag it onto a RepairableDevice; no code is required to add new questions.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuestion", menuName = "Starfall/Question", order = 0)]
    public class QuestionData : ScriptableObject
    {
        public const int OPTION_COUNT = 4;

        [SerializeField, TextArea(3, 10)]
        [Tooltip("Supports TextMeshPro rich text, e.g. <color=#7FDBFF>code</color>.")]
        private string questionText;

        [SerializeField]
        private string[] options = new string[OPTION_COUNT];

        [SerializeField, Range(0, OPTION_COUNT - 1)]
        private int correctIndex;

        [SerializeField, TextArea(2, 6)]
        [Tooltip("Shown after the player answers.")]
        private string explanation;

        public string QuestionText => questionText;
        public IReadOnlyList<string> Options => options;
        public int CorrectIndex => correctIndex;
        public string Explanation => explanation;

        public bool IsCorrect(int optionIndex) => optionIndex == correctIndex;

        private void OnValidate()
        {
            if (options == null || options.Length != OPTION_COUNT)
            {
                Debug.LogWarning($"Question '{name}' should have exactly {OPTION_COUNT} options.", this);
            }
        }
    }
}
