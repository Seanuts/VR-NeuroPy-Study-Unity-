using UnityEngine;
using TMPro;

public class ScreenTimer : MonoBehaviour{
    [Header("Timer Settings")]
    public TextMeshProUGUI timerText; 
    public float timePerScreen = 60f; //starting time of each new screen
    private float timeRemaining;
    private bool timerIsRunning = false;

    [Header("Page Settings")]
    public GameObject[] pages; 
    private int currentPageIndex = 0; 

    void Start(){
        timeRemaining = timePerScreen;
        timerIsRunning = true; 

        for (int i = 0; i < pages.Length; i++){
            if (pages[i] != null){
                pages[i].SetActive(i == 0); //only sets to true if very first item (i= 0)
            }
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

    void AdvanceToNextPage(){
        if (pages[currentPageIndex] != null){
            pages[currentPageIndex].SetActive(false);
        }

        currentPageIndex++; 

        if (currentPageIndex < pages.Length){ //still has pages?
            if (pages[currentPageIndex] != null){//turn on new page

                pages[currentPageIndex].SetActive(true);
            }
    
            timeRemaining = timePerScreen;//reset timer
            Debug.Log("Swapped to page: " + (currentPageIndex + 1));
        }
        else{
            timeRemaining = 0;
            timerIsRunning = false;
            timerText.text = "00:00";
            Debug.Log("Study Complete -> No more pages.");
        }
    }

    void UpdateTimerDisplay(float timeToDisplay){
        timeToDisplay += 1; 
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds); 
    }
}