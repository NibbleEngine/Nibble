using NbCore;

namespace NbCore
{
    public enum MouseMovementStatus
    {
        CAMERA_MOVEMENT = 0x0,
        GIZMO_MOVEMENT,
        IDLE
    }

    public enum NbMouseButton
    {
        LEFT,
        RIGHT,
        MIDDLE
    }

    public unsafe struct NbMouseState
    {
        public fixed bool ButtonDown[3];
        public NbVector2 Position;
        public NbVector2 PositionDelta;
        public NbVector2 Scroll;
        //public NbVector2 ScrollDelta;
        public bool UpdateScene;

        public bool IsButtonDown(NbMouseButton button)
        {
            return ButtonDown[(int)button];
        }

        public void SetButtonStatus(NbMouseButton button, bool state)
        {
            ButtonDown[(int)button] = state;
        }

    }



}
