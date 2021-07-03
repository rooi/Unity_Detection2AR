using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextMeshFadeAlpha : MonoBehaviour
{
    [SerializeField] private AnimationCurve fadeCurve;
    [SerializeField] private float delay = 0f;
    TMPro.TMP_Text text;
    Color _faceColor;
    Color _outlineColor;
    float _timer = 0f;

    private void Awake()
    {
        text = GetComponent<TMPro.TMP_Text>(); // do this in awake, it has an impact on performances in Update

        _timer = -delay;

        _faceColor = text.faceColor;
        _outlineColor = text.outlineColor;

        // Set to initial value
        var alpha = fadeCurve.Evaluate(_timer);
        _faceColor.a = alpha;
        _outlineColor.a = alpha;

        text.faceColor = _faceColor;
        text.outlineColor = _outlineColor;

    }

    private void Update()
    {
        _timer += Time.deltaTime;

        var alpha = fadeCurve.Evaluate(_timer);
        _faceColor.a = alpha;
        _outlineColor.a = alpha;

        text.faceColor = _faceColor;
        text.outlineColor = _outlineColor;

    }
}
