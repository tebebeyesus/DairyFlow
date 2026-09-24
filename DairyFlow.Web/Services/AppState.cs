namespace DairyFlow.Web.Services;

public class AppState
{
    public bool IsAuthenticated { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string UserRole { get; private set; } = string.Empty;
    public string Lang { get; private set; } = "en";
    public bool IsSidebarOpen { get; private set; } = false;

    public event Action? OnChange;

    public void SetAuthenticated(bool value, string name = "", string role = "Worker", string lang = "en")
    {
        IsAuthenticated = value;
        UserName = name;
        UserRole = role;
        Lang = lang;
        NotifyStateChanged();
    }

    public void SetLang(string lang)
    {
        Lang = lang;
        NotifyStateChanged();
    }

    public void ToggleSidebar()
    {
        IsSidebarOpen = !IsSidebarOpen;
        NotifyStateChanged();
    }

    public void CloseSidebar()
    {
        if (IsSidebarOpen) { IsSidebarOpen = false; NotifyStateChanged(); }
    }

    public bool IsOwnerOrManager => UserRole is "Owner" or "Manager";

    private void NotifyStateChanged() => OnChange?.Invoke();
}
