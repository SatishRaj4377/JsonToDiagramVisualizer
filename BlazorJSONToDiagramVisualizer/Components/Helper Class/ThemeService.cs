namespace BlazorJSONToDiagramVisualizer.Components.Helper_Class
{
    public class ThemeService
    {
        public event Action OnThemeChanged;

        public string CurrentTheme { get; private set; } = "light"; // "light" or "dark"

        public ThemeSettings CurrentThemeSettings { get; set; }

        public void SetTheme(string theme, ThemeSettings themeSettings)
        {
            CurrentTheme = theme;
            CurrentThemeSettings = themeSettings;
            OnThemeChanged?.Invoke();
        }
    }

}
