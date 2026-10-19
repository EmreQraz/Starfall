using System;
using System.Collections.Generic;
using Starfall.Interaction;
using Starfall.Questions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Starfall.UI
{
    /// <summary>
    /// The single question panel of a scene. Devices call Open() with their questions and
    /// get a callback once every question has been answered correctly.
    /// </summary>
    public class QuestionPanel : MonoBehaviour
    {
        private const string CONTINUE_LABEL = "Continue";
        private const string FINISH_LABEL = "Finish repair";
        private const string RETRY_LABEL = "Try again";
        private const string CORRECT_HEADER = "<color=#5CFF9D>CORRECT</color>";
        private const string WRONG_HEADER = "<color=#FF5C5C>INCORRECT</color>";

        [Serializable]
        private class OptionButton
        {
            public Button button;
            public TMP_Text label;
            public Image background;
        }

        [Header("References")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private OptionButton[] optionButtons = new OptionButton[QuestionData.OPTION_COUNT];
        [SerializeField] private GameObject feedbackRoot;
        [SerializeField] private TMP_Text explanationText;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private PlayerControlLock playerLock;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.05f, 0.16f, 0.22f, 0.9f);
        [SerializeField] private Color correctColor = new Color(0.1f, 0.55f, 0.3f, 0.95f);
        [SerializeField] private Color wrongColor = new Color(0.6f, 0.12f, 0.12f, 0.95f);

        [Header("Input")]
        [SerializeField]
        private InputAction closeAction = new InputAction(
            "ClosePanel", InputActionType.Button, "<Keyboard>/escape");

        private IReadOnlyList<QuestionData> questions;
        private int currentIndex;
        private bool lastAnswerCorrect;
        private Action onCompleted;
        private Action onCancelled;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (playerLock == null) playerLock = FindAnyObjectByType<PlayerControlLock>();

            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                optionButtons[i].button.onClick.AddListener(() => SelectOption(index));
            }
            continueButton.onClick.AddListener(OnContinue);

            root.SetActive(false);
        }

        private void OnEnable()
        {
            closeAction.performed += OnCloseRequested;
        }

        private void OnDisable()
        {
            closeAction.performed -= OnCloseRequested;
            closeAction.Disable();
        }

        /// <summary>
        /// Shows the questions in order. onCompleted runs only after all of them are answered correctly.
        /// onCancelled runs if the player closes the panel early (progress is not kept).
        /// </summary>
        public void Open(string title, IReadOnlyList<QuestionData> questionList, Action completed, Action cancelled = null)
        {
            if (IsOpen)
            {
                Debug.LogWarning("QuestionPanel is already open.", this);
                return;
            }
            if (questionList == null || questionList.Count == 0)
            {
                Debug.LogWarning($"'{title}' has no questions; completing immediately.", this);
                completed?.Invoke();
                return;
            }

            questions = questionList;
            onCompleted = completed;
            onCancelled = cancelled;
            currentIndex = 0;
            IsOpen = true;

            titleText.text = title;
            root.SetActive(true);
            closeAction.Enable();
            if (playerLock != null) playerLock.Lock();

            ShowQuestion();
        }

        private void ShowQuestion()
        {
            var question = questions[currentIndex];
            if (question == null)
            {
                Debug.LogError($"Question slot {currentIndex} in '{titleText.text}' is empty.", this);
                Close(onCancelled);
                return;
            }

            progressText.text = $"Question {currentIndex + 1} / {questions.Count}";
            questionText.text = question.QuestionText;

            for (int i = 0; i < optionButtons.Length; i++)
            {
                var option = optionButtons[i];
                bool hasOption = i < question.Options.Count && !string.IsNullOrEmpty(question.Options[i]);
                option.button.gameObject.SetActive(hasOption);
                if (!hasOption) continue;

                option.label.text = $"{(char)('A' + i)})  {question.Options[i]}";
                option.background.color = normalColor;
                option.button.interactable = true;
            }

            feedbackRoot.SetActive(false);
        }

        private void SelectOption(int index)
        {
            var question = questions[currentIndex];
            lastAnswerCorrect = question.IsCorrect(index);

            foreach (var option in optionButtons) option.button.interactable = false;
            optionButtons[index].background.color = lastAnswerCorrect ? correctColor : wrongColor;

            explanationText.text = $"{(lastAnswerCorrect ? CORRECT_HEADER : WRONG_HEADER)}\n{question.Explanation}";
            bool isLast = currentIndex == questions.Count - 1;
            continueLabel.text = !lastAnswerCorrect ? RETRY_LABEL : isLast ? FINISH_LABEL : CONTINUE_LABEL;
            feedbackRoot.SetActive(true);
        }

        private void OnContinue()
        {
            if (!lastAnswerCorrect)
            {
                ShowQuestion();
                return;
            }

            currentIndex++;
            if (currentIndex < questions.Count)
            {
                ShowQuestion();
                return;
            }

            Close(onCompleted);
        }

        private void OnCloseRequested(InputAction.CallbackContext context)
        {
            if (IsOpen) Close(onCancelled);
        }

        private void Close(Action callback)
        {
            IsOpen = false;
            root.SetActive(false);
            closeAction.Disable();
            if (playerLock != null) playerLock.Unlock();

            onCompleted = null;
            onCancelled = null;
            questions = null;
            callback?.Invoke();
        }
    }
}
