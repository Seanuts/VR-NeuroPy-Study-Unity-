using UnityEngine;
using TMPro;

// 1. We create a custom container that holds BOTH the page and its specific timer.
// [System.Serializable] tells Unity to show this custom group in the Inspector.
[System.Serializable]
public class StudyPage
{
    public GameObject pageObject;
    public float displayTime = 60f; // Default to 60s, but you can change this in the Inspector
}

public class ScreenTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public TextMeshProUGUI timerText; 
    private float timeRemaining;
    private bool timerIsRunning = false;

    [Header("Page Settings")]
    // 2. We use our new custom class instead of a standard GameObject array
    public StudyPage[] pages; 
    private int currentPageIndex = 0; 

    void Start()
    {
        // 3. Check if we actually have pages to avoid errors
        if (pages.Length > 0)
        {
            // Set the starting time to the FIRST page's specific time
            timeRemaining = pages[0].displayTime;
            timerIsRunning = true; 

            // Turn off all pages except the first one
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i].pageObject != null)
                {
                    pages[i].pageObject.SetActive(i == 0); 
                }
            }
        }
        else
        {
            Debug.LogWarning("You haven't assigned any pages in the Inspector!");
        }
    }

    void Update()
    {
        if (timerIsRunning)
        {
            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime; 
                UpdateTimerDisplay(timeRemaining);
            }
            else
            {
                AdvanceToNextPage();
            }
        }
    }

    void AdvanceToNextPage()
    {
        // Turn off the current page
        if (pages[currentPageIndex].pageObject != null)
        {
            pages[currentPageIndex].pageObject.SetActive(false);
        }

        currentPageIndex++; 

        // Are there still pages left?
        if (currentPageIndex < pages.Length) 
        { 
            // Turn on the new page
            if (pages[currentPageIndex].pageObject != null)
            {
                pages[currentPageIndex].pageObject.SetActive(true);
            }
    
            // 4. Reset the timer using the NEW page's custom time
            timeRemaining = pages[currentPageIndex].displayTime;
            
            Debug.Log("Swapped to page: " + (currentPageIndex + 1) + ". Timer set for: " + timeRemaining + " seconds.");
        }
        else
        {
            timeRemaining = 0;
            timerIsRunning = false;
            timerText.text = "00:00";
            Debug.Log("Study Complete -> No more pages.");
        }
    }

    void UpdateTimerDisplay(float timeToDisplay)
    {
        timeToDisplay += 1; 
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);
        
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds); 
    }
}