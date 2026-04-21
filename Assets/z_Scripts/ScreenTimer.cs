using UnityEngine;
using TMPro;

[System.Serializable]
public class StudyPage{
    public GameObject pageObject;
    public float displayTime = 60f; 
}

public class ScreenTimer : MonoBehaviour{
    [Header("Timer Settings")]
    public TextMeshProUGUI timerText; 
    private float timeRemaining;
    private bool timerIsRunning = false;

    [Header("Page Settings")]
    public StudyPage[] pages; 
    private int currentPageIndex = 0; 

    void Start(){
        if (pages.Length > 0){
            timeRemaining = pages[0].displayTime;
            timerIsRunning = true; 

            for (int i = 0; i < pages.Length; i++){
                if (pages[i].pageObject != null){
                    pages[i].pageObject.SetActive(i == 0); 
                }
            }
        }
        else
        {
            Debug.LogWarning("You haven't assigned any pages in the Inspector!");
        }
    }

    void Update(){
        if (timerIsRunning){
            if (timeRemaining > 0){
                timeRemaining -= Time.deltaTime; 
                UpdateTimerDisplay(timeRemaining);
            }
            else{
                AdvanceToNextPage();
            }
        }
    }

    void AdvanceToNextPage()
    {
        if (pages[currentPageIndex].pageObject != null){
            pages[currentPageIndex].pageObject.SetActive(false);
        }

        currentPageIndex++; 

        if (currentPageIndex < pages.Length) { 
            if (pages[currentPageIndex].pageObject != null){
                pages[currentPageIndex].pageObject.SetActive(true);
            }
    
            timeRemaining = pages[currentPageIndex].displayTime;
        }
        else{
            timeRemaining = 0;
            timerIsRunning = false;
            
            // TELL THE MASTER SCRIPT WE ARE DONE!
            if (StudyCoordinator.Instance != null){
                StudyCoordinator.Instance.OnPhaseComplete();
            }
            else{
                Debug.LogError("No StudyCoordinator found in the scene, make sure you started from the Start scene.");
            }
        }
    }

    void UpdateTimerDisplay(float timeToDisplay){
        timeToDisplay += 1; 
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds); 
    }
}