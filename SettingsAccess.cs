namespace GPT9000_WinForms_TestSystem;

internal sealed class SettingsAccess
{
    public bool Unlocked { get; private set; }
    public bool IsRoot { get; private set; }

    public bool Unlock(string password)
    {
        if (password is not ("123456" or "root123456")) return false;
        Unlocked = true;
        IsRoot = password == "root123456";
        return true;
    }

    public void Lock() => (Unlocked, IsRoot) = (false, false);
}
