using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InputCanvas : MonoBehaviour
{
    public Color DefaultColor = Color.black;
    public Color PressedColor = Color.red;

    public Image Up;
    public Image Down;
    public Image Left;
    public Image Right;

    private void Update()
    {
        if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed)
        {
            Up.color = PressedColor;
        }
        else
        {
            Up.color = DefaultColor;
        }

        if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed)
        {
            Down.color = PressedColor;
        }
        else
        {
            Down.color = DefaultColor;
        }

        if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
        {
            Left.color = PressedColor;
        }
        else
        {
            Left.color = DefaultColor;
        }

        if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
        {
            Right.color = PressedColor;
        }
        else
        {
            Right.color = DefaultColor;
        }
    }
}
