public static class InputManager
{
    private static GameInputActions _instance;

    public static GameInputActions Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GameInputActions();
            }
            return _instance;
        }
    }
}