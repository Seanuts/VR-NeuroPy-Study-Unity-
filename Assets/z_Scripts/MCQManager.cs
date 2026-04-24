using UnityEngine;
using TMPro;

[System.Serializable]
public class QuestionPage{
    // NEW: Removed pageObject because ScreenTimer controls the UI now
    [Tooltip("0 = A, 1 = B, 2 = C, 3 = D")]
    public int correctOptionIndex; 
}

public class MCQManager : MonoBehaviour{
    [Header("Quiz Settings")]
    public QuestionPage[] questions;
    private int currentQuestionIndex = 0;
    private int[] userAnswers;

    [Header("Results Screen")]
    public GameObject resultsPage;
    public TextMeshProUGUI scoreText;

    void Start(){
        userAnswers = new int[questions.Length];

        // NEW: Fill answers with -1 so unanswered questions are marked wrong
        for (int i = 0; i < userAnswers.Length; i++){
            userAnswers[i] = -1;
        }

        if (resultsPage != null){
            resultsPage.SetActive(false);
        }
        // NEW: Removed SetActive logic so it doesn't fight ScreenTimer
    }

    public void SelectOption(int selectedIndex){
        // NEW: Prevent errors if user double-clicks too fast
        if (currentQuestionIndex < questions.Length){
            userAnswers[currentQuestionIndex] = selectedIndex;
            currentQuestionIndex++;
        }

        // NEW: Tell the ScreenTimer to skip the rest of the 1-minute timer
        FindObjectOfType<ScreenTimer>().AdvanceToNextPage();
    }

    // NEW: Called by ScreenTimer if the 1-minute timer hits 00:00
    public void RegisterTimeout(){
        if (currentQuestionIndex < questions.Length){
            currentQuestionIndex++;
        }
    }

    // NEW: Made public so ScreenTimer can trigger it at the very end
    public void ShowResults(){
        int score = 0;
        for (int i = 0; i < questions.Length; i++){
            if (userAnswers[i] == questions[i].correctOptionIndex){
                score++;
            }
        }

        if (resultsPage != null){
            resultsPage.SetActive(true);
        }

        if (scoreText != null){
            scoreText.text = "You scored " + score + " out of " + questions.Length + ".";
        }
    }

    public void FinishQuizPhase(){
        if (StudyCoordinatorV2.Instance != null){
            StudyCoordinatorV2.Instance.OnPhaseComplete();
        } else {
            Debug.LogError("No StudyCoordinator found! Started from wrong scene?");
        }
    }
}