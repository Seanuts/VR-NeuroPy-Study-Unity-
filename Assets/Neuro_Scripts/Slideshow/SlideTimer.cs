using UnityEngine;
using TMPro;
using System;

public class SlideTimer : MonoBehaviour
{
    [SerializeField] SlideshowController controller;
    [SerializeField] TextMeshProUGUI timerText;

    bool active;
    float currentTime;

    void Awake()
    {
        active = false;
        currentTime = 0;
        timerText.text = "0";
    }

    void Update()
    {
        if (active)
        {
            currentTime -= Time.deltaTime;
        }

        // end timer when clock runs out + low time warning
        if (active && currentTime <= 0)
        {
            currentTime = 0;
            active = false;
            timerText.color = Color.red;
            // let slideshow controller know
            controller.TimerExpired();
        }
        else if (currentTime <= 15)
        {
            timerText.color = Color.yellow;
        }

        // update gfx
        float secondsLeft = MathF.Round(currentTime);
        timerText.text = $"{secondsLeft}";
    }

    public void StartTimer(float timerTime)
    {
        currentTime = timerTime;
        active = true;
        timerText.color = Color.white;
    }
}
