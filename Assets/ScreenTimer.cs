using UnityEngine;
using TMPro;

public class ScreenTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public TextMeshProUGUI timerText; 
    public float timeRemaining = 60f; //60s
    private bool timerIsRunning = false;

    void Start()
    {
        timerIsRunning = true; 
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
                timeRemaining = 0;
                timerIsRunning = false;
                timerText.text = "00:00";
                
                Debug.Log("Time is up! Ready for next screen."); 
            }
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