using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SlideBlock", menuName = "Scriptable Objects/SlideBlock")]
public class SlideBlock : ScriptableObject
{
    public List<Slide> infographicSlides;
    public List<Slide> questionSlides;
}
