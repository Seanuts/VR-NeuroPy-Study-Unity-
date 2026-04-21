using UnityEngine;
using TMPro;

[System.Serializable]
public class QuestionPage {
    public GameObject pageObject;
    [Tooltip("0 = A, 1 = B, 2 = C, 3 = D")]
    public int correctOptionIndex; 
}

public class MCQManager : MonoBehaviour {
    [Header("Quiz Settings")]
    public QuestionPage[] questions;
    private int currentQuestionIndex = 0;
    private int[] userAnswers;

    [Header("Results Screen")]
    public GameObject resultsPage;
    public TextMeshProUGUI scoreText;

    void Start() {
        userAnswers = new int[questions.Length];
        resultsPage.SetActive(false);
        for (int i = 0; i < questions.Length; i++) {
            if (questions[i].pageObject != null) {
                questions[i].pageObject.SetActive(i == 0); 
            }
        }
    }

    public void SelectOption(int selectedIndex) {
        userAnswers[currentQuestionIndex] = selectedIndex;

        if (questions[currentQuestionIndex].pageObject != null) {
            questions[currentQuestionIndex].pageObject.SetActive(false);
        }

        currentQuestionIndex++;
        if (currentQuestionIndex < questions.Length) {
            if (questions[currentQuestionIndex].pageObject != null) {
                questions[currentQuestionIndex].pageObject.SetActive(true);
            }
        } else {
            ShowResults();
        }
    }

    void ShowResults() {
        int score = 0;
        for (int i = 0; i < questions.Length; i++) {
            if (userAnswers[i] == questions[i].correctOptionIndex) {
                score++;
            }
        }
        resultsPage.SetActive(true);
        if (scoreText != null) {
            scoreText.text = "You scored " + score + " out of " + questions.Length + ".";
        }
    }

    public void FinishQuizPhase() {
        if (StudyCoordinator.Instance != null) {
            StudyCoordinator.Instance.OnPhaseComplete();
        } else {
            Debug.LogError("No StudyCoordinator found! Cannot transition to next scene.");
        }
    }
}