using UnityEngine;

public class DCFilter
{
    private float _r; // Feedback-Faktor
    private float _prevInput;
    private float _prevOutput;

    public DCFilter(float r = 0.995f)
    {
        _r = r;
    }

    public float Process(float input)
    {
        float output = input - _prevInput + _r * _prevOutput;
        _prevInput = input;
        _prevOutput = output;
        return output;
    }

    public void Reset()
    {
        _prevInput = 0f;
        _prevOutput = 0f;
    }
}

